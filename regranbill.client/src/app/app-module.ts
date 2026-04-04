import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { LoginComponent } from './pages/login/login.component';
import { DeliveryChallanComponent } from './pages/delivery-challan/delivery-challan.component';
import { PurchaseVoucherComponent } from './pages/purchase-voucher/purchase-voucher.component';
import { CategoriesAccountsComponent } from './pages/categories-accounts/categories-accounts.component';
import { PendingChallansComponent } from './pages/pending-challans/pending-challans.component';
import { PendingPurchasesComponent } from './pages/pending-purchases/pending-purchases.component';
import { AddRateComponent } from './pages/add-rate/add-rate.component';
import { AddPurchaseRateComponent } from './pages/add-purchase-rate/add-purchase-rate.component';
import { SearchableSelectComponent } from './components/searchable-select/searchable-select.component';
import { SoaComponent } from './pages/soa/soa.component';
import { MasterReportComponent } from './pages/master-report/master-report.component';
import { ProductStockReportComponent } from './pages/product-stock-report/product-stock-report.component';
import { PrintDcComponent } from './pages/print-dc/print-dc.component';
import { JournalVoucherComponent } from './pages/journal-voucher/journal-voucher.component';
import { CashVoucherComponent } from './pages/cash-voucher/cash-voucher.component';
import { VoucherEditorComponent } from './pages/voucher-editor/voucher-editor.component';
import { PrintPvComponent } from './pages/print-pv/print-pv.component';
import { PrintSoaComponent } from './pages/print-soa/print-soa.component';
import { UserManagementComponent } from './pages/user-management/user-management.component';
import { AccountClosingReportComponent } from './pages/account-closing-report/account-closing-report.component';
import { SalePurchaseReportComponent } from './pages/sale-purchase-report/sale-purchase-report.component';
import { CompanySettingsComponent } from './pages/company-settings/company-settings.component';
import { PrintMasterReportComponent } from './pages/print-master-report/print-master-report.component';
import { PrintAccountClosingReportComponent } from './pages/print-account-closing-report/print-account-closing-report.component';
import { PrintProductStockReportComponent } from './pages/print-product-stock-report/print-product-stock-report.component';
import { PkCurrencyPipe } from './pipes/pk-currency.pipe';
import { AuthInterceptor } from './interceptors/auth.interceptor';

@NgModule({
  declarations: [
    App,
    LoginComponent,
    DeliveryChallanComponent,
    PurchaseVoucherComponent,
    CategoriesAccountsComponent,
    PendingChallansComponent,
    PendingPurchasesComponent,
    AddRateComponent,
    AddPurchaseRateComponent,
    PrintDcComponent,
    JournalVoucherComponent,
    CashVoucherComponent,
    VoucherEditorComponent,
    PrintPvComponent,
    PrintSoaComponent,
    PrintMasterReportComponent,
    PrintAccountClosingReportComponent,
    PrintProductStockReportComponent,
    UserManagementComponent,
    AccountClosingReportComponent,
    SalePurchaseReportComponent,
    CompanySettingsComponent,
    SearchableSelectComponent,
    SoaComponent,
    MasterReportComponent,
    ProductStockReportComponent,
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
