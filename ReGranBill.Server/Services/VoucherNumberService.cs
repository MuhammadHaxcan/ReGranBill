using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Exceptions;

namespace ReGranBill.Server.Services;

public class VoucherNumberService : IVoucherNumberService
{
    private readonly AppDbContext _db;

    public VoucherNumberService(AppDbContext db) => _db = db;

    public async Task<string> GetNextNumberPreviewAsync(string sequenceKey, string prefix, CancellationToken cancellationToken = default)
    {
        var counter = await GetOrCreateCounterAsync(sequenceKey, prefix, lockForUpdate: false, cancellationToken);

        return $"{prefix}{counter.LastNumber + 1:D4}";
    }

    public async Task<string> ReserveNextNumberAsync(string sequenceKey, string prefix, CancellationToken cancellationToken = default)
    {
        var counter = await GetOrCreateCounterAsync(sequenceKey, prefix, lockForUpdate: true, cancellationToken);

        counter.LastNumber += 1;
        await _db.SaveChangesAsync(cancellationToken);

        return $"{prefix}{counter.LastNumber:D4}";
    }

    private async Task<VoucherCounter> GetOrCreateCounterAsync(
        string sequenceKey,
        string prefix,
        bool lockForUpdate,
        CancellationToken cancellationToken)
    {
        var counter = await LoadCounterAsync(sequenceKey, lockForUpdate, cancellationToken);
        if (counter != null)
        {
            return counter;
        }

        var lastNumber = await GetExistingMaxNumberAsync(prefix, cancellationToken);
        var createdCounter = new VoucherCounter
        {
            SequenceKey = sequenceKey,
            LastNumber = lastNumber
        };

        _db.VoucherCounters.Add(createdCounter);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return lockForUpdate
                ? await LoadCounterAsync(sequenceKey, lockForUpdate: true, cancellationToken)
                    ?? throw new RequestValidationException($"Voucher counter '{sequenceKey}' could not be reloaded.")
                : createdCounter;
        }
        catch (DbUpdateException)
        {
            _db.Entry(createdCounter).State = EntityState.Detached;
            return await LoadCounterAsync(sequenceKey, lockForUpdate, cancellationToken)
                ?? throw new RequestValidationException($"Voucher counter '{sequenceKey}' could not be initialized.");
        }
    }

    private Task<VoucherCounter?> LoadCounterAsync(string sequenceKey, bool lockForUpdate, CancellationToken cancellationToken)
    {
        if (!lockForUpdate)
        {
            return _db.VoucherCounters
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.SequenceKey == sequenceKey, cancellationToken);
        }

        return _db.VoucherCounters
            .FromSqlInterpolated($@"
                SELECT sequence_key, last_number
                FROM voucher_counters
                WHERE sequence_key = {sequenceKey}
                FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<int> GetExistingMaxNumberAsync(string prefix, CancellationToken cancellationToken)
    {
        var existingNumbers = await _db.JournalVouchers
            .AsNoTracking()
            .Where(v => v.VoucherNumber.StartsWith(prefix))
            .Select(v => v.VoucherNumber)
            .ToListAsync(cancellationToken);

        var maxNumber = 0;
        foreach (var voucherNumber in existingNumbers)
        {
            if (!voucherNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = voucherNumber[prefix.Length..];
            if (int.TryParse(suffix, out var parsed) && parsed > maxNumber)
            {
                maxNumber = parsed;
            }
        }

        return maxNumber;
    }
}
