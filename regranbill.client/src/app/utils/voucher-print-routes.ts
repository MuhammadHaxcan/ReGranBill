export function getVoucherPrintPath(voucherType: string, voucherId: number, voucherNumber?: string | null): string | null {
  const hasId = !!voucherId && voucherId > 0;
  const normalizedNumber = voucherNumber?.trim();
  const encodedNumber = normalizedNumber ? encodeURIComponent(normalizedNumber) : null;

  switch (voucherType) {
    case 'SaleVoucher':
      return encodedNumber ? `/print-dc/${encodedNumber}` : (hasId ? `/print-dc/${voucherId}` : null);
    case 'PurchaseVoucher':
      return encodedNumber ? `/print-pv/${encodedNumber}` : (hasId ? `/print-pv/${voucherId}` : null);
    case 'SaleReturnVoucher':
      return encodedNumber ? `/print-sr/${encodedNumber}` : (hasId ? `/print-sr/${voucherId}` : null);
    case 'PurchaseReturnVoucher':
      return encodedNumber ? `/print-pr/${encodedNumber}` : (hasId ? `/print-pr/${voucherId}` : null);
    case 'ProductionVoucher':
      return hasId ? `/print-prod/${voucherId}` : null;
    case 'WashingVoucher':
      return encodedNumber ? `/print-wsh/${encodedNumber}` : (hasId ? `/print-wsh/${voucherId}` : null);
    default:
      return null;
  }
}

export function isPrintableVoucherType(voucherType: string, voucherId: number, voucherNumber?: string | null): boolean {
  return getVoucherPrintPath(voucherType, voucherId, voucherNumber) !== null;
}
