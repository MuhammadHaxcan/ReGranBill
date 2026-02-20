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
        return await _db.DeliveryChallans
            .Include(d => d.Customer).ThenInclude(c => c.PartyDetail)
            .Include(d => d.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.ProductDetail)
            .Include(d => d.Cartage).ThenInclude(c => c!.Transporter).ThenInclude(t => t.PartyDetail)
            .Include(d => d.JournalVouchers).ThenInclude(j => j.Entries).ThenInclude(e => e.Account)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => MapToDto(d))
            .ToListAsync();
    }

    public async Task<DeliveryChallanDto?> GetByIdAsync(int id)
    {
        var dc = await _db.DeliveryChallans
            .Include(d => d.Customer).ThenInclude(c => c.PartyDetail)
            .Include(d => d.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.ProductDetail)
            .Include(d => d.Cartage).ThenInclude(c => c!.Transporter).ThenInclude(t => t.PartyDetail)
            .Include(d => d.JournalVouchers).ThenInclude(j => j.Entries).ThenInclude(e => e.Account)
            .FirstOrDefaultAsync(d => d.Id == id);

        return dc == null ? null : MapToDto(dc);
    }

    public async Task<string> GetNextNumberAsync()
    {
        var seq = await _db.DcNumberSequences.FirstAsync();
        var next = seq.LastNumber + 1;
        return $"DC-{next:D4}";
    }

    public async Task<DeliveryChallanDto> CreateAsync(CreateDcRequest request, int userId)
    {
        // Increment sequence
        var seq = await _db.DcNumberSequences.FirstAsync();
        seq.LastNumber++;

        var status = Enum.Parse<ChallanStatus>(request.Status);
        var voucherType = Enum.Parse<VoucherType>(request.VoucherType ?? "SaleVoucher");

        var dc = new DeliveryChallan
        {
            DcNumber = request.DcNumber,
            Date = request.Date,
            CustomerId = request.CustomerId,
            VehicleNumber = request.VehicleNumber,
            Description = request.Description,
            VoucherType = voucherType,
            Status = status,
            RatesAdded = false,
            CreatedBy = userId,
        };

        foreach (var line in request.Lines)
        {
            dc.Lines.Add(new DcLine
            {
                ProductId = line.ProductId,
                Rbp = line.Rbp,
                Qty = line.Qty,
                Rate = line.Rate,
                SortOrder = line.SortOrder
            });
        }

        if (request.Cartage != null)
        {
            dc.Cartage = new DcCartage
            {
                TransporterId = request.Cartage.TransporterId,
                Amount = request.Cartage.Amount
            };
        }

        _db.DeliveryChallans.Add(dc);
        await _db.SaveChangesAsync();

        // Auto-create Journal Voucher with double-entry accounting
        await CreateJournalVoucherAsync(dc, userId);

        return (await GetByIdAsync(dc.Id))!;
    }

    public async Task<DeliveryChallanDto?> UpdateAsync(int id, CreateDcRequest request)
    {
        var dc = await _db.DeliveryChallans
            .Include(d => d.Lines)
            .Include(d => d.Cartage)
            .Include(d => d.JournalVouchers).ThenInclude(j => j.Entries)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dc == null) return null;

        dc.Date = request.Date;
        dc.CustomerId = request.CustomerId;
        dc.VehicleNumber = request.VehicleNumber;
        dc.Description = request.Description;
        dc.Status = Enum.Parse<ChallanStatus>(request.Status);
        dc.UpdatedAt = DateTime.UtcNow;

        // Replace lines
        _db.DcLines.RemoveRange(dc.Lines);
        foreach (var line in request.Lines)
        {
            dc.Lines.Add(new DcLine
            {
                ProductId = line.ProductId,
                Rbp = line.Rbp,
                Qty = line.Qty,
                Rate = line.Rate,
                SortOrder = line.SortOrder
            });
        }

        // Replace cartage
        if (dc.Cartage != null)
            _db.DcCartages.Remove(dc.Cartage);

        if (request.Cartage != null)
        {
            dc.Cartage = new DcCartage
            {
                TransporterId = request.Cartage.TransporterId,
                Amount = request.Cartage.Amount
            };
        }

        await _db.SaveChangesAsync();

        // Rebuild journal entries
        await RebuildJournalEntriesAsync(dc);

        return (await GetByIdAsync(dc.Id))!;
    }

    public async Task<bool> UpdateRatesAsync(int id, UpdateDcRatesRequest request)
    {
        var dc = await _db.DeliveryChallans
            .Include(d => d.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.ProductDetail)
            .Include(d => d.Cartage)
            .Include(d => d.JournalVouchers).ThenInclude(j => j.Entries)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (dc == null) return false;

        foreach (var update in request.Lines)
        {
            var line = dc.Lines.FirstOrDefault(l => l.Id == update.LineId);
            if (line != null)
                line.Rate = update.Rate;
        }

        // Check if all lines have rates
        dc.RatesAdded = dc.Lines.All(l => l.Rate > 0);
        dc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Rebuild journal entries with updated amounts
        await RebuildJournalEntriesAsync(dc);

        return true;
    }

    public async Task<bool> SubmitAsync(int id)
    {
        var dc = await _db.DeliveryChallans.FindAsync(id);
        if (dc == null) return false;

        dc.Status = ChallanStatus.Posted;
        dc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Creates Journal Vouchers from a Delivery Challan:
    /// - Sale JV: DEBIT customer (product total), CREDIT each product line
    /// - Cartage JV (if cartage exists): DEBIT customer (cartage), CREDIT transporter (cartage)
    /// </summary>
    private async Task CreateJournalVoucherAsync(DeliveryChallan dc, int userId)
    {
        var dcWithDetails = await _db.DeliveryChallans
            .Include(d => d.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.ProductDetail)
            .Include(d => d.Cartage)
            .FirstAsync(d => d.Id == dc.Id);

        // --- Sale JV (always created) ---
        var saleJv = new JournalVoucher
        {
            VoucherNumber = $"_ps_{dc.Id}",
            Date = dc.Date,
            VoucherType = VoucherType.SaleVoucher,
            DcId = dc.Id,
            Description = $"Sale entries for {dc.DcNumber}",
            RatesAdded = false,
            CreatedBy = userId,
        };

        int sortOrder = 0;
        decimal totalProductAmount = 0;

        foreach (var line in dcWithDetails.Lines.OrderBy(l => l.SortOrder))
        {
            var lineAmount = line.Rbp == "Yes"
                ? (line.Product?.ProductDetail?.PackingWeightKg ?? 0) * line.Qty * line.Rate
                : line.Qty * line.Rate;
            totalProductAmount += lineAmount;

            saleJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{line.Product?.Name} - {line.Qty} bags",
                Debit = 0,
                Credit = lineAmount,
                Qty = line.Qty,
                Rbp = line.Rbp,
                SortOrder = ++sortOrder
            });
        }

        saleJv.Entries.Add(new JournalEntry
        {
            AccountId = dc.CustomerId,
            Description = $"Delivery to customer - {dc.DcNumber}",
            Debit = totalProductAmount,
            Credit = 0,
            SortOrder = 0
        });

        _db.JournalVouchers.Add(saleJv);

        // --- Cartage JV (only when cartage exists with amount > 0) ---
        JournalVoucher? cartageJv = null;
        decimal cartageAmount = dcWithDetails.Cartage?.Amount ?? 0;
        if (dcWithDetails.Cartage != null && cartageAmount > 0)
        {
            cartageJv = new JournalVoucher
            {
                VoucherNumber = $"_pc_{dc.Id}",
                Date = dc.Date,
                VoucherType = VoucherType.CartageVoucher,
                DcId = dc.Id,
                Description = $"Cartage entries for {dc.DcNumber}",
                RatesAdded = true,
                CreatedBy = userId,
            };

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = dc.CustomerId,
                Description = $"Cartage charge - {dc.DcNumber}",
                Debit = cartageAmount,
                Credit = 0,
                SortOrder = 0
            });

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = dcWithDetails.Cartage.TransporterId,
                Description = $"Cartage for {dc.DcNumber}",
                Debit = 0,
                Credit = cartageAmount,
                SortOrder = 1
            });

            _db.JournalVouchers.Add(cartageJv);
        }

        // Save to get auto-generated Ids
        await _db.SaveChangesAsync();

        // Now assign voucher numbers from the JV's own Id
        saleJv.VoucherNumber = $"DC-{saleJv.Id:D4}";
        if (cartageJv != null)
            cartageJv.VoucherNumber = $"CV-{cartageJv.Id:D4}";

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Rebuilds journal entries when DC lines or rates change.
    /// Handles Sale JV (products) and Cartage JV (cartage) separately.
    /// </summary>
    private async Task RebuildJournalEntriesAsync(DeliveryChallan dc)
    {
        // Load all JVs for this DC
        var jvs = dc.JournalVouchers?.Where(j => j.Entries != null).ToList();
        if (jvs == null || !jvs.Any())
        {
            jvs = await _db.JournalVouchers
                .Include(j => j.Entries)
                .Where(j => j.DcId == dc.Id)
                .ToListAsync();
        }

        // Load lines with product details if not loaded
        if (!dc.Lines.Any(l => l.Product != null))
        {
            await _db.Entry(dc).Collection(d => d.Lines).Query()
                .Include(l => l.Product).ThenInclude(p => p.ProductDetail)
                .LoadAsync();
        }
        if (dc.Cartage != null && dc.Cartage.Transporter == null)
        {
            await _db.Entry(dc.Cartage).Reference(c => c.Transporter).LoadAsync();
        }

        // --- Rebuild Sale JV ---
        var saleJv = jvs.FirstOrDefault(j => j.VoucherType == VoucherType.SaleVoucher);
        if (saleJv != null)
        {
            _db.JournalEntries.RemoveRange(saleJv.Entries);
            saleJv.Entries.Clear();

            int sortOrder = 0;
            decimal totalProductAmount = 0;

            foreach (var line in dc.Lines.OrderBy(l => l.SortOrder))
            {
                var lineAmount = line.Rbp == "Yes"
                    ? (line.Product?.ProductDetail?.PackingWeightKg ?? 0) * line.Qty * line.Rate
                    : line.Qty * line.Rate;
                totalProductAmount += lineAmount;

                saleJv.Entries.Add(new JournalEntry
                {
                    AccountId = line.ProductId,
                    Description = $"{line.Product?.Name} - {line.Qty} bags",
                    Debit = 0,
                    Credit = lineAmount,
                    Qty = line.Qty,
                    Rbp = line.Rbp,
                    SortOrder = ++sortOrder
                });
            }

            saleJv.Entries.Add(new JournalEntry
            {
                AccountId = dc.CustomerId,
                Description = $"Delivery to customer - {dc.DcNumber}",
                Debit = totalProductAmount,
                Credit = 0,
                SortOrder = 0
            });

            saleJv.RatesAdded = dc.Lines.All(l => l.Rate > 0);
            saleJv.UpdatedAt = DateTime.UtcNow;
            dc.RatesAdded = saleJv.RatesAdded;
        }

        // --- Handle Cartage JV ---
        var cartageJv = jvs.FirstOrDefault(j => j.VoucherType == VoucherType.CartageVoucher);
        decimal cartageAmount = dc.Cartage?.Amount ?? 0;

        if (dc.Cartage != null && cartageAmount > 0)
        {
            if (cartageJv == null)
            {
                // Create new Cartage JV
                cartageJv = new JournalVoucher
                {
                    VoucherNumber = $"_pc_{dc.Id}",
                    Date = dc.Date,
                    VoucherType = VoucherType.CartageVoucher,
                    DcId = dc.Id,
                    Description = $"Cartage entries for {dc.DcNumber}",
                    RatesAdded = true,
                    CreatedBy = dc.CreatedBy,
                };
                _db.JournalVouchers.Add(cartageJv);
            }
            else
            {
                // Rebuild existing Cartage JV entries
                _db.JournalEntries.RemoveRange(cartageJv.Entries);
                cartageJv.Entries.Clear();
                cartageJv.UpdatedAt = DateTime.UtcNow;
            }

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = dc.CustomerId,
                Description = $"Cartage charge - {dc.DcNumber}",
                Debit = cartageAmount,
                Credit = 0,
                SortOrder = 0
            });

            cartageJv.Entries.Add(new JournalEntry
            {
                AccountId = dc.Cartage!.TransporterId,
                Description = $"Cartage for {dc.DcNumber}",
                Debit = 0,
                Credit = cartageAmount,
                SortOrder = 1
            });
        }
        else if (cartageJv != null)
        {
            // Cartage removed — delete Cartage JV
            _db.JournalEntries.RemoveRange(cartageJv.Entries);
            _db.JournalVouchers.Remove(cartageJv);
        }

        await _db.SaveChangesAsync();

        // Assign voucher number from Id for newly created Cartage JV
        if (cartageJv != null && cartageJv.VoucherNumber.StartsWith("_pc_"))
        {
            cartageJv.VoucherNumber = $"CV-{cartageJv.Id:D4}";
            await _db.SaveChangesAsync();
        }
    }

    private static DeliveryChallanDto MapToDto(DeliveryChallan d) => new()
    {
        Id = d.Id,
        DcNumber = d.DcNumber,
        Date = d.Date,
        CustomerId = d.CustomerId,
        CustomerName = d.Customer?.Name,
        VehicleNumber = d.VehicleNumber,
        Description = d.Description,
        VoucherType = d.VoucherType.ToString(),
        Status = d.Status.ToString(),
        RatesAdded = d.RatesAdded,
        Lines = d.Lines.OrderBy(l => l.SortOrder).Select(l => new DcLineDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ProductName = l.Product?.Name,
            Packing = l.Product?.ProductDetail?.Packing,
            PackingWeightKg = l.Product?.ProductDetail?.PackingWeightKg ?? 0,
            Rbp = l.Rbp,
            Qty = l.Qty,
            Rate = l.Rate,
            SortOrder = l.SortOrder
        }).ToList(),
        Cartage = d.Cartage != null ? new DcCartageDto
        {
            TransporterId = d.Cartage.TransporterId,
            TransporterName = d.Cartage.Transporter?.Name,
            City = d.Cartage.Transporter?.PartyDetail?.City,
            Amount = d.Cartage.Amount
        } : null,
        JournalVouchers = d.JournalVouchers.OrderBy(j => j.Id).Select(j => new JournalVoucherSummaryDto
        {
            Id = j.Id,
            VoucherNumber = j.VoucherNumber,
            VoucherType = j.VoucherType.ToString(),
            RatesAdded = j.RatesAdded,
            TotalDebit = j.Entries.Sum(e => e.Debit),
            TotalCredit = j.Entries.Sum(e => e.Credit),
            Entries = j.Entries.OrderBy(e => e.SortOrder).Select(e => new JournalEntryDto
            {
                Id = e.Id,
                AccountId = e.AccountId,
                AccountName = e.Account.Name,
                Description = e.Description,
                Debit = e.Debit,
                Credit = e.Credit,
                Qty = e.Qty,
                Rbp = e.Rbp,
                SortOrder = e.SortOrder
            }).ToList()
        }).ToList()
    };
}
