import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SaleInvoiceComponent } from './pages/sale-invoice/sale-invoice.component';
import { CategoriesAccountsComponent } from './pages/categories-accounts/categories-accounts.component';

const routes: Routes = [
  { path: 'sale-invoice', component: SaleInvoiceComponent },
  { path: 'metadata', component: CategoriesAccountsComponent },
  { path: '', redirectTo: 'sale-invoice', pathMatch: 'full' },
  { path: '**', redirectTo: 'sale-invoice' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
