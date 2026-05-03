using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Common;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class DeliveryChallanService : IDeliveryChallanService
{
    private readonly AppDbContext _db;
    private readonly IVoucherNumberService _voucherNumberService;

    public DeliveryChallanService(AppDbContext db, IVoucherNumberService voucherNumberService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
    }

    public async Task<List<DeliveryChallanDto>> GetAllAsync()
    {
        var saleJvs = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.SaleVoucher)
            .Include(j => j.Creator).ThenInclude(u => u.Role)
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
            .Include(j => j.Creator).ThenInclude(u => u.Role)
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

    public Task<string> GetNextNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.DeliveryChallan, "DC-");

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
                e.JournalVoucher.VoucherType == VoucherType.SaleVoucher)
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

    public async Task<DeliveryChallanDto> CreateAsync(CreateDcRequest request, int userId)
    {
        var validation = await ValidateRequestAsync(request, PartyRole.Customer, "customer");
        var dcDate = request.Date;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var dcNumber = await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.DeliveryChallan, "DC-");
        var cartageNumber = validation.TransporterAccount == null
            ? null
            : await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.CartageVoucher, "CV-");

        var saleJv = new JournalVoucher
        {
            VoucherNumber = dcNumber,
            Date = dcDate,
            VoucherType = VoucherType.SaleVoucher,
            VehicleNumber = request.VehicleNumber,
            Description = VoucherHelpers.ResolveDescription(request.Description, "Sale to", validation.PartyAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts),
            RatesAdded = request.Lines.All(l => l.Rate > 0),
            CreatedBy = userId,
        };

        var totalProductAmount = 0m;
        var sortOrder = 0;

        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = validation.ProductAccounts[line.ProductId];
            var lineAmount = VoucherHelpers.CalculateLineAmount(product.ProductDetail?.PackingWeightKg ?? 0, line.Rbp, line.Qty, line.Rate);
            totalProductAmount += lineAmount;

            saleJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product.Name} - {line.Qty} bags",
                Debit = 0,
                Credit = lineAmount,
                Qty = line.Qty,
                Rbp = VoucherHelpers.NormalizeRbp(line.Rbp),
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = false,
                SortOrder = ++sortOrder
            });
        }

        saleJv.Entries.Add(new JournalEntry
        {
            AccountId = validation.PartyAccount.Id,
            Description = $"Delivery to customer - {dcNumber}",
            Debit = totalProductAmount,
            Credit = 0,
            IsEdited = false,
            SortOrder = 0
        });

        _db.JournalVouchers.Add(saleJv);

        if (validation.TransporterAccount != null && request.Cartage != null && cartageNumber != null)
        {
            var cartageJv = VoucherHelpers.BuildCartageVoucher(
                cartageNumber,
                dcDate,
                dcNumber,
                userId,
                validation.PartyAccount.Id,
                validation.TransporterAccount.Id,
                request.Cartage.Amount,
                false);

            _db.JournalVouchers.Add(cartageJv);
            _db.JournalVoucherReferences.Add(new JournalVoucherReference
            {
                MainVoucher = saleJv,
                ReferenceVoucher = cartageJv
            });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(saleJv.Id))!;
    }

    public async Task<DeliveryChallanDto?> UpdateAsync(int id, CreateDcRequest request)
    {
        var saleJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.SaleVoucher)
            .Include(j => j.Entries)
            .FirstOrDefaultAsync();

        if (saleJv == null) return null;

        var validation = await ValidateRequestAsync(request, PartyRole.Customer, "customer");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        saleJv.Date = request.Date;
        saleJv.VehicleNumber = request.VehicleNumber;
        saleJv.Description = VoucherHelpers.ResolveDescription(request.Description, "Sale to", validation.PartyAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts);
        saleJv.RatesAdded = request.Lines.All(l => l.Rate > 0);
        saleJv.UpdatedAt = DateTime.UtcNow;

        _db.JournalEntries.RemoveRange(saleJv.Entries);
        saleJv.Entries.Clear();

        var totalProductAmount = 0m;
        var sortOrder = 0;

        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = validation.ProductAccounts[line.ProductId];
            var lineAmount = VoucherHelpers.CalculateLineAmount(product.ProductDetail?.PackingWeightKg ?? 0, line.Rbp, line.Qty, line.Rate);
            totalProductAmount += lineAmount;

            saleJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product.Name} - {line.Qty} bags",
                Debit = 0,
                Credit = lineAmount,
                Qty = line.Qty,
                Rbp = VoucherHelpers.NormalizeRbp(line.Rbp),
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = true,
                SortOrder = ++sortOrder
            });
        }

        saleJv.Entries.Add(new JournalEntry
        {
            AccountId = validation.PartyAccount.Id,
            Description = $"Delivery to customer - {saleJv.VoucherNumber}",
            Debit = totalProductAmount,
            Credit = 0,
            IsEdited = true,
            SortOrder = 0
        });

        await RebuildCartageVoucherAsync(saleJv, validation.PartyAccount.Id, validation.TransporterAccount, request.Cartage);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(saleJv.Id))!;
    }

    public async Task<bool> UpdateRatesAsync(int id, UpdateDcRatesRequest request)
    {
        var saleJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.SaleVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .FirstOrDefaultAsync();

        if (saleJv == null) return false;

        foreach (var update in request.Lines)
        {
            var entry = saleJv.Entries.FirstOrDefault(e => e.Id == update.EntryId);
            if (entry != null && entry.SortOrder > 0)
            {
                entry.Rate = update.Rate;
                entry.IsEdited = true;

                var weight = entry.Account?.ProductDetail?.PackingWeightKg ?? 0;
                entry.Credit = VoucherHelpers.CalculateLineAmount(weight, entry.Rbp, entry.Qty ?? 0, update.Rate);
            }
        }

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

    private async Task RebuildCartageVoucherAsync(
        JournalVoucher saleJv,
        int customerId,
        Account? transporterAccount,
        CreateDcCartageRequest? cartageRequest)
    {
        var cartageRef = await _db.JournalVoucherReferences
            .Where(r => r.MainVoucherId == saleJv.Id)
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
                    saleJv.Date,
                    saleJv.VoucherNumber,
                    saleJv.CreatedBy,
                    customerId,
                    transporterAccount.Id,
                    cartageAmount,
                    true);

                _db.JournalVouchers.Add(cartageJv);
                _db.JournalVoucherReferences.Add(new JournalVoucherReference
                {
                    MainVoucherId = saleJv.Id,
                    ReferenceVoucher = cartageJv
                });
                return;
            }

            _db.JournalEntries.RemoveRange(cartageJv.Entries);
            cartageJv.Entries.Clear();
            cartageJv.Date = saleJv.Date;
            cartageJv.Description = $"Cartage entries for {saleJv.VoucherNumber}";
            cartageJv.UpdatedAt = DateTime.UtcNow;

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
                AccountId = transporterAccount.Id,
                Description = $"Cartage for {saleJv.VoucherNumber}",
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

    private async Task<ValidatedVoucherRequest> ValidateRequestAsync(CreateDcRequest request, PartyRole requiredRole, string partyLabel)
    {
        if (request.CustomerId <= 0)
        {
            throw new RequestValidationException($"Select a valid {partyLabel}.");
        }

        if (request.Lines.Count == 0)
        {
            throw new RequestValidationException("Add at least one product line.");
        }

        foreach (var line in request.Lines)
        {
            if (line.ProductId <= 0)
            {
                throw new RequestValidationException("Each line must reference a valid product.");
            }

            if (line.Qty <= 0)
            {
                throw new RequestValidationException("Each line must have a quantity greater than zero.");
            }

            if (line.Rate < 0)
            {
                throw new RequestValidationException("Rates cannot be negative.");
            }

            if (!VoucherHelpers.IsValidRbp(line.Rbp))
            {
                throw new RequestValidationException("RBP must be either Yes or No.");
            }
        }

        if (request.Cartage != null)
        {
            if (request.Cartage.TransporterId <= 0)
            {
                throw new RequestValidationException("Select a valid transporter.");
            }

            if (request.Cartage.Amount <= 0)
            {
                throw new RequestValidationException("Cartage amount must be greater than zero.");
            }
        }

        var accountIds = request.Lines
            .Select(line => line.ProductId)
            .Append(request.CustomerId)
            .Concat(request.Cartage == null ? [] : [request.Cartage.TransporterId])
            .Distinct()
            .ToList();

        var accountsById = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.PartyDetail)
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        if (!accountsById.TryGetValue(request.CustomerId, out var partyAccount))
        {
            throw new RequestValidationException($"Select a valid {partyLabel}.");
        }

        EnsurePartyAccount(partyAccount, requiredRole, partyLabel);

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

        return new ValidatedVoucherRequest(productAccounts, partyAccount, transporterAccount);
    }

    private static void EnsurePartyAccount(Account account, PartyRole requiredRole, string partyLabel)
    {
        if (account.AccountType != AccountType.Party || account.PartyDetail == null)
        {
            throw new RequestValidationException($"Select a valid {partyLabel}.");
        }

        var isValid = requiredRole switch
        {
            PartyRole.Customer => account.PartyDetail.PartyRole is PartyRole.Customer or PartyRole.Both,
            PartyRole.Vendor => account.PartyDetail.PartyRole is PartyRole.Vendor or PartyRole.Both,
            _ => false
        };

        if (!isValid)
        {
            throw new RequestValidationException($"Selected account is not a valid {partyLabel}.");
        }
    }

    private static bool IsInventoryAccount(Account account) =>
        VoucherHelpers.IsInventoryAccount(account);

    private static DeliveryChallanDto MapToDto(JournalVoucher saleJv, JournalVoucher? cartageJv)
    {
        var customerEntry = saleJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
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
            CreatedByRole = saleJv.Creator?.Role?.Name,
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

        dto.JournalVouchers = [MapJvSummary(saleJv)];
        if (cartageJv != null)
        {
            dto.JournalVouchers.Add(MapJvSummary(cartageJv));
        }

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

    private sealed record ValidatedVoucherRequest(
        IReadOnlyDictionary<int, Account> ProductAccounts,
        Account PartyAccount,
        Account? TransporterAccount);
}
