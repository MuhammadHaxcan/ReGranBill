using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Services;

public class PurchaseVoucherService : IPurchaseVoucherService
{
    private readonly AppDbContext _db;

    public PurchaseVoucherService(AppDbContext db) => _db = db;

    public async Task<List<DeliveryChallanDto>> GetAllAsync()
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

    public async Task<DeliveryChallanDto?> GetByIdAsync(int id)
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

    public async Task<string> GetNextNumberAsync()
    {
        var lastNumber = await GetLastPvNumberAsync();
        return $"PV-{lastNumber + 1:D4}";
    }

    private static DateTime NormalizeToUtc(DateTime date)
    {
        return date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
        };
    }

    private async Task<int> GetLastPvNumberAsync()
    {
        var pvNumbers = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.PurchaseVoucher && j.VoucherNumber.StartsWith("PV-"))
            .Select(j => j.VoucherNumber)
            .ToListAsync();

        var max = 0;
        foreach (var number in pvNumbers)
        {
            if (number.Length > 3 && int.TryParse(number.AsSpan(3), out var parsed))
                max = Math.Max(max, parsed);
        }
        return max;
    }

    private async Task<int> GetLastCvNumberAsync()
    {
        var cvNumbers = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.CartageVoucher && j.VoucherNumber.StartsWith("CV-"))
            .Select(j => j.VoucherNumber)
            .ToListAsync();

        var max = 0;
        foreach (var number in cvNumbers)
        {
            if (number.Length > 3 && int.TryParse(number.AsSpan(3), out var parsed))
                max = Math.Max(max, parsed);
        }
        return max;
    }

    public async Task<DeliveryChallanDto> CreateAsync(CreateDcRequest request, int userId)
    {
        var lastNumber = await GetLastPvNumberAsync();
        var nextNumber = lastNumber + 1;
        var pvNumber = $"PV-{nextNumber:D4}";

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var productAccounts = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Where(a => productIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var purchaseJv = new JournalVoucher
        {
            VoucherNumber = $"_tp_{Guid.NewGuid().ToString("N")[..16]}",
            Date = NormalizeToUtc(request.Date),
            VoucherType = VoucherType.PurchaseVoucher,
            VehicleNumber = request.VehicleNumber,
            Description = request.Description,
            RatesAdded = false,
            CreatedBy = userId,
        };

        int sortOrder = 0;
        decimal totalProductAmount = 0;

        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = productAccounts.GetValueOrDefault(line.ProductId);
            var weight = product?.ProductDetail?.PackingWeightKg ?? 0;
            var lineAmount = line.Rbp == "Yes"
                ? weight * line.Qty * line.Rate
                : line.Qty * line.Rate;
            totalProductAmount += lineAmount;

            purchaseJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product?.Name} - {line.Qty} bags",
                Debit = lineAmount,
                Credit = 0,
                Qty = line.Qty,
                Rbp = line.Rbp,
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = false,
                SortOrder = ++sortOrder
            });
        }

        purchaseJv.Entries.Add(new JournalEntry
        {
            AccountId = request.CustomerId,
            Description = $"Purchase from vendor - {pvNumber}",
            Debit = 0,
            Credit = totalProductAmount,
            IsEdited = false,
            SortOrder = 0
        });

        purchaseJv.RatesAdded = request.Lines.All(l => l.Rate > 0);
        _db.JournalVouchers.Add(purchaseJv);

        JournalVoucher? cartageJv = null;
        if (request.Cartage != null && request.Cartage.Amount > 0)
        {
            cartageJv = new JournalVoucher
            {
                VoucherNumber = $"_tc_{Guid.NewGuid().ToString("N")[..16]}",
                Date = NormalizeToUtc(request.Date),
                VoucherType = VoucherType.CartageVoucher,
                Description = $"Cartage entries for {pvNumber}",
                RatesAdded = true,
                CreatedBy = userId,
            };

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = request.CustomerId,
                Description = $"Cartage charge - {pvNumber}",
                Debit = request.Cartage.Amount,
                Credit = 0,
                IsEdited = false,
                SortOrder = 0
            });

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = request.Cartage.TransporterId,
                Description = $"Cartage for {pvNumber}",
                Debit = 0,
                Credit = request.Cartage.Amount,
                IsEdited = false,
                SortOrder = 1
            });

            _db.JournalVouchers.Add(cartageJv);
        }

        await _db.SaveChangesAsync();

        purchaseJv.VoucherNumber = pvNumber;
        if (cartageJv != null)
        {
            var lastCvNumber = await GetLastCvNumberAsync();
            cartageJv.VoucherNumber = $"CV-{lastCvNumber + 1:D4}";
            _db.JournalVoucherReferences.Add(new JournalVoucherReference
            {
                MainVoucherId = purchaseJv.Id,
                ReferenceVoucherId = cartageJv.Id
            });
        }

        await _db.SaveChangesAsync();

        return (await GetByIdAsync(purchaseJv.Id))!;
    }

    public async Task<DeliveryChallanDto?> UpdateAsync(int id, CreateDcRequest request)
    {
        var purchaseJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.PurchaseVoucher)
            .Include(j => j.Entries)
            .FirstOrDefaultAsync();

        if (purchaseJv == null) return null;

        purchaseJv.Date = NormalizeToUtc(request.Date);
        purchaseJv.VehicleNumber = request.VehicleNumber;
        purchaseJv.Description = request.Description;
        purchaseJv.UpdatedAt = DateTime.UtcNow;

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var productAccounts = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Where(a => productIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        _db.JournalEntries.RemoveRange(purchaseJv.Entries);
        purchaseJv.Entries.Clear();

        int sortOrder = 0;
        decimal totalProductAmount = 0;

        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = productAccounts.GetValueOrDefault(line.ProductId);
            var weight = product?.ProductDetail?.PackingWeightKg ?? 0;
            var lineAmount = line.Rbp == "Yes"
                ? weight * line.Qty * line.Rate
                : line.Qty * line.Rate;
            totalProductAmount += lineAmount;

            purchaseJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product?.Name} - {line.Qty} bags",
                Debit = lineAmount,
                Credit = 0,
                Qty = line.Qty,
                Rbp = line.Rbp,
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = true,
                SortOrder = ++sortOrder
            });
        }

        purchaseJv.Entries.Add(new JournalEntry
        {
            AccountId = request.CustomerId,
            Description = $"Purchase from vendor - {purchaseJv.VoucherNumber}",
            Debit = 0,
            Credit = totalProductAmount,
            IsEdited = true,
            SortOrder = 0
        });

        purchaseJv.RatesAdded = request.Lines.All(l => l.Rate > 0);

        await RebuildCartageVoucherAsync(purchaseJv, request.CustomerId, request.Cartage);

        await _db.SaveChangesAsync();

        return (await GetByIdAsync(purchaseJv.Id))!;
    }

    public async Task<bool> UpdateRatesAsync(int id, UpdateDcRatesRequest request)
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

                var weight = entry.Account?.ProductDetail?.PackingWeightKg ?? 0;
                entry.Debit = entry.Rbp == "Yes"
                    ? weight * (entry.Qty ?? 0) * update.Rate
                    : (entry.Qty ?? 0) * update.Rate;
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

        // Also soft-delete any associated cartage voucher
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

    private async Task RebuildCartageVoucherAsync(JournalVoucher purchaseJv, int vendorId, CreateDcCartageRequest? cartageRequest)
    {
        var cartageRef = await _db.JournalVoucherReferences
            .Where(r => r.MainVoucherId == purchaseJv.Id)
            .Include(r => r.ReferenceVoucher).ThenInclude(v => v.Entries)
            .FirstOrDefaultAsync();

        var cartageJv = cartageRef?.ReferenceVoucher;
        decimal cartageAmount = cartageRequest?.Amount ?? 0;

        if (cartageRequest != null && cartageAmount > 0)
        {
            if (cartageJv == null)
            {
                cartageJv = new JournalVoucher
                {
                    VoucherNumber = $"_tc_{Guid.NewGuid().ToString("N")[..16]}",
                    Date = purchaseJv.Date,
                    VoucherType = VoucherType.CartageVoucher,
                    Description = $"Cartage entries for {purchaseJv.VoucherNumber}",
                    RatesAdded = true,
                    CreatedBy = purchaseJv.CreatedBy,
                };
                _db.JournalVouchers.Add(cartageJv);
            }
            else
            {
                _db.JournalEntries.RemoveRange(cartageJv.Entries);
                cartageJv.Entries.Clear();
                cartageJv.UpdatedAt = DateTime.UtcNow;
            }

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
                AccountId = cartageRequest.TransporterId,
                Description = $"Cartage for {purchaseJv.VoucherNumber}",
                Debit = 0,
                Credit = cartageAmount,
                IsEdited = true,
                SortOrder = 1
            });

            await _db.SaveChangesAsync();

            if (cartageJv.VoucherNumber.StartsWith("_tc_"))
            {
                var lastCvNumber = await GetLastCvNumberAsync();
                cartageJv.VoucherNumber = $"CV-{lastCvNumber + 1:D4}";

                _db.JournalVoucherReferences.Add(new JournalVoucherReference
                {
                    MainVoucherId = purchaseJv.Id,
                    ReferenceVoucherId = cartageJv.Id
                });

                await _db.SaveChangesAsync();
            }
        }
        else if (cartageJv != null)
        {
            if (cartageRef != null)
                _db.JournalVoucherReferences.Remove(cartageRef);

            _db.JournalEntries.RemoveRange(cartageJv.Entries);
            _db.JournalVouchers.Remove(cartageJv);
        }
    }

    private static DeliveryChallanDto MapToDto(JournalVoucher purchaseJv, JournalVoucher? cartageJv)
    {
        var vendorEntry = purchaseJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        var productEntries = purchaseJv.Entries.Where(e => e.SortOrder > 0).OrderBy(e => e.SortOrder).ToList();

        var dto = new DeliveryChallanDto
        {
            Id = purchaseJv.Id,
            DcNumber = purchaseJv.VoucherNumber,
            Date = purchaseJv.Date,
            CustomerId = vendorEntry?.AccountId ?? 0,
            CustomerName = vendorEntry?.Account?.Name,
            VehicleNumber = purchaseJv.VehicleNumber,
            Description = purchaseJv.Description,
            VoucherType = purchaseJv.VoucherType.ToString(),
            RatesAdded = purchaseJv.RatesAdded,
            Lines = productEntries.Select(e => new DcLineDto
            {
                Id = e.Id,
                ProductId = e.AccountId,
                ProductName = e.Account?.Name,
                Packing = e.Account?.ProductDetail?.Packing,
                PackingWeightKg = e.Account?.ProductDetail?.PackingWeightKg ?? 0,
                Rbp = e.Rbp ?? "Yes",
                Qty = e.Qty ?? 0,
                Rate = e.Rate ?? 0,
                SortOrder = e.SortOrder
            }).ToList(),
        };

        if (cartageJv != null)
        {
            var cartageCredit = cartageJv.Entries.FirstOrDefault(e => e.SortOrder == 1 && e.Credit > 0);
            if (cartageCredit != null)
            {
                dto.Cartage = new DcCartageDto
                {
                    TransporterId = cartageCredit.AccountId,
                    TransporterName = cartageCredit.Account?.Name,
                    City = cartageCredit.Account?.PartyDetail?.City,
                    Amount = cartageCredit.Credit
                };
            }
        }

        var jvSummaries = new List<JournalVoucherSummaryDto>();
        jvSummaries.Add(MapJvSummary(purchaseJv));
        if (cartageJv != null)
            jvSummaries.Add(MapJvSummary(cartageJv));

        dto.JournalVouchers = jvSummaries;

        return dto;
    }

    private static JournalVoucherSummaryDto MapJvSummary(JournalVoucher jv) => new()
    {
        Id = jv.Id,
        VoucherNumber = jv.VoucherNumber,
        VoucherType = jv.VoucherType.ToString(),
        RatesAdded = jv.RatesAdded,
        TotalDebit = jv.Entries.Sum(e => e.Debit),
        TotalCredit = jv.Entries.Sum(e => e.Credit),
        Entries = jv.Entries.OrderBy(e => e.SortOrder).Select(e => new JournalEntryDto
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
}
