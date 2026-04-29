import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'flexNumber',
  standalone: false
})
export class FlexNumberPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value == null || isNaN(value)) return '0';
    const formatted = value.toLocaleString('en-US', {
      minimumFractionDigits: 0,
      maximumFractionDigits: 2
    });
    return formatted;
  }
}
