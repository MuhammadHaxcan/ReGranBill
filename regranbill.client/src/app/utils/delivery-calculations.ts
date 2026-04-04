export interface DeliveryCalculationLine {
  qty?: number | null;
  rate?: number | null;
  rbp?: string | null;
  packingWeightKg?: number | null;
}

export interface PurchaseCalculationLine {
  qty?: number | null;
  totalWeightKg?: number | null;
  rate?: number | null;
}

export function toNumber(value: unknown): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

export function round2(value: number): number {
  return Number(toNumber(value).toFixed(2));
}

export function isPackedLine(rbp: string | null | undefined): boolean {
  return String(rbp ?? 'Yes').trim().toLowerCase() === 'yes';
}

export function getDeliveryLineWeight(line: DeliveryCalculationLine): number {
  const qty = toNumber(line.qty);
  if (!qty) return 0;

  return isPackedLine(line.rbp)
    ? round2(toNumber(line.packingWeightKg) * qty)
    : round2(qty);
}

export function getDeliveryLineAmount(line: DeliveryCalculationLine): number {
  return round2(getDeliveryLineWeight(line) * toNumber(line.rate));
}

export function getDeliveryTotalBags<T extends DeliveryCalculationLine>(lines: T[]): number {
  return lines.reduce((sum, line) => sum + (isPackedLine(line.rbp) ? toNumber(line.qty) : 0), 0);
}

export function getDeliveryTotalWeight<T extends DeliveryCalculationLine>(lines: T[]): number {
  return round2(lines.reduce((sum, line) => sum + getDeliveryLineWeight(line), 0));
}

export function getDeliveryTotalAmount<T extends DeliveryCalculationLine>(lines: T[]): number {
  return round2(lines.reduce((sum, line) => sum + getDeliveryLineAmount(line), 0));
}

export function getPurchaseLineWeight(line: PurchaseCalculationLine): number {
  return round2(toNumber(line.totalWeightKg));
}

export function getPurchaseLineAverageWeight(line: PurchaseCalculationLine): number {
  const qty = toNumber(line.qty);
  if (qty <= 0) return 0;
  return round2(getPurchaseLineWeight(line) / qty);
}

export function getPurchaseLineAmount(line: PurchaseCalculationLine): number {
  return round2(getPurchaseLineWeight(line) * toNumber(line.rate));
}

export function getPurchaseTotalBags<T extends PurchaseCalculationLine>(lines: T[]): number {
  return lines.reduce((sum, line) => sum + toNumber(line.qty), 0);
}

export function getPurchaseTotalWeight<T extends PurchaseCalculationLine>(lines: T[]): number {
  return round2(lines.reduce((sum, line) => sum + getPurchaseLineWeight(line), 0));
}

export function getPurchaseTotalAmount<T extends PurchaseCalculationLine>(lines: T[]): number {
  return round2(lines.reduce((sum, line) => sum + getPurchaseLineAmount(line), 0));
}

export function getPurchaseAverageWeightPerBag<T extends PurchaseCalculationLine>(lines: T[]): number {
  const bags = getPurchaseTotalBags(lines);
  if (bags <= 0) return 0;
  return round2(getPurchaseTotalWeight(lines) / bags);
}
