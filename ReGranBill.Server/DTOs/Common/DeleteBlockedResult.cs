namespace ReGranBill.Server.DTOs.Common;

public record VoucherRef(int Id, string VoucherNumber, DateOnly Date, string VoucherType);

public record DeleteBlockedResult(string Message, IReadOnlyList<VoucherRef> Vouchers, int TotalCount);

public record DeleteResult(bool Success, string? Error, DeleteBlockedResult? Blocked);
