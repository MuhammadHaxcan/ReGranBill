import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { LoginComponent } from './pages/login/login.component';
import { DeliveryChallanComponent } from './pages/delivery-challan/delivery-challan.component';
import { CategoriesAccountsComponent } from './pages/categories-accounts/categories-accounts.component';
import { PendingChallansComponent } from './pages/pending-challans/pending-challans.component';
import { AddRateComponent } from './pages/add-rate/add-rate.component';
import { SearchableSelectComponent } from './components/searchable-select/searchable-select.component';
import { PkCurrencyPipe } from './pipes/pk-currency.pipe';
import { AuthInterceptor } from './interceptors/auth.interceptor';

@NgModule({
  declarations: [
    App,
    LoginComponent,
    DeliveryChallanComponent,
    CategoriesAccountsComponent,
    PendingChallansComponent,
    AddRateComponent,
    SearchableSelectComponent,
    PkCurrencyPipe
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule,
    AppRoutingModule
  ],
  providers: [
    provideBrowserGlobalErrorListeners(),
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true }
  ],
  bootstrap: [App]
})
export class AppModule { }
