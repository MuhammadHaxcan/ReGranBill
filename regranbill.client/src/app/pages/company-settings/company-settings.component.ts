import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { CompanySettings, VehicleOption } from '../../models/company-settings.model';
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
  vehicles: VehicleOption[] = [];
  selectedLogo: File | null = null;
  previewUrl: string | null = null;
  loading = false;
  saving = false;
  savingVehicles = false;

  constructor(
    private companySettingsService: CompanySettingsService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadSettings();
    this.loadVehicles();
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

  loadVehicles(): void {
    this.companySettingsService.getVehicles().subscribe({
      next: vehicles => {
        this.vehicles = vehicles
          .slice()
          .sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id);
        this.cdr.detectChanges();
      },
      error: () => {
        this.toast.error('Unable to load vehicle options.');
      }
    });
  }

  addVehicle(): void {
    this.vehicles.push({
      id: 0,
      name: '',
      vehicleNumber: '',
      sortOrder: this.vehicles.length
    });
  }

  removeVehicle(index: number): void {
    this.vehicles.splice(index, 1);
    this.reindexVehicles();
  }

  moveVehicleUp(index: number): void {
    if (index <= 0) return;
    const current = this.vehicles[index];
    this.vehicles[index] = this.vehicles[index - 1];
    this.vehicles[index - 1] = current;
    this.reindexVehicles();
  }

  moveVehicleDown(index: number): void {
    if (index >= this.vehicles.length - 1) return;
    const current = this.vehicles[index];
    this.vehicles[index] = this.vehicles[index + 1];
    this.vehicles[index + 1] = current;
    this.reindexVehicles();
  }

  saveVehicles(): void {
    const payload = this.vehicles.map((vehicle, index) => ({
      id: vehicle.id,
      name: vehicle.name.trim(),
      vehicleNumber: vehicle.vehicleNumber.trim(),
      sortOrder: index
    }));

    if (payload.some(vehicle => !vehicle.name || !vehicle.vehicleNumber)) {
      this.toast.error('Each vehicle must include both name and number.');
      return;
    }

    this.savingVehicles = true;
    this.companySettingsService.updateVehicles({ vehicles: payload }).subscribe({
      next: vehicles => {
        this.vehicles = vehicles
          .slice()
          .sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id);
        this.savingVehicles = false;
        this.toast.success('Vehicle options updated.');
        this.cdr.detectChanges();
      },
      error: err => {
        this.toast.error(err?.error?.message || 'Unable to update vehicle options.');
        this.savingVehicles = false;
        this.cdr.detectChanges();
      }
    });
  }

  trackVehicle(index: number, vehicle: VehicleOption): number {
    return vehicle.id > 0 ? vehicle.id : index;
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

  private reindexVehicles(): void {
    this.vehicles = this.vehicles.map((vehicle, index) => ({
      ...vehicle,
      sortOrder: index
    }));
  }
}
