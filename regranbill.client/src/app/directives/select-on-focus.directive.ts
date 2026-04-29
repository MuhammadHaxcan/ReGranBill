import { Directive, HostListener } from '@angular/core';

@Directive({
  selector: 'input[type=number]',
  standalone: false
})
export class SelectOnFocusDirective {
  @HostListener('focus', ['$event'])
  onFocus(event: Event): void {
    const input = event.target as HTMLInputElement;
    input.select();
  }
}
