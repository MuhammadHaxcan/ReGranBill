import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { PAGES, PageDefinition } from '../../config/page-catalog';

interface PaletteEntry {
  page: PageDefinition;
  score: number;
}

@Component({
  selector: 'app-command-palette',
  templateUrl: './command-palette.component.html',
  styleUrl: './command-palette.component.css',
  standalone: false
})
export class CommandPaletteComponent implements AfterViewInit, OnChanges {
  @Input() open = false;
  @Output() closed = new EventEmitter<void>();

  @ViewChild('searchInput') searchInput?: ElementRef<HTMLInputElement>;

  query = '';
  results: PaletteEntry[] = [];
  activeIndex = 0;

  constructor(
    private router: Router,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngAfterViewInit(): void {
    if (this.open) this.focusInput();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue) {
      this.query = '';
      this.computeResults();
      setTimeout(() => this.focusInput(), 0);
    }
  }

  private focusInput(): void {
    this.searchInput?.nativeElement.focus();
    this.searchInput?.nativeElement.select();
  }

  onQueryChange(): void {
    this.computeResults();
    this.activeIndex = 0;
  }

  private computeResults(): void {
    const allowed = PAGES.filter(p => !p.hidden && !!p.route && this.auth.hasPage(p.key));
    const q = this.query.trim().toLowerCase();
    if (!q) {
      this.results = allowed.map(p => ({ page: p, score: 0 }));
      return;
    }
    this.results = allowed
      .map(p => ({ page: p, score: this.score(p, q) }))
      .filter(e => e.score > 0)
      .sort((a, b) => b.score - a.score);
  }

  private score(p: PageDefinition, q: string): number {
    const label = p.label.toLowerCase();
    const group = p.groupLabel.toLowerCase();
    const desc = (p.description || '').toLowerCase();
    const route = p.route.toLowerCase();
    if (label === q) return 1000;
    if (label.startsWith(q)) return 800;
    if (label.includes(q)) return 600;
    if (route.includes(q)) return 400;
    if (group.includes(q)) return 300;
    if (desc.includes(q)) return 200;
    // loose subsequence match on label
    let i = 0;
    for (const ch of label) {
      if (ch === q[i]) i++;
      if (i === q.length) return 100;
    }
    return 0;
  }

  @HostListener('document:keydown', ['$event'])
  onKey(event: KeyboardEvent): void {
    if (!this.open) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (this.results.length) {
        this.activeIndex = (this.activeIndex + 1) % this.results.length;
        this.cdr.detectChanges();
        this.scrollActiveIntoView();
      }
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (this.results.length) {
        this.activeIndex = (this.activeIndex - 1 + this.results.length) % this.results.length;
        this.cdr.detectChanges();
        this.scrollActiveIntoView();
      }
      return;
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      const entry = this.results[this.activeIndex];
      if (entry) this.go(entry.page, event);
    }
  }

  private scrollActiveIntoView(): void {
    const el = document.querySelector('.cmdp-result.active');
    el?.scrollIntoView({ block: 'nearest' });
  }

  onBackdropClick(): void {
    this.close();
  }

  go(page: PageDefinition, event?: MouseEvent | KeyboardEvent): void {
    if (event && 'button' in event && (event.ctrlKey || event.metaKey || (event as MouseEvent).button === 1)) {
      window.open(page.route, '_blank');
      this.close();
      return;
    }
    void this.router.navigateByUrl(page.route);
    this.close();
  }

  close(): void {
    this.closed.emit();
  }

  trackByKey = (_: number, e: PaletteEntry): string => e.page.key;
}
