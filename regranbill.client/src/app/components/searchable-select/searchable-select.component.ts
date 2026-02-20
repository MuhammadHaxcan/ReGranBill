import {
  Component, Input, ElementRef, HostListener,
  forwardRef, OnChanges, SimpleChanges, ViewChild, AfterViewChecked
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SelectOption {
  value: any;
  label: string;
  sublabel?: string;
}

@Component({
  selector: 'app-searchable-select',
  templateUrl: './searchable-select.component.html',
  styleUrl: './searchable-select.component.css',
  standalone: false,
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => SearchableSelectComponent),
    multi: true
  }]
})
export class SearchableSelectComponent implements ControlValueAccessor, OnChanges, AfterViewChecked {
  @Input() options: SelectOption[] = [];
  @Input() placeholder = 'Select...';
  @Input() compact = false;

  @ViewChild('searchInput') searchInputRef!: ElementRef<HTMLInputElement>;

  isOpen = false;
  searchTerm = '';
  highlightIndex = -1;
  selectedOption: SelectOption | null = null;

  // Dropdown position (fixed)
  dropdownTop = 0;
  dropdownLeft = 0;
  dropdownWidth = 0;

  private onChange: (value: any) => void = () => {};
  private onTouched: () => void = () => {};
  private internalValue: any = null;
  private needsFocus = false;

  constructor(private elRef: ElementRef) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['options'] && this.internalValue != null) {
      this.selectedOption = this.options.find(o => o.value === this.internalValue) || null;
    }
  }

  ngAfterViewChecked(): void {
    if (this.needsFocus && this.searchInputRef) {
      this.searchInputRef.nativeElement.focus();
      this.needsFocus = false;
    }
  }

  get filteredOptions(): SelectOption[] {
    if (!this.searchTerm.trim()) return this.options;
    const term = this.searchTerm.toLowerCase();
    return this.options.filter(o =>
      o.label.toLowerCase().includes(term) ||
      (o.sublabel && o.sublabel.toLowerCase().includes(term))
    );
  }

  writeValue(value: any): void {
    this.internalValue = value;
    this.selectedOption = this.options.find(o => o.value === value) || null;
  }

  registerOnChange(fn: (value: any) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }

  toggleDropdown(): void {
    this.isOpen ? this.close() : this.open();
  }

  open(): void {
    this.updatePosition();
    this.isOpen = true;
    this.searchTerm = '';
    this.highlightIndex = -1;
    this.needsFocus = true;
  }

  close(): void {
    this.isOpen = false;
    this.searchTerm = '';
    this.onTouched();
  }

  selectOption(option: SelectOption): void {
    this.selectedOption = option;
    this.internalValue = option.value;
    this.onChange(option.value);
    this.close();
  }

  clear(event: Event): void {
    event.stopPropagation();
    this.selectedOption = null;
    this.internalValue = null;
    this.onChange(null);
  }

  onKeydown(event: KeyboardEvent): void {
    const filtered = this.filteredOptions;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!this.isOpen) { this.open(); return; }
        this.highlightIndex = Math.min(this.highlightIndex + 1, filtered.length - 1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.highlightIndex = Math.max(this.highlightIndex - 1, 0);
        break;
      case 'Enter':
        event.preventDefault();
        if (this.highlightIndex >= 0 && this.highlightIndex < filtered.length) {
          this.selectOption(filtered[this.highlightIndex]);
        }
        break;
      case 'Escape':
        this.close();
        break;
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (!this.elRef.nativeElement.contains(event.target)) {
      this.close();
    }
  }

  @HostListener('window:scroll', [])
  @HostListener('window:resize', [])
  onWindowChange(): void {
    if (this.isOpen) {
      this.updatePosition();
    }
  }

  private updatePosition(): void {
    const trigger = this.elRef.nativeElement.querySelector('.ss-trigger');
    if (!trigger) return;
    const rect = trigger.getBoundingClientRect();
    this.dropdownTop = rect.bottom + 4;
    this.dropdownLeft = rect.left;
    this.dropdownWidth = rect.width;
  }
}
