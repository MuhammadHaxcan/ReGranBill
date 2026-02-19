import { HttpClientModule } from '@angular/common/http';
import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { SaleInvoiceComponent } from './pages/sale-invoice/sale-invoice.component';
import { CategoriesAccountsComponent } from './pages/categories-accounts/categories-accounts.component';
import { PkCurrencyPipe } from './pipes/pk-currency.pipe';

@NgModule({
  declarations: [
    App,
    SaleInvoiceComponent,
    CategoriesAccountsComponent,
    PkCurrencyPipe
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule,
    AppRoutingModule
  ],
  providers: [
    provideBrowserGlobalErrorListeners()
  ],
  bootstrap: [App]
})
export class AppModule { }
