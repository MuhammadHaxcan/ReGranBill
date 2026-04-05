export interface CompanySettings {
  companyName: string;
  address: string | null;
  hasLogo: boolean;
  updatedAt: string | null;
}

export interface VehicleOption {
  id: number;
  name: string;
  vehicleNumber: string;
  sortOrder: number;
}

export interface VehicleOptionUpsertItem {
  id: number;
  name: string;
  vehicleNumber: string;
  sortOrder: number;
}

export interface UpdateVehicleOptionsRequest {
  vehicles: VehicleOptionUpsertItem[];
}
