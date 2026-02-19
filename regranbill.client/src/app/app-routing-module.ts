import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SaleInvoiceComponent } from './pages/sale-invoice/sale-invoice.component';
import { CategoriesAccountsComponent } from './pages/categories-accounts/categories-accounts.component';
import { PendingInvoicesComponent } from './pages/pending-invoices/pending-invoices.component';
import { AddRateComponent } from './pages/add-rate/add-rate.component';

const routes: Routes = [
  { path: 'sale-invoice', component: SaleInvoiceComponent },
  { path: 'sale-invoice/:id', component: SaleInvoiceComponent },
  { path: 'pending-invoices', component: PendingInvoicesComponent },
  { path: 'add-rate/:id', component: AddRateComponent },
  { path: 'metadata', component: CategoriesAccountsComponent },
  { path: '', redirectTo: 'sale-invoice', pathMatch: 'full' },
  { path: '**', redirectTo: 'sale-invoice' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
