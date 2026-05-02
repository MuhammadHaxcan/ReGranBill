import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FormsModule } from '@angular/forms';
import { BsDatepickerModule, BsDatepickerConfig } from 'ngx-bootstrap/datepicker';

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
import { SaleReturnComponent } from './pages/sale-return/sale-return.component';
import { PendingSaleReturnsComponent } from './pages/pending-sale-returns/pending-sale-returns.component';
import { AddSaleReturnRateComponent } from './pages/add-sale-return-rate/add-sale-return-rate.component';
import { PrintSrComponent } from './pages/print-sr/print-sr.component';
import { PurchaseReturnComponent } from './pages/purchase-return/purchase-return.component';
import { PendingPurchaseReturnsComponent } from './pages/pending-purchase-returns/pending-purchase-returns.component';
import { AddPurchaseReturnRateComponent } from './pages/add-purchase-return-rate/add-purchase-return-rate.component';
import { PrintCustomerLedgerComponent } from './pages/print-customer-ledger/print-customer-ledger.component';
import { PrintPrComponent } from './pages/print-pr/print-pr.component';
import { PendingComponent } from './pages/pending/pending.component';
import { CustomerLedgerComponent } from './pages/customer-ledger/customer-ledger.component';
import { PkCurrencyPipe } from './pipes/pk-currency.pipe';
import { FlexNumberPipe } from './pipes/flex-number.pipe';
import { AuthInterceptor } from './interceptors/auth.interceptor';
import { SelectOnFocusDirective } from './directives/select-on-focus.directive';
import { PdfViewerShellComponent } from './components/pdf-viewer-shell/pdf-viewer-shell.component';

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
    PdfViewerShellComponent,
    PkCurrencyPipe,
    FlexNumberPipe,
    SelectOnFocusDirective,
    SaleReturnComponent,
    PendingSaleReturnsComponent,
    AddSaleReturnRateComponent,
    PrintSrComponent,
    PurchaseReturnComponent,
    PendingPurchaseReturnsComponent,
    AddPurchaseReturnRateComponent,
    PrintPrComponent,
    PrintCustomerLedgerComponent,
    PendingComponent,
    CustomerLedgerComponent,
      ],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    HttpClientModule,
    FormsModule,
    AppRoutingModule,
    BsDatepickerModule
  ],
  providers: [
    provideBrowserGlobalErrorListeners(),
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    {
      provide: BsDatepickerConfig,
      useFactory: () => Object.assign(new BsDatepickerConfig(), {
        dateInputFormat: 'DD-MM-YYYY',
        containerClass: 'theme-default',
        adaptivePosition: true,
        showWeekNumbers: false
      })
    }
  ],
  bootstrap: [App]
})
export class AppModule { }
