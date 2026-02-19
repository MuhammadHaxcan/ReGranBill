import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'pkCurrency',
  standalone: false
})
export class PkCurrencyPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value == null || isNaN(value)) {
      return 'Rs. 0.00';
    }
    const formatted = value.toLocaleString('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
    return `Rs. ${formatted}`;
  }
}
