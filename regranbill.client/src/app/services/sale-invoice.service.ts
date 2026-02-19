import { Injectable } from '@angular/core';
import { Product, Customer, SaleInvoice } from '../models/sale-invoice.model';

@Injectable({
  providedIn: 'root'
})
export class SaleInvoiceService {
  private invoiceCounter = 42;

  private products: Product[] = [
    { id: 1, name: 'HDPE Blue Drum', packing: '50kg / Bag', packingWeightKg: 50 },
    { id: 2, name: 'PP-125 Natural', packing: '25kg / Bag', packingWeightKg: 25 },
    { id: 3, name: 'LDPE Film Grade', packing: '25kg / Bag', packingWeightKg: 25 },
    { id: 4, name: 'HDPE Blow Moulding', packing: '50kg / Bag', packingWeightKg: 50 },
    { id: 5, name: 'PP Raffia', packing: '25kg / Bag', packingWeightKg: 25 },
    { id: 6, name: 'LLDPE Roto Grade', packing: '50kg / Bag', packingWeightKg: 50 },
  ];

  private customers: Customer[] = [
    { id: 1, name: 'Akhtar Plastics', city: 'Lahore' },
    { id: 2, name: 'Bilal Industries', city: 'Faisalabad' },
    { id: 3, name: 'Crescent Polymers', city: 'Karachi' },
    { id: 4, name: 'Delta Packaging', city: 'Multan' },
    { id: 5, name: 'Eagle Plastics', city: 'Islamabad' },
  ];

  getNextInvoiceNumber(): string {
    return `SV-${String(this.invoiceCounter).padStart(4, '0')}`;
  }

  getProducts(): Product[] {
    return [...this.products];
  }

  getCustomers(): Customer[] {
    return [...this.customers];
  }

  saveDraft(invoice: SaleInvoice): void {
    console.log('Draft saved:', invoice);
  }

  submitInvoice(invoice: SaleInvoice): void {
    console.log('Invoice submitted:', invoice);
    this.invoiceCounter++;
  }
}
