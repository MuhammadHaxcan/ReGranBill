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
        return await GetVoucherAsync(j => j.Id == id);
    }

    public async Task<PurchaseVoucherDto?> GetByNumberAsync(string voucherNumber)
    {
        var normalized = voucherNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return await GetVoucherAsync(j => j.VoucherNumber == normalized);
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
        var voucherDate = request.Date;

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
        await SyncPurchaseInventoryAsync(purchaseJv, validation.VendorAccount.Id);
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
        var productEntries = purchaseJv.Entries
            .Where(e => e.SortOrder > 0)
            .OrderBy(e => e.SortOrder)
            .ToList();
        var inventoryByEntryId = await LoadPurchaseInventoryStatesAsync(purchaseJv.Id);
        ValidatePurchaseLineIds(request, productEntries);
        EnsurePurchaseStockChangesAllowed(purchaseJv, request, productEntries, inventoryByEntryId);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        purchaseJv.Date = request.Date;
        purchaseJv.VehicleNumber = request.VehicleNumber;
        purchaseJv.Description = VoucherHelpers.ResolveDescription(request.Description, "Purchase by", validation.VendorAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts);
        purchaseJv.RatesAdded = request.Lines.All(l => l.Rate > 0);
        purchaseJv.UpdatedAt = DateTime.UtcNow;

        var vendorEntry = purchaseJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        if (vendorEntry == null)
            throw new ConflictException("Purchase voucher is missing the vendor entry.");

        var requestLinesById = request.Lines
            .Where(line => line.LineId.HasValue)
            .ToDictionary(line => line.LineId!.Value);

        foreach (var existingEntry in productEntries)
        {
            if (requestLinesById.ContainsKey(existingEntry.Id))
                continue;

            // Detach the lot from this entry before the entry is deleted, otherwise the
            // FK_inventory_lots_journal_entries_SourceEntryId (Restrict) constraint will trip
            // because EF Core does not auto-null nullable FKs on principal delete.
            if (inventoryByEntryId.TryGetValue(existingEntry.Id, out var orphanState))
            {
                _db.InventoryVoucherLinks.Remove(orphanState.Link);
                _db.InventoryTransactions.Remove(orphanState.Transaction);
                orphanState.Lot.Status = InventoryLotStatus.Voided;
                orphanState.Lot.SourceEntryId = null;
                orphanState.Lot.UpdatedAt = DateTime.UtcNow;
            }

            _db.JournalEntries.Remove(existingEntry);
            purchaseJv.Entries.Remove(existingEntry);
        }

        var totalProductAmount = 0m;
        var sortOrder = 0;

        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = validation.ProductAccounts[line.ProductId];
            var lineAmount = CalculatePurchaseLineAmount(line.TotalWeightKg, line.Rate);
            totalProductAmount += lineAmount;

            var entry = line.LineId.HasValue
                ? productEntries.First(x => x.Id == line.LineId.Value)
                : new JournalEntry();

            entry.AccountId = line.ProductId;
            entry.Description = $"{product.Name} - {line.Qty} bags / {line.TotalWeightKg:0.##} kg";
            entry.Debit = lineAmount;
            entry.Credit = 0;
            entry.Qty = line.Qty;
            entry.ActualWeightKg = line.TotalWeightKg;
            entry.Rbp = "Yes";
            entry.Rate = line.Rate > 0 ? line.Rate : null;
            entry.IsEdited = true;
            entry.SortOrder = ++sortOrder;

            if (line.LineId is null)
                purchaseJv.Entries.Add(entry);
        }

        vendorEntry.AccountId = validation.VendorAccount.Id;
        vendorEntry.Description = $"Purchase from vendor - {purchaseJv.VoucherNumber}";
        vendorEntry.Debit = 0;
        vendorEntry.Credit = totalProductAmount;
        vendorEntry.IsEdited = true;
        vendorEntry.SortOrder = 0;

        await RebuildCartageVoucherAsync(purchaseJv, validation.VendorAccount.Id, validation.TransporterAccount, request.Cartage);

        await _db.SaveChangesAsync();
        await SyncPurchaseInventoryAsync(purchaseJv, validation.VendorAccount.Id);
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

        var inventoryByEntryId = await LoadPurchaseInventoryStatesAsync(purchaseJv.Id);
        if (inventoryByEntryId.Values.Any(x => x.HasNonReturnConsumption))
            throw new ConflictException("This purchase voucher rate cannot be changed because one or more linked lots have already been consumed in washing or production.");

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
        var vendorId = purchaseJv.Entries.FirstOrDefault(e => e.SortOrder == 0)?.AccountId ?? 0;
        await SyncPurchaseInventoryAsync(purchaseJv, vendorId);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var purchaseJv = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.PurchaseVoucher);

        if (purchaseJv == null) return (false, null);

        if (await HasPurchaseDownstreamConsumptionAsync(purchaseJv.Id))
            return (false, "Cannot delete a purchase voucher whose inventory lot has already been consumed.");

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

        await RemoveVoucherInventoryAsync(purchaseJv.Id, VoucherType.PurchaseVoucher);
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
                || !HasValidPurchaseInventorySetup(productAccount))
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

    private static bool HasValidPurchaseInventorySetup(Account account) =>
        account.AccountType == AccountType.UnwashedMaterial || account.ProductDetail != null;

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

    private sealed record PurchaseInventoryState(
        InventoryLot Lot,
        InventoryTransaction Transaction,
        InventoryVoucherLink Link,
        bool HasDownstreamConsumption,
        bool HasNonReturnConsumption,
        int DownstreamQtyUsed,
        decimal DownstreamWeightKgUsed);

    private async Task<bool> HasPurchaseDownstreamConsumptionAsync(int voucherId)
    {
        var lotIds = await _db.InventoryVoucherLinks
            .Where(x => x.VoucherId == voucherId && x.VoucherType == VoucherType.PurchaseVoucher)
            .Select(x => x.LotId)
            .Distinct()
            .ToListAsync();

        if (lotIds.Count == 0)
            return false;

        return await _db.InventoryTransactions
            .AnyAsync(x => lotIds.Contains(x.LotId) && x.TransactionType != InventoryTransactionType.PurchaseIn);
    }

    private static void ValidatePurchaseLineIds(CreatePurchaseVoucherRequest request, IReadOnlyCollection<JournalEntry> productEntries)
    {
        var duplicateLineId = request.Lines
            .Where(x => x.LineId.HasValue)
            .GroupBy(x => x.LineId!.Value)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateLineId != null)
            throw new RequestValidationException("Purchase request contains duplicate line references.");

        var existingEntryIds = productEntries.Select(x => x.Id).ToHashSet();
        if (request.Lines.Any(x => x.LineId.HasValue && !existingEntryIds.Contains(x.LineId.Value)))
            throw new RequestValidationException("Purchase request contains an invalid existing line reference.");
    }

    private static void EnsurePurchaseStockChangesAllowed(
        JournalVoucher purchaseJv,
        CreatePurchaseVoucherRequest request,
        IReadOnlyCollection<JournalEntry> productEntries,
        IReadOnlyDictionary<int, PurchaseInventoryState> inventoryByEntryId)
    {
        var consumedStates = inventoryByEntryId.Values.Where(x => x.HasDownstreamConsumption).ToList();
        if (consumedStates.Count == 0)
            return;

        var requestLinesById = request.Lines
            .Where(x => x.LineId.HasValue)
            .ToDictionary(x => x.LineId!.Value);
        var currentVendorId = purchaseJv.Entries.FirstOrDefault(e => e.SortOrder == 0)?.AccountId ?? 0;

        if (purchaseJv.Date != request.Date)
            throw new ConflictException("Purchase date cannot be changed after one or more lots have been consumed.");
        if (request.VendorId != currentVendorId)
            throw new ConflictException("Vendor cannot be changed after one or more lots have been consumed.");

        foreach (var entry in productEntries)
        {
            if (!inventoryByEntryId.TryGetValue(entry.Id, out var state) || !state.HasDownstreamConsumption)
                continue;

            if (!requestLinesById.TryGetValue(entry.Id, out var requestLine))
            {
                throw new ConflictException(
                    $"Cannot delete purchase line {entry.SortOrder} because its lot has already been used in returns, washing, or production.");
            }

            var currentWeight = VoucherHelpers.Round2(entry.ActualWeightKg ?? 0m);
            var requestedWeight = VoucherHelpers.Round2(requestLine.TotalWeightKg);
            var currentRate = VoucherHelpers.Round2(entry.Rate ?? 0m);
            var requestedRate = VoucherHelpers.Round2(requestLine.Rate);
            var requestedQty = requestLine.Qty;

            if (requestLine.ProductId != entry.AccountId)
            {
                throw new ConflictException(
                    $"Cannot change the product on purchase line {entry.SortOrder} because its lot has already been used.");
            }

            if (requestedQty < state.DownstreamQtyUsed)
            {
                throw new ConflictException(
                    $"Purchase line {entry.SortOrder} qty cannot be less than {state.DownstreamQtyUsed} because that quantity is already used downstream.");
            }

            if (requestedWeight + 0.01m < state.DownstreamWeightKgUsed)
            {
                throw new ConflictException(
                    $"Purchase line {entry.SortOrder} total kg cannot be less than {state.DownstreamWeightKgUsed:0.##} because that weight is already used downstream.");
            }

            if (state.HasNonReturnConsumption && requestedRate != currentRate)
            {
                throw new ConflictException(
                    $"Cannot change the rate on purchase line {entry.SortOrder} because its lot has already been consumed in washing or production.");
            }

            if (state.HasNonReturnConsumption
                && requestedQty == (entry.Qty ?? 0)
                && requestedWeight == currentWeight
                && requestedRate == currentRate)
            {
                continue;
            }
        }
    }

    private async Task RemoveVoucherInventoryAsync(int voucherId, VoucherType voucherType)
    {
        var links = await _db.InventoryVoucherLinks
            .Where(x => x.VoucherId == voucherId && x.VoucherType == voucherType)
            .ToListAsync();

        if (links.Count == 0)
            return;

        var transactionIds = links.Select(x => x.TransactionId).Distinct().ToList();
        var lotIds = links.Select(x => x.LotId).Distinct().ToList();

        var transactions = await _db.InventoryTransactions
            .Where(x => transactionIds.Contains(x.Id))
            .ToListAsync();
        var lots = await _db.InventoryLots
            .Where(x => lotIds.Contains(x.Id))
            .ToListAsync();

        _db.InventoryVoucherLinks.RemoveRange(links);
        _db.InventoryTransactions.RemoveRange(transactions);

        foreach (var lot in lots)
        {
            lot.Status = InventoryLotStatus.Voided;
            lot.SourceEntryId = null;
            lot.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<Dictionary<int, PurchaseInventoryState>> LoadPurchaseInventoryStatesAsync(int voucherId)
    {
        var links = await _db.InventoryVoucherLinks
            .Where(x => x.VoucherId == voucherId && x.VoucherType == VoucherType.PurchaseVoucher)
            .Include(x => x.Lot)
            .Include(x => x.Transaction)
            .ToListAsync();

        if (links.Count == 0)
            return [];

        var activeLinks = links
            .Where(x => x.Lot.SourceEntryId.HasValue && x.Lot.Status != InventoryLotStatus.Voided)
            .ToList();
        var lotIds = activeLinks.Select(x => x.LotId).Distinct().ToList();
        var downstreamRows = await _db.InventoryTransactions
            .Where(x => lotIds.Contains(x.LotId) && x.TransactionType != InventoryTransactionType.PurchaseIn)
            .GroupBy(x => x.LotId)
            .Select(g => new
            {
                LotId = g.Key,
                HasDownstream = g.Any(),
                HasNonReturnConsumption = g.Any(x => x.TransactionType != InventoryTransactionType.PurchaseReturnOut),
                DownstreamQtyUsed = g.Sum(x => x.QtyDelta.HasValue && x.QtyDelta.Value < 0 ? -x.QtyDelta.Value : 0),
                DownstreamWeightKgUsed = VoucherHelpers.Round2(g.Sum(x => x.WeightKgDelta < 0 ? -x.WeightKgDelta : 0m))
            })
            .ToListAsync();
        var downstreamByLotId = downstreamRows.ToDictionary(x => x.LotId);

        return activeLinks.ToDictionary(
            x => x.Lot.SourceEntryId!.Value,
            x =>
            {
                var downstream = downstreamByLotId.GetValueOrDefault(x.LotId);
                return new PurchaseInventoryState(
                    x.Lot,
                    x.Transaction,
                    x,
                    downstream?.HasDownstream ?? false,
                    downstream?.HasNonReturnConsumption ?? false,
                    downstream?.DownstreamQtyUsed ?? 0,
                    downstream?.DownstreamWeightKgUsed ?? 0m);
            });
    }

    private async Task SyncPurchaseInventoryAsync(JournalVoucher purchaseJv, int vendorId)
    {
        var productEntries = purchaseJv.Entries
            .Where(x => x.SortOrder > 0)
            .OrderBy(x => x.SortOrder)
            .ToList();
        var inventoryByEntryId = await LoadPurchaseInventoryStatesAsync(purchaseJv.Id);
        var currentEntryIds = productEntries.Select(x => x.Id).ToHashSet();

        foreach (var pair in inventoryByEntryId.Where(x => !currentEntryIds.Contains(x.Key)).ToList())
        {
            if (pair.Value.HasDownstreamConsumption)
            {
                throw new ConflictException(
                    $"Cannot remove purchase line {pair.Value.Transaction.VoucherLineKey} because its lot has already been consumed.");
            }

            _db.InventoryVoucherLinks.Remove(pair.Value.Link);
            _db.InventoryTransactions.Remove(pair.Value.Transaction);
            pair.Value.Lot.Status = InventoryLotStatus.Voided;
            pair.Value.Lot.SourceEntryId = null;
            pair.Value.Lot.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var entry in productEntries)
        {
            if (inventoryByEntryId.TryGetValue(entry.Id, out var existingState))
            {
                if (entry.Qty < existingState.DownstreamQtyUsed)
                {
                    throw new ConflictException(
                        $"Purchase line {entry.SortOrder} qty cannot be less than {existingState.DownstreamQtyUsed} because that quantity is already used downstream.");
                }

                var entryWeight = VoucherHelpers.Round2(entry.ActualWeightKg ?? 0m);
                if (entryWeight + 0.01m < existingState.DownstreamWeightKgUsed)
                {
                    throw new ConflictException(
                        $"Purchase line {entry.SortOrder} total kg cannot be less than {existingState.DownstreamWeightKgUsed:0.##} because that weight is already used downstream.");
                }

                existingState.Lot.LotNumber = $"{purchaseJv.VoucherNumber}-L{entry.SortOrder:00}";
                existingState.Lot.ProductAccountId = entry.AccountId;
                existingState.Lot.VendorAccountId = vendorId;
                existingState.Lot.SourceVoucherId = purchaseJv.Id;
                existingState.Lot.SourceVoucherType = VoucherType.PurchaseVoucher;
                existingState.Lot.SourceEntryId = entry.Id;
                existingState.Lot.OriginalQty = entry.Qty;
                existingState.Lot.OriginalWeightKg = entry.ActualWeightKg ?? 0m;
                existingState.Lot.BaseRate = entry.Rate ?? 0m;
                existingState.Lot.Status = InventoryLotStatus.Open;
                existingState.Lot.UpdatedAt = DateTime.UtcNow;

                existingState.Transaction.VoucherLineKey = $"purchase-line-{entry.SortOrder}";
                existingState.Transaction.ProductAccountId = entry.AccountId;
                existingState.Transaction.QtyDelta = entry.Qty;
                existingState.Transaction.WeightKgDelta = entry.ActualWeightKg ?? 0m;
                existingState.Transaction.Rate = entry.Rate ?? 0m;
                existingState.Transaction.ValueDelta = entry.Debit;
                existingState.Transaction.TransactionDate = purchaseJv.Date;
                existingState.Transaction.Notes = entry.Description;

                existingState.Link.VoucherLineKey = existingState.Transaction.VoucherLineKey;
                continue;
            }

            var lot = new InventoryLot
            {
                LotNumber = $"{purchaseJv.VoucherNumber}-L{entry.SortOrder:00}",
                ProductAccountId = entry.AccountId,
                VendorAccountId = vendorId,
                SourceVoucherId = purchaseJv.Id,
                SourceVoucherType = VoucherType.PurchaseVoucher,
                SourceEntryId = entry.Id,
                ParentLotId = null,
                OriginalQty = entry.Qty,
                OriginalWeightKg = entry.ActualWeightKg ?? 0m,
                BaseRate = entry.Rate ?? 0m,
                Status = InventoryLotStatus.Open
            };
            _db.InventoryLots.Add(lot);
            await _db.SaveChangesAsync();

            var transaction = new InventoryTransaction
            {
                VoucherId = purchaseJv.Id,
                VoucherType = VoucherType.PurchaseVoucher,
                VoucherLineKey = $"purchase-line-{entry.SortOrder}",
                TransactionType = InventoryTransactionType.PurchaseIn,
                ProductAccountId = entry.AccountId,
                LotId = lot.Id,
                QtyDelta = entry.Qty,
                WeightKgDelta = entry.ActualWeightKg ?? 0m,
                Rate = entry.Rate ?? 0m,
                ValueDelta = entry.Debit,
                TransactionDate = purchaseJv.Date,
                Notes = entry.Description
            };
            _db.InventoryTransactions.Add(transaction);
            await _db.SaveChangesAsync();

            _db.InventoryVoucherLinks.Add(new InventoryVoucherLink
            {
                VoucherId = purchaseJv.Id,
                VoucherType = VoucherType.PurchaseVoucher,
                VoucherLineKey = $"purchase-line-{entry.SortOrder}",
                LotId = lot.Id,
                TransactionId = transaction.Id
            });
        }
    }

    private async Task<PurchaseVoucherDto?> GetVoucherAsync(System.Linq.Expressions.Expression<Func<JournalVoucher, bool>> predicate)
    {
        var purchaseJv = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.PurchaseVoucher)
            .Where(predicate)
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
}
