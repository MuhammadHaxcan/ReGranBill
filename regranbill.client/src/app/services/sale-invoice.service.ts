import { Injectable } from '@angular/core';
import { Product, Customer, SaleInvoice } from '../models/sale-invoice.model';

@Injectable({
  providedIn: 'root'
})
export class SaleInvoiceService {
  private invoiceCounter = 42;
  private nextId = 1;

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

  private invoices: SaleInvoice[] = [
    {
      id: 1,
      invoiceNumber: 'SV-0038',
      date: new Date('2026-02-15'),
      customerId: 1,
      customerName: 'Akhtar Plastics',
      description: 'Monthly supply order',
      lines: [
        { product: { id: 1, name: 'HDPE Blue Drum', packing: '50kg / Bag', packingWeightKg: 50 }, rbp: 'Yes', qty: 10, rate: 0 },
        { product: { id: 2, name: 'PP-125 Natural', packing: '25kg / Bag', packingWeightKg: 25 }, rbp: 'No', qty: 20, rate: 0 },
      ],
      cartage: { providerName: 'Al-Madina Logistics', city: 'Lahore', amount: 350 },
      status: 'Draft'
    },
    {
      id: 2,
      invoiceNumber: 'SV-0039',
      date: new Date('2026-02-16'),
      customerId: 3,
      customerName: 'Crescent Polymers',
      description: 'Urgent delivery',
      lines: [
        { product: { id: 3, name: 'LDPE Film Grade', packing: '25kg / Bag', packingWeightKg: 25 }, rbp: 'Yes', qty: 15, rate: 0 },
      ],
      cartage: null,
      status: 'Draft'
    },
    {
      id: 3,
      invoiceNumber: 'SV-0040',
      date: new Date('2026-02-17'),
      customerId: 2,
      customerName: 'Bilal Industries',
      description: '',
      lines: [
        { product: { id: 4, name: 'HDPE Blow Moulding', packing: '50kg / Bag', packingWeightKg: 50 }, rbp: 'Yes', qty: 8, rate: 0 },
        { product: { id: 5, name: 'PP Raffia', packing: '25kg / Bag', packingWeightKg: 25 }, rbp: 'Yes', qty: 12, rate: 0 },
        { product: { id: 6, name: 'LLDPE Roto Grade', packing: '50kg / Bag', packingWeightKg: 50 }, rbp: 'No', qty: 5, rate: 0 },
      ],
      cartage: { providerName: 'Fast Cargo', city: 'Faisalabad', amount: 500 },
      status: 'Draft'
    },
    {
      id: 4,
      invoiceNumber: 'SV-0041',
      date: new Date('2026-02-18'),
      customerId: 5,
      customerName: 'Eagle Plastics',
      description: 'Sample order',
      lines: [
        { product: { id: 1, name: 'HDPE Blue Drum', packing: '50kg / Bag', packingWeightKg: 50 }, rbp: 'No', qty: 3, rate: 0 },
      ],
      cartage: { providerName: 'City Express', city: 'Islamabad', amount: 200 },
      status: 'Draft'
    }
  ];

  constructor() {
    this.nextId = 5;
  }

  getNextInvoiceNumber(): string {
    return `SV-${String(this.invoiceCounter).padStart(4, '0')}`;
  }

  getProducts(): Product[] {
    return [...this.products];
  }

  getCustomers(): Customer[] {
    return [...this.customers];
  }

  getAllInvoices(): SaleInvoice[] {
    return [...this.invoices];
  }

  getInvoiceById(id: number): SaleInvoice | undefined {
    const invoice = this.invoices.find(inv => inv.id === id);
    if (invoice) {
      return {
        ...invoice,
        lines: invoice.lines.map(l => ({ ...l, product: l.product ? { ...l.product } : null })),
        cartage: invoice.cartage ? { ...invoice.cartage } : null
      };
    }
    return undefined;
  }

  saveDraft(invoice: SaleInvoice): void {
    const customer = this.customers.find(c => c.id === invoice.customerId);
    const toSave: SaleInvoice = {
      ...invoice,
      customerName: customer ? customer.name : '',
    };

    if (invoice.id) {
      const idx = this.invoices.findIndex(inv => inv.id === invoice.id);
      if (idx >= 0) {
        this.invoices[idx] = toSave;
      }
    } else {
      toSave.id = this.nextId++;
      this.invoices.push(toSave);
    }
    console.log('Draft saved:', toSave);
  }

  submitInvoice(invoice: SaleInvoice): void {
    const customer = this.customers.find(c => c.id === invoice.customerId);
    const toSave: SaleInvoice = {
      ...invoice,
      customerName: customer ? customer.name : '',
    };

    if (invoice.id) {
      const idx = this.invoices.findIndex(inv => inv.id === invoice.id);
      if (idx >= 0) {
        this.invoices[idx] = toSave;
      }
    } else {
      toSave.id = this.nextId++;
      this.invoices.push(toSave);
    }
    console.log('Invoice submitted:', toSave);
    this.invoiceCounter++;
  }

  updateInvoiceRates(id: number, invoice: SaleInvoice): void {
    const idx = this.invoices.findIndex(inv => inv.id === id);
    if (idx >= 0) {
      this.invoices[idx] = { ...invoice, id };
    }
    console.log('Rates updated:', this.invoices[idx]);
  }
}
