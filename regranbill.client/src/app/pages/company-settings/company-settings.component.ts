import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { CompanySettings } from '../../models/company-settings.model';
import { CompanySettingsService } from '../../services/company-settings.service';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-company-settings',
  templateUrl: './company-settings.component.html',
  styleUrl: './company-settings.component.css',
  standalone: false
})
export class CompanySettingsComponent implements OnInit, OnDestroy {
  companyName = '';
  address = '';
  currentSettings: CompanySettings | null = null;
  selectedLogo: File | null = null;
  previewUrl: string | null = null;
  loading = false;
  saving = false;

  constructor(
    private companySettingsService: CompanySettingsService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadSettings();
  }

  ngOnDestroy(): void {
    this.revokePreviewUrl();
  }

  loadSettings(): void {
    this.loading = true;
    this.companySettingsService.getSettings().subscribe({
      next: settings => {
        this.currentSettings = settings;
        this.companyName = settings.companyName ?? '';
        this.address = settings.address ?? '';
        this.loading = false;
        this.loadLogoPreview();
      },
      error: () => {
        this.toast.error('Unable to load company settings.');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedLogo = file;

    if (!file) {
      this.loadLogoPreview();
      return;
    }

    this.revokePreviewUrl();
    this.previewUrl = URL.createObjectURL(file);
    this.cdr.detectChanges();
  }

  save(): void {
    const formData = new FormData();
    formData.append('companyName', this.companyName.trim());
    formData.append('address', this.address.trim());
    if (this.selectedLogo) {
      formData.append('logo', this.selectedLogo);
    }

    this.saving = true;
    this.companySettingsService.updateSettings(formData).subscribe({
      next: settings => {
        this.currentSettings = settings;
        this.selectedLogo = null;
        this.saving = false;
        this.toast.success('Company settings updated.');
        this.loadLogoPreview();
      },
      error: err => {
        this.toast.error(err?.error?.message || 'Unable to update company settings.');
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  private loadLogoPreview(): void {
    this.revokePreviewUrl();

    if (!this.currentSettings?.hasLogo) {
      this.previewUrl = null;
      this.cdr.detectChanges();
      return;
    }

    this.companySettingsService.getLogo().subscribe({
      next: blob => {
        this.previewUrl = URL.createObjectURL(blob);
        this.cdr.detectChanges();
      },
      error: () => {
        this.previewUrl = null;
        this.cdr.detectChanges();
      }
    });
  }

  private revokePreviewUrl(): void {
    if (this.previewUrl?.startsWith('blob:')) {
      URL.revokeObjectURL(this.previewUrl);
    }
    this.previewUrl = null;
  }
}
