using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.CashVouchers;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class CashVoucherService : ICashVoucherService
{
    private readonly AppDbContext _db;
    private readonly IVoucherNumberService _voucherNumberService;

    public CashVoucherService(AppDbContext db, IVoucherNumberService voucherNumberService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
    }

    public Task<string> GetNextReceiptNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.ReceiptVoucher, "RV-");

    public Task<string> GetNextPaymentNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.PaymentVoucher, "PMV-");

    public Task<CashVoucherDto?> GetReceiptByIdAsync(int id) =>
        GetByIdAsync(id, VoucherType.ReceiptVoucher);

    public Task<CashVoucherDto?> GetPaymentByIdAsync(int id) =>
        GetByIdAsync(id, VoucherType.PaymentVoucher);

    public Task<CashVoucherDto> CreateReceiptAsync(CreateCashVoucherRequest request, int userId) =>
        CreateAsync(VoucherType.ReceiptVoucher, VoucherSequenceKeys.ReceiptVoucher, "RV-", request, userId);

    public Task<CashVoucherDto> CreatePaymentAsync(CreateCashVoucherRequest request, int userId) =>
        CreateAsync(VoucherType.PaymentVoucher, VoucherSequenceKeys.PaymentVoucher, "PMV-", request, userId);

    public Task<CashVoucherDto?> UpdateReceiptAsync(int id, CreateCashVoucherRequest request) =>
        UpdateAsync(id, VoucherType.ReceiptVoucher, request);

    public Task<CashVoucherDto?> UpdatePaymentAsync(int id, CreateCashVoucherRequest request) =>
        UpdateAsync(id, VoucherType.PaymentVoucher, request);

    private async Task<CashVoucherDto?> GetByIdAsync(int id, VoucherType voucherType)
    {
        var voucher = await _db.JournalVouchers
            .Where(v => v.Id == id && v.VoucherType == voucherType)
            .Include(v => v.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .Include(v => v.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.BankDetail)
            .FirstOrDefaultAsync();

        return voucher == null ? null : MapToDto(voucher);
    }

    private async Task<CashVoucherDto> CreateAsync(
        VoucherType voucherType,
        string sequenceKey,
        string prefix,
        CreateCashVoucherRequest request,
        int userId)
    {
        await ValidateRequestAsync(voucherType, request);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var voucherNumber = await _voucherNumberService.ReserveNextNumberAsync(sequenceKey, prefix);
        var voucher = BuildVoucher(voucherType, voucherNumber, request, userId, isEdited: false);

        _db.JournalVouchers.Add(voucher);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(voucher.Id, voucherType))!;
    }

    private async Task<CashVoucherDto?> UpdateAsync(
        int id,
        VoucherType voucherType,
        CreateCashVoucherRequest request)
    {
        var voucher = await _db.JournalVouchers
            .Where(v => v.Id == id && v.VoucherType == voucherType)
            .Include(v => v.Entries)
            .FirstOrDefaultAsync();

        if (voucher == null) return null;

        await ValidateRequestAsync(voucherType, request);

        voucher.Date = request.Date;
        voucher.Description = VoucherHelpers.ToNullIfWhiteSpace(request.Description);
        voucher.RatesAdded = true;
        voucher.UpdatedAt = DateTime.UtcNow;

        _db.JournalEntries.RemoveRange(voucher.Entries);
        voucher.Entries.Clear();

        var totalAmount = VoucherHelpers.Round2(request.Lines.Sum(line => line.Amount));
        voucher.Entries.Add(BuildPartyEntry(voucherType, request.PartyAccountId, voucher.VoucherNumber, totalAmount, isEdited: true));

        foreach (var (line, index) in request.Lines
                     .OrderBy(line => line.SortOrder)
                     .Select((line, index) => (line, index)))
        {
            voucher.Entries.Add(BuildAccountEntry(voucherType, line, index + 1, isEdited: true));
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(voucher.Id, voucherType);
    }

    private async Task ValidateRequestAsync(VoucherType voucherType, CreateCashVoucherRequest request)
    {
        if (request.PartyAccountId <= 0)
            throw new RequestValidationException("Select a valid party account.");

        if (request.Lines.Count == 0)
            throw new RequestValidationException("Add at least one cash or bank line.");

        var accountIds = request.Lines
            .Select(line => line.AccountId)
            .Append(request.PartyAccountId)
            .Distinct()
            .ToList();

        var accountsById = await _db.Accounts
            .Include(a => a.PartyDetail)
            .Include(a => a.BankDetail)
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        if (accountsById.Count != accountIds.Count)
            throw new RequestValidationException("One or more selected accounts are invalid.");

        var partyAccount = accountsById[request.PartyAccountId];
        ValidatePartyAccount(voucherType, partyAccount);

        foreach (var line in request.Lines)
        {
            if (line.Amount <= 0)
                throw new RequestValidationException("Each line amount must be greater than zero.");

            if (line.AccountId == request.PartyAccountId)
                throw new RequestValidationException("Cash or bank account cannot be the same as the selected party.");

            var account = accountsById[line.AccountId];
            if (account.AccountType != AccountType.Account)
                throw new RequestValidationException("Only cash and bank accounts are allowed in receipt and payment vouchers.");
        }

        var totalAmount = VoucherHelpers.Round2(request.Lines.Sum(line => line.Amount));
        if (totalAmount <= 0)
            throw new RequestValidationException("Voucher total must be greater than zero.");
    }

    private static void ValidatePartyAccount(VoucherType voucherType, Account account)
    {
        if (account.AccountType != AccountType.Party || account.PartyDetail == null)
            throw new RequestValidationException("Selected party account is invalid.");

        var isValid = voucherType switch
        {
            VoucherType.ReceiptVoucher => account.PartyDetail.PartyRole is PartyRole.Customer or PartyRole.Both,
            VoucherType.PaymentVoucher => account.PartyDetail.PartyRole is PartyRole.Vendor or PartyRole.Both,
            _ => false
        };

        if (!isValid)
        {
            throw new RequestValidationException(voucherType == VoucherType.ReceiptVoucher
                ? "Receipt voucher requires a customer account."
                : "Payment voucher requires a vendor account.");
        }
    }

    private static JournalVoucher BuildVoucher(
        VoucherType voucherType,
        string voucherNumber,
        CreateCashVoucherRequest request,
        int userId,
        bool isEdited)
    {
        var totalAmount = VoucherHelpers.Round2(request.Lines.Sum(line => line.Amount));
        var voucher = new JournalVoucher
        {
            VoucherNumber = voucherNumber,
            Date = request.Date,
            VoucherType = voucherType,
            Description = VoucherHelpers.ToNullIfWhiteSpace(request.Description),
            RatesAdded = true,
            CreatedBy = userId
        };

        voucher.Entries.Add(BuildPartyEntry(voucherType, request.PartyAccountId, voucherNumber, totalAmount, isEdited));

        foreach (var (line, index) in request.Lines
                     .OrderBy(line => line.SortOrder)
                     .Select((line, index) => (line, index)))
        {
            voucher.Entries.Add(BuildAccountEntry(voucherType, line, index + 1, isEdited));
        }

        return voucher;
    }

    private static JournalEntry BuildPartyEntry(
        VoucherType voucherType,
        int partyAccountId,
        string voucherNumber,
        decimal totalAmount,
        bool isEdited = false) =>
        new()
        {
            AccountId = partyAccountId,
            Description = voucherType == VoucherType.ReceiptVoucher
                ? $"Receipt from party - {voucherNumber}"
                : $"Payment to party - {voucherNumber}",
            Debit = voucherType == VoucherType.PaymentVoucher ? totalAmount : 0,
            Credit = voucherType == VoucherType.ReceiptVoucher ? totalAmount : 0,
            IsEdited = isEdited,
            SortOrder = 0
        };

    private static JournalEntry BuildAccountEntry(
        VoucherType voucherType,
        CreateCashVoucherLineRequest line,
        int sortOrder,
        bool isEdited = false) =>
        new()
        {
            AccountId = line.AccountId,
            Description = VoucherHelpers.ToNullIfWhiteSpace(line.Description),
            Debit = voucherType == VoucherType.ReceiptVoucher ? VoucherHelpers.Round2(line.Amount) : 0,
            Credit = voucherType == VoucherType.PaymentVoucher ? VoucherHelpers.Round2(line.Amount) : 0,
            IsEdited = isEdited,
            SortOrder = sortOrder
        };

    private static CashVoucherDto MapToDto(JournalVoucher voucher)
    {
        var partyEntry = voucher.Entries
            .OrderBy(entry => entry.SortOrder)
            .First(entry => IsPartyEntry(voucher.VoucherType, entry));

        var lineEntries = voucher.Entries
            .Where(entry => entry.Id != partyEntry.Id)
            .OrderBy(entry => entry.SortOrder)
            .ToList();

        return new CashVoucherDto
        {
            Id = voucher.Id,
            VoucherNumber = voucher.VoucherNumber,
            VoucherType = voucher.VoucherType.ToString(),
            Date = voucher.Date,
            Description = voucher.Description,
            RatesAdded = voucher.RatesAdded,
            PartyAccountId = partyEntry.AccountId,
            PartyAccountName = partyEntry.Account?.Name ?? string.Empty,
            TotalAmount = voucher.VoucherType == VoucherType.ReceiptVoucher
                ? lineEntries.Sum(entry => entry.Debit)
                : lineEntries.Sum(entry => entry.Credit),
            Lines = lineEntries.Select(entry => new CashVoucherLineDto
            {
                Id = entry.Id,
                AccountId = entry.AccountId,
                AccountName = entry.Account?.Name ?? string.Empty,
                Description = entry.Description,
                Amount = voucher.VoucherType == VoucherType.ReceiptVoucher ? entry.Debit : entry.Credit,
                IsEdited = entry.IsEdited,
                SortOrder = entry.SortOrder
            }).ToList()
        };
    }

    private static bool IsPartyEntry(VoucherType voucherType, JournalEntry entry) =>
        voucherType switch
        {
            VoucherType.ReceiptVoucher => entry.Credit > 0,
            VoucherType.PaymentVoucher => entry.Debit > 0,
            _ => false
        };

}
