using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Services;

public class DeliveryChallanService : IDeliveryChallanService
{
    private readonly AppDbContext _db;

    public DeliveryChallanService(AppDbContext db) => _db = db;

    public async Task<List<DeliveryChallanDto>> GetAllAsync()
    {
        var saleJvs = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.SaleVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        var saleJvIds = saleJvs.Select(j => j.Id).ToList();

        var refs = await _db.JournalVoucherReferences
            .Where(r => saleJvIds.Contains(r.MainVoucherId))
            .Include(r => r.ReferenceVoucher).ThenInclude(v => v.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .ToListAsync();

        var cartageMap = refs.ToDictionary(r => r.MainVoucherId, r => r.ReferenceVoucher);

        return saleJvs.Select(jv => MapToDto(jv, cartageMap.GetValueOrDefault(jv.Id))).ToList();
    }

    public async Task<DeliveryChallanDto?> GetByIdAsync(int id)
    {
        var saleJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.SaleVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .FirstOrDefaultAsync();

        if (saleJv == null) return null;

        var cartageRef = await _db.JournalVoucherReferences
            .Where(r => r.MainVoucherId == saleJv.Id)
            .Include(r => r.ReferenceVoucher).ThenInclude(v => v.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .FirstOrDefaultAsync();

        return MapToDto(saleJv, cartageRef?.ReferenceVoucher);
    }

    public async Task<string> GetNextNumberAsync()
    {
        var lastNumber = await GetLastDcNumberAsync();
        return $"DC-{lastNumber + 1:D4}";
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

    private async Task<int> GetLastDcNumberAsync()
    {
        var dcNumbers = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.SaleVoucher && j.VoucherNumber.StartsWith("DC-"))
            .Select(j => j.VoucherNumber)
            .ToListAsync();

        var max = 0;
        foreach (var number in dcNumbers)
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
        // Derive next number from last SaleVoucher
        var lastNumber = await GetLastDcNumberAsync();
        var nextNumber = lastNumber + 1;
        var dcNumber = $"DC-{nextNumber:D4}";

        // Load product accounts with ProductDetail for weight calculation
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var productAccounts = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Where(a => productIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);
        var customerName = await _db.Accounts
            .Where(a => a.Id == request.CustomerId)
            .Select(a => a.Name)
            .FirstOrDefaultAsync();

        // Build Sale JV
        var saleJv = new JournalVoucher
        {
            VoucherNumber = $"_ts_{Guid.NewGuid().ToString("N")[..16]}",
            Date = NormalizeToUtc(request.Date),
            VoucherType = VoucherType.SaleVoucher,
            VehicleNumber = request.VehicleNumber,
            Description = ResolveDescription(request.Description, "Sale to", customerName, request.Lines, productAccounts),
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

            saleJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product?.Name} - {line.Qty} bags",
                Debit = 0,
                Credit = lineAmount,
                Qty = line.Qty,
                Rbp = line.Rbp,
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = false,
                SortOrder = ++sortOrder
            });
        }

        // DEBIT customer at SortOrder=0
        saleJv.Entries.Add(new JournalEntry
        {
            AccountId = request.CustomerId,
            Description = $"Delivery to customer - {dcNumber}",
            Debit = totalProductAmount,
            Credit = 0,
            IsEdited = false,
            SortOrder = 0
        });

        saleJv.RatesAdded = request.Lines.All(l => l.Rate > 0);
        _db.JournalVouchers.Add(saleJv);

        // Cartage JV (if cartage exists with amount > 0)
        JournalVoucher? cartageJv = null;
        if (request.Cartage != null && request.Cartage.Amount > 0)
        {
            cartageJv = new JournalVoucher
            {
                VoucherNumber = $"_tc_{Guid.NewGuid().ToString("N")[..16]}",
                Date = NormalizeToUtc(request.Date),
                VoucherType = VoucherType.CartageVoucher,
                Description = $"Cartage entries for {dcNumber}",
                RatesAdded = true,
                CreatedBy = userId,
            };

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = request.CustomerId,
                Description = $"Cartage charge - {dcNumber}",
                Debit = request.Cartage.Amount,
                Credit = 0,
                IsEdited = false,
                SortOrder = 0
            });

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = request.Cartage.TransporterId,
                Description = $"Cartage for {dcNumber}",
                Debit = 0,
                Credit = request.Cartage.Amount,
                IsEdited = false,
                SortOrder = 1
            });

            _db.JournalVouchers.Add(cartageJv);
        }

        await _db.SaveChangesAsync();

        // Assign voucher numbers
        saleJv.VoucherNumber = dcNumber;
        if (cartageJv != null)
        {
            var lastCvNumber = await GetLastCvNumberAsync();
            cartageJv.VoucherNumber = $"CV-{lastCvNumber + 1:D4}";
            _db.JournalVoucherReferences.Add(new JournalVoucherReference
            {
                MainVoucherId = saleJv.Id,
                ReferenceVoucherId = cartageJv.Id
            });
        }

        await _db.SaveChangesAsync();

        return (await GetByIdAsync(saleJv.Id))!;
    }

    public async Task<DeliveryChallanDto?> UpdateAsync(int id, CreateDcRequest request)
    {
        var saleJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.SaleVoucher)
            .Include(j => j.Entries)
            .FirstOrDefaultAsync();

        if (saleJv == null) return null;

        // Update header fields
        saleJv.Date = NormalizeToUtc(request.Date);
        saleJv.VehicleNumber = request.VehicleNumber;
        saleJv.Description = request.Description;
        saleJv.UpdatedAt = DateTime.UtcNow;

        // Load product accounts
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var productAccounts = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Where(a => productIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);
        var customerName = await _db.Accounts
            .Where(a => a.Id == request.CustomerId)
            .Select(a => a.Name)
            .FirstOrDefaultAsync();

        // Rebuild all entries
        _db.JournalEntries.RemoveRange(saleJv.Entries);
        saleJv.Entries.Clear();
        saleJv.Description = ResolveDescription(request.Description, "Sale to", customerName, request.Lines, productAccounts);

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

            saleJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product?.Name} - {line.Qty} bags",
                Debit = 0,
                Credit = lineAmount,
                Qty = line.Qty,
                Rbp = line.Rbp,
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = true,
                SortOrder = ++sortOrder
            });
        }

        saleJv.Entries.Add(new JournalEntry
        {
            AccountId = request.CustomerId,
            Description = $"Delivery to customer - {saleJv.VoucherNumber}",
            Debit = totalProductAmount,
            Credit = 0,
            IsEdited = true,
            SortOrder = 0
        });

        saleJv.RatesAdded = request.Lines.All(l => l.Rate > 0);

        // Handle cartage JV
        await RebuildCartageVoucherAsync(saleJv, request.CustomerId, request.Cartage);

        await _db.SaveChangesAsync();

        return (await GetByIdAsync(saleJv.Id))!;
    }

    public async Task<bool> UpdateRatesAsync(int id, UpdateDcRatesRequest request)
    {
        var saleJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.SaleVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .FirstOrDefaultAsync();

        if (saleJv == null) return false;

        // Update rates on product CREDIT entries (SortOrder > 0)
        foreach (var update in request.Lines)
        {
            var entry = saleJv.Entries.FirstOrDefault(e => e.Id == update.EntryId);
            if (entry != null && entry.SortOrder > 0)
            {
                entry.Rate = update.Rate;
                entry.IsEdited = true;

                // Recalculate credit amount
                var weight = entry.Account?.ProductDetail?.PackingWeightKg ?? 0;
                entry.Credit = entry.Rbp == "Yes"
                    ? weight * (entry.Qty ?? 0) * update.Rate
                    : (entry.Qty ?? 0) * update.Rate;
            }
        }

        // Update customer DEBIT total (SortOrder=0)
        var customerEntry = saleJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        if (customerEntry != null)
        {
            customerEntry.Debit = saleJv.Entries.Where(e => e.SortOrder > 0).Sum(e => e.Credit);
            customerEntry.IsEdited = true;
        }

        saleJv.RatesAdded = saleJv.Entries.Where(e => e.SortOrder > 0).All(e => e.Rate > 0);
        saleJv.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var saleJv = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.SaleVoucher);

        if (saleJv == null) return (false, null);

        if (saleJv.RatesAdded)
            return (false, "Cannot delete a rated challan. Only pending challans can be deleted.");

        saleJv.IsDeleted = true;
        saleJv.UpdatedAt = DateTime.UtcNow;

        // Also soft-delete any associated cartage voucher
        var cartageRef = await _db.JournalVoucherReferences
            .Where(r => r.MainVoucherId == saleJv.Id)
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

    private async Task RebuildCartageVoucherAsync(JournalVoucher saleJv, int customerId, CreateDcCartageRequest? cartageRequest)
    {
        var cartageRef = await _db.JournalVoucherReferences
            .Where(r => r.MainVoucherId == saleJv.Id)
            .Include(r => r.ReferenceVoucher).ThenInclude(v => v.Entries)
            .FirstOrDefaultAsync();

        var cartageJv = cartageRef?.ReferenceVoucher;
        decimal cartageAmount = cartageRequest?.Amount ?? 0;

        if (cartageRequest != null && cartageAmount > 0)
        {
            if (cartageJv == null)
            {
                // Create new Cartage JV
                cartageJv = new JournalVoucher
                {
                    VoucherNumber = $"_tc_{Guid.NewGuid().ToString("N")[..16]}",
                    Date = saleJv.Date,
                    VoucherType = VoucherType.CartageVoucher,
                    Description = $"Cartage entries for {saleJv.VoucherNumber}",
                    RatesAdded = true,
                    CreatedBy = saleJv.CreatedBy,
                };
                _db.JournalVouchers.Add(cartageJv);
            }
            else
            {
                // Rebuild existing entries
                _db.JournalEntries.RemoveRange(cartageJv.Entries);
                cartageJv.Entries.Clear();
                cartageJv.UpdatedAt = DateTime.UtcNow;
            }

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = customerId,
                Description = $"Cartage charge - {saleJv.VoucherNumber}",
                Debit = cartageAmount,
                Credit = 0,
                IsEdited = true,
                SortOrder = 0
            });

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = cartageRequest.TransporterId,
                Description = $"Cartage for {saleJv.VoucherNumber}",
                Debit = 0,
                Credit = cartageAmount,
                IsEdited = true,
                SortOrder = 1
            });

            await _db.SaveChangesAsync();

            // Assign voucher number and create reference for newly created cartage JV
            if (cartageJv.VoucherNumber.StartsWith("_tc_"))
            {
                var lastCvNumber = await GetLastCvNumberAsync();
                cartageJv.VoucherNumber = $"CV-{lastCvNumber + 1:D4}";

                _db.JournalVoucherReferences.Add(new JournalVoucherReference
                {
                    MainVoucherId = saleJv.Id,
                    ReferenceVoucherId = cartageJv.Id
                });

                await _db.SaveChangesAsync();
            }
        }
        else if (cartageJv != null)
        {
            // Cartage removed — delete reference then Cartage JV
            if (cartageRef != null)
                _db.JournalVoucherReferences.Remove(cartageRef);

            _db.JournalEntries.RemoveRange(cartageJv.Entries);
            _db.JournalVouchers.Remove(cartageJv);
        }
    }

    private static DeliveryChallanDto MapToDto(JournalVoucher saleJv, JournalVoucher? cartageJv)
    {
        // Customer = DEBIT entry at SortOrder=0
        var customerEntry = saleJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        // Product lines = CREDIT entries at SortOrder > 0
        var productEntries = saleJv.Entries.Where(e => e.SortOrder > 0).OrderBy(e => e.SortOrder).ToList();

        var dto = new DeliveryChallanDto
        {
            Id = saleJv.Id,
            DcNumber = saleJv.VoucherNumber,
            Date = saleJv.Date,
            CustomerId = customerEntry?.AccountId ?? 0,
            CustomerName = customerEntry?.Account?.Name,
            VehicleNumber = saleJv.VehicleNumber,
            Description = saleJv.Description,
            VoucherType = saleJv.VoucherType.ToString(),
            RatesAdded = saleJv.RatesAdded,
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

        // Cartage from linked CartageVoucher
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

        // Build JournalVouchers summary list
        var jvSummaries = new List<JournalVoucherSummaryDto>();
        jvSummaries.Add(MapJvSummary(saleJv));
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

    private static string? ResolveDescription(
        string? requestedDescription,
        string prefix,
        string? partyName,
        IEnumerable<CreateDcLineRequest> lines,
        IReadOnlyDictionary<int, Account> productAccounts)
    {
        var trimmed = string.IsNullOrWhiteSpace(requestedDescription) ? null : requestedDescription.Trim();
        if (trimmed != null)
            return trimmed;

        var productSummary = string.Join(", ", lines
            .OrderBy(line => line.SortOrder)
            .Select(line =>
            {
                var productName = productAccounts.GetValueOrDefault(line.ProductId)?.Name ?? $"Product {line.ProductId}";
                return $"{productName} ({line.Qty})";
            }));

        if (string.IsNullOrWhiteSpace(productSummary))
            return partyName == null ? null : $"{prefix} {partyName}";

        return $"{prefix} {partyName ?? "Unknown"} - {productSummary}";
    }
}
