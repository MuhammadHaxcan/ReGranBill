using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Common;
using ReGranBill.Server.DTOs.PurchaseVouchers;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class PurchaseVoucherService : IPurchaseVoucherService
{
    private readonly AppDbContext _db;
    private readonly IVoucherNumberService _voucherNumberService;

    public PurchaseVoucherService(AppDbContext db, IVoucherNumberService voucherNumberService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
    }

    public async Task<List<PurchaseVoucherDto>> GetAllAsync()
    {
        var purchaseJvs = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.PurchaseVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        var purchaseJvIds = purchaseJvs.Select(j => j.Id).ToList();

        var refs = await _db.JournalVoucherReferences
            .Where(r => purchaseJvIds.Contains(r.MainVoucherId))
            .Include(r => r.ReferenceVoucher).ThenInclude(v => v.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .ToListAsync();

        var cartageMap = refs.ToDictionary(r => r.MainVoucherId, r => r.ReferenceVoucher);

        return purchaseJvs.Select(jv => MapToDto(jv, cartageMap.GetValueOrDefault(jv.Id))).ToList();
    }

    public async Task<PurchaseVoucherDto?> GetByIdAsync(int id)
    {
        var purchaseJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.PurchaseVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .FirstOrDefaultAsync();

        if (purchaseJv == null) return null;

        var cartageRef = await _db.JournalVoucherReferences
            .Where(r => r.MainVoucherId == purchaseJv.Id)
            .Include(r => r.ReferenceVoucher).ThenInclude(v => v.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .FirstOrDefaultAsync();

        return MapToDto(purchaseJv, cartageRef?.ReferenceVoucher);
    }

    public Task<string> GetNextNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.PurchaseVoucher, "PV-");

    public async Task<List<LatestProductRateDto>> GetLatestRatesAsync(IReadOnlyCollection<int> productIds)
    {
        var ids = productIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
            return [];

        var entries = await _db.JournalEntries
            .AsNoTracking()
            .Where(e =>
                e.SortOrder > 0 &&
                ids.Contains(e.AccountId) &&
                e.Rate.HasValue &&
                e.Rate.Value > 0 &&
                e.JournalVoucher.VoucherType == VoucherType.PurchaseVoucher)
            .Select(e => new
            {
                e.AccountId,
                Rate = e.Rate!.Value,
                e.Id,
                VoucherId = e.VoucherId,
                VoucherNumber = e.JournalVoucher.VoucherNumber,
                VoucherDate = e.JournalVoucher.Date
            })
            .OrderByDescending(e => e.VoucherDate)
            .ThenByDescending(e => e.VoucherId)
            .ThenByDescending(e => e.Id)
            .ToListAsync();

        return entries
            .GroupBy(e => e.AccountId)
            .Select(group => group.First())
            .Select(item => new LatestProductRateDto
            {
                ProductId = item.AccountId,
                Rate = item.Rate,
                SourceVoucherNumber = item.VoucherNumber,
                SourceDate = item.VoucherDate
            })
            .ToList();
    }

    public async Task<PurchaseVoucherDto> CreateAsync(CreatePurchaseVoucherRequest request, int userId)
    {
        var validation = await ValidateRequestAsync(request);
        var voucherDate = VoucherHelpers.NormalizeToUtc(request.Date);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var voucherNumber = await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.PurchaseVoucher, "PV-");
        var cartageNumber = validation.TransporterAccount == null
            ? null
            : await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.CartageVoucher, "CV-");

        var purchaseJv = new JournalVoucher
        {
            VoucherNumber = voucherNumber,
            Date = voucherDate,
            VoucherType = VoucherType.PurchaseVoucher,
            VehicleNumber = request.VehicleNumber,
            Description = VoucherHelpers.ResolveDescription(request.Description, "Purchase by", validation.VendorAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts),
            RatesAdded = request.Lines.All(l => l.Rate > 0),
            CreatedBy = userId,
        };

        var totalProductAmount = 0m;
        var sortOrder = 0;

        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = validation.ProductAccounts[line.ProductId];
            var lineAmount = CalculatePurchaseLineAmount(line.TotalWeightKg, line.Rate);
            totalProductAmount += lineAmount;

            purchaseJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product.Name} - {line.Qty} bags / {line.TotalWeightKg:0.##} kg",
                Debit = lineAmount,
                Credit = 0,
                Qty = line.Qty,
                ActualWeightKg = line.TotalWeightKg,
                Rbp = "Yes",
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = false,
                SortOrder = ++sortOrder
            });
        }

        purchaseJv.Entries.Add(new JournalEntry
        {
            AccountId = validation.VendorAccount.Id,
            Description = $"Purchase from vendor - {voucherNumber}",
            Debit = 0,
            Credit = totalProductAmount,
            IsEdited = false,
            SortOrder = 0
        });

        _db.JournalVouchers.Add(purchaseJv);

        if (validation.TransporterAccount != null && request.Cartage != null && cartageNumber != null)
        {
            var cartageJv = VoucherHelpers.BuildCartageVoucher(
                cartageNumber,
                voucherDate,
                voucherNumber,
                userId,
                validation.VendorAccount.Id,
                validation.TransporterAccount.Id,
                request.Cartage.Amount,
                false);

            _db.JournalVouchers.Add(cartageJv);
            _db.JournalVoucherReferences.Add(new JournalVoucherReference
            {
                MainVoucher = purchaseJv,
                ReferenceVoucher = cartageJv
            });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(purchaseJv.Id))!;
    }

    public async Task<PurchaseVoucherDto?> UpdateAsync(int id, CreatePurchaseVoucherRequest request)
    {
        var purchaseJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.PurchaseVoucher)
            .Include(j => j.Entries)
            .FirstOrDefaultAsync();

        if (purchaseJv == null) return null;

        var validation = await ValidateRequestAsync(request);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        purchaseJv.Date = VoucherHelpers.NormalizeToUtc(request.Date);
        purchaseJv.VehicleNumber = request.VehicleNumber;
        purchaseJv.Description = VoucherHelpers.ResolveDescription(request.Description, "Purchase by", validation.VendorAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts);
        purchaseJv.RatesAdded = request.Lines.All(l => l.Rate > 0);
        purchaseJv.UpdatedAt = DateTime.UtcNow;

        _db.JournalEntries.RemoveRange(purchaseJv.Entries);
        purchaseJv.Entries.Clear();

        var totalProductAmount = 0m;
        var sortOrder = 0;

        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = validation.ProductAccounts[line.ProductId];
            var lineAmount = CalculatePurchaseLineAmount(line.TotalWeightKg, line.Rate);
            totalProductAmount += lineAmount;

            purchaseJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product.Name} - {line.Qty} bags / {line.TotalWeightKg:0.##} kg",
                Debit = lineAmount,
                Credit = 0,
                Qty = line.Qty,
                ActualWeightKg = line.TotalWeightKg,
                Rbp = "Yes",
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = true,
                SortOrder = ++sortOrder
            });
        }

        purchaseJv.Entries.Add(new JournalEntry
        {
            AccountId = validation.VendorAccount.Id,
            Description = $"Purchase from vendor - {purchaseJv.VoucherNumber}",
            Debit = 0,
            Credit = totalProductAmount,
            IsEdited = true,
            SortOrder = 0
        });

        await RebuildCartageVoucherAsync(purchaseJv, validation.VendorAccount.Id, validation.TransporterAccount, request.Cartage);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(purchaseJv.Id))!;
    }

    public async Task<bool> UpdateRatesAsync(int id, UpdatePurchaseVoucherRatesRequest request)
    {
        var purchaseJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.PurchaseVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .FirstOrDefaultAsync();

        if (purchaseJv == null) return false;

        foreach (var update in request.Lines)
        {
            var entry = purchaseJv.Entries.FirstOrDefault(e => e.Id == update.EntryId);
            if (entry != null && entry.SortOrder > 0)
            {
                entry.Rate = update.Rate;
                entry.IsEdited = true;
                entry.Debit = CalculatePurchaseLineAmount(entry.ActualWeightKg ?? 0m, update.Rate);
            }
        }

        var vendorEntry = purchaseJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        if (vendorEntry != null)
        {
            vendorEntry.Credit = purchaseJv.Entries.Where(e => e.SortOrder > 0).Sum(e => e.Debit);
            vendorEntry.IsEdited = true;
        }

        purchaseJv.RatesAdded = purchaseJv.Entries.Where(e => e.SortOrder > 0).All(e => e.Rate > 0);
        purchaseJv.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var purchaseJv = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.PurchaseVoucher);

        if (purchaseJv == null) return (false, null);

        if (purchaseJv.RatesAdded)
            return (false, "Cannot delete a rated purchase voucher. Only pending vouchers can be deleted.");

        purchaseJv.IsDeleted = true;
        purchaseJv.UpdatedAt = DateTime.UtcNow;

        var cartageRef = await _db.JournalVoucherReferences
            .Where(r => r.MainVoucherId == purchaseJv.Id)
            .Include(r => r.ReferenceVoucher)
            .FirstOrDefaultAsync();

        if (cartageRef?.ReferenceVoucher != null)
        {
            cartageRef.ReferenceVoucher.IsDeleted = true;
            cartageRef.ReferenceVoucher.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return (true, null);
    }

    private async Task RebuildCartageVoucherAsync(
        JournalVoucher purchaseJv,
        int vendorId,
        Account? transporterAccount,
        CreatePurchaseVoucherCartageRequest? cartageRequest)
    {
        var cartageRef = await _db.JournalVoucherReferences
            .Where(r => r.MainVoucherId == purchaseJv.Id)
            .Include(r => r.ReferenceVoucher).ThenInclude(v => v.Entries)
            .FirstOrDefaultAsync();

        var cartageJv = cartageRef?.ReferenceVoucher;
        var cartageAmount = cartageRequest?.Amount ?? 0;

        if (transporterAccount != null && cartageRequest != null && cartageAmount > 0)
        {
            if (cartageJv == null)
            {
                var cartageNumber = await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.CartageVoucher, "CV-");
                cartageJv = VoucherHelpers.BuildCartageVoucher(
                    cartageNumber,
                    purchaseJv.Date,
                    purchaseJv.VoucherNumber,
                    purchaseJv.CreatedBy,
                    vendorId,
                    transporterAccount.Id,
                    cartageAmount,
                    true);

                _db.JournalVouchers.Add(cartageJv);
                _db.JournalVoucherReferences.Add(new JournalVoucherReference
                {
                    MainVoucherId = purchaseJv.Id,
                    ReferenceVoucher = cartageJv
                });
                return;
            }

            _db.JournalEntries.RemoveRange(cartageJv.Entries);
            cartageJv.Entries.Clear();
            cartageJv.Date = purchaseJv.Date;
            cartageJv.Description = $"Cartage entries for {purchaseJv.VoucherNumber}";
            cartageJv.UpdatedAt = DateTime.UtcNow;

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = vendorId,
                Description = $"Cartage charge - {purchaseJv.VoucherNumber}",
                Debit = cartageAmount,
                Credit = 0,
                IsEdited = true,
                SortOrder = 0
            });

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = transporterAccount.Id,
                Description = $"Cartage for {purchaseJv.VoucherNumber}",
                Debit = 0,
                Credit = cartageAmount,
                IsEdited = true,
                SortOrder = 1
            });
        }
        else if (cartageJv != null)
        {
            if (cartageRef != null)
            {
                _db.JournalVoucherReferences.Remove(cartageRef);
            }

            _db.JournalEntries.RemoveRange(cartageJv.Entries);
            _db.JournalVouchers.Remove(cartageJv);
        }
    }

    private async Task<ValidatedVoucherRequest> ValidateRequestAsync(CreatePurchaseVoucherRequest request)
    {
        if (request.VendorId <= 0)
            throw new RequestValidationException("Select a valid vendor.");

        if (request.Lines.Count == 0)
            throw new RequestValidationException("Add at least one product line.");

        foreach (var line in request.Lines)
        {
            if (line.ProductId <= 0)
                throw new RequestValidationException("Each line must reference a valid product.");

            if (line.Qty <= 0)
                throw new RequestValidationException("Each line must have a quantity greater than zero.");

            if (line.TotalWeightKg <= 0)
                throw new RequestValidationException("Each line must have total weight greater than zero.");

            if (line.Rate < 0)
                throw new RequestValidationException("Rates cannot be negative.");
        }

        if (request.Cartage != null)
        {
            if (request.Cartage.TransporterId <= 0)
                throw new RequestValidationException("Select a valid transporter.");

            if (request.Cartage.Amount <= 0)
                throw new RequestValidationException("Cartage amount must be greater than zero.");
        }

        var accountIds = request.Lines
            .Select(line => line.ProductId)
            .Append(request.VendorId)
            .Concat(request.Cartage == null ? [] : [request.Cartage.TransporterId])
            .Distinct()
            .ToList();

        var accountsById = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.PartyDetail)
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        if (!accountsById.TryGetValue(request.VendorId, out var vendorAccount)
            || vendorAccount.AccountType != AccountType.Party
            || vendorAccount.PartyDetail == null
            || vendorAccount.PartyDetail.PartyRole is not (PartyRole.Vendor or PartyRole.Both))
        {
            throw new RequestValidationException("Select a valid vendor.");
        }

        var productAccounts = new Dictionary<int, Account>();
        foreach (var productId in request.Lines.Select(line => line.ProductId).Distinct())
        {
            if (!accountsById.TryGetValue(productId, out var productAccount)
                || !IsInventoryAccount(productAccount)
                || productAccount.ProductDetail == null)
            {
                throw new RequestValidationException("One or more selected inventory items are invalid.");
            }

            productAccounts[productId] = productAccount;
        }

        Account? transporterAccount = null;
        if (request.Cartage != null)
        {
            if (!accountsById.TryGetValue(request.Cartage.TransporterId, out transporterAccount)
                || transporterAccount.AccountType != AccountType.Party
                || transporterAccount.PartyDetail == null
                || transporterAccount.PartyDetail.PartyRole is not (PartyRole.Transporter or PartyRole.Both))
            {
                throw new RequestValidationException("Select a valid transporter.");
            }
        }

        return new ValidatedVoucherRequest(productAccounts, vendorAccount, transporterAccount);
    }

    private static bool IsInventoryAccount(Account account) =>
        VoucherHelpers.IsInventoryAccount(account);

    private static decimal CalculatePurchaseLineAmount(decimal totalWeightKg, decimal rate) =>
        VoucherHelpers.Round2(totalWeightKg * rate);

    private static PurchaseVoucherDto MapToDto(JournalVoucher purchaseJv, JournalVoucher? cartageJv)
    {
        var vendorEntry = purchaseJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        var productEntries = purchaseJv.Entries.Where(e => e.SortOrder > 0).OrderBy(e => e.SortOrder).ToList();

        var dto = new PurchaseVoucherDto
        {
            Id = purchaseJv.Id,
            VoucherNumber = purchaseJv.VoucherNumber,
            Date = purchaseJv.Date,
            VendorId = vendorEntry?.AccountId ?? 0,
            VendorName = vendorEntry?.Account?.Name,
            VehicleNumber = purchaseJv.VehicleNumber,
            Description = purchaseJv.Description,
            VoucherType = purchaseJv.VoucherType.ToString(),
            RatesAdded = purchaseJv.RatesAdded,
            Lines = productEntries.Select(e => new PurchaseVoucherLineDto
            {
                Id = e.Id,
                ProductId = e.AccountId,
                ProductName = e.Account?.Name,
                Packing = e.Account?.ProductDetail?.Packing,
                PackingWeightKg = e.Account?.ProductDetail?.PackingWeightKg ?? 0,
                Qty = e.Qty ?? 0,
                TotalWeightKg = e.ActualWeightKg ?? 0,
                AvgWeightPerBagKg = (e.Qty ?? 0) > 0
                    ? VoucherHelpers.Round2((e.ActualWeightKg ?? 0m) / (e.Qty ?? 0))
                    : 0,
                Rate = e.Rate ?? 0,
                SortOrder = e.SortOrder
            }).ToList(),
        };

        if (cartageJv != null)
        {
            var cartageCredit = cartageJv.Entries.FirstOrDefault(e => e.SortOrder == 1 && e.Credit > 0);
            if (cartageCredit != null)
            {
                dto.Cartage = new PurchaseVoucherCartageDto
                {
                    TransporterId = cartageCredit.AccountId,
                    TransporterName = cartageCredit.Account?.Name,
                    City = cartageCredit.Account?.PartyDetail?.City,
                    Amount = cartageCredit.Credit
                };
            }
        }

        dto.JournalVouchers = [MapJournalVoucherSummary(purchaseJv)];
        if (cartageJv != null)
        {
            dto.JournalVouchers.Add(MapJournalVoucherSummary(cartageJv));
        }

        return dto;
    }

    private static PurchaseVoucherJournalSummaryDto MapJournalVoucherSummary(JournalVoucher voucher) => new()
    {
        Id = voucher.Id,
        VoucherNumber = voucher.VoucherNumber,
        VoucherType = voucher.VoucherType.ToString(),
        RatesAdded = voucher.RatesAdded,
        TotalDebit = voucher.Entries.Sum(e => e.Debit),
        TotalCredit = voucher.Entries.Sum(e => e.Credit),
        Entries = voucher.Entries.OrderBy(e => e.SortOrder).Select(e => new PurchaseVoucherJournalEntryDto
        {
            Id = e.Id,
            AccountId = e.AccountId,
            AccountName = e.Account?.Name,
            Description = e.Description,
            Debit = e.Debit,
            Credit = e.Credit,
            Qty = e.Qty,
            Rbp = e.Rbp,
            Rate = e.Rate,
            IsEdited = e.IsEdited,
            SortOrder = e.SortOrder
        }).ToList()
    };

    private sealed record ValidatedVoucherRequest(
        IReadOnlyDictionary<int, Account> ProductAccounts,
        Account VendorAccount,
        Account? TransporterAccount);
}
