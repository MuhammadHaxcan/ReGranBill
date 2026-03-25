namespace ReGranBill.Server.Services;

public interface IVoucherNumberService
{
    Task<string> GetNextNumberPreviewAsync(string sequenceKey, string prefix, CancellationToken cancellationToken = default);
    Task<string> ReserveNextNumberAsync(string sequenceKey, string prefix, CancellationToken cancellationToken = default);
}
