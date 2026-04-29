import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LoginComponent } from './pages/login/login.component';
import { DeliveryChallanComponent } from './pages/delivery-challan/delivery-challan.component';
import { PurchaseVoucherComponent } from './pages/purchase-voucher/purchase-voucher.component';
import { CategoriesAccountsComponent } from './pages/categories-accounts/categories-accounts.component';
import { PendingChallansComponent } from './pages/pending-challans/pending-challans.component';
import { PendingPurchasesComponent } from './pages/pending-purchases/pending-purchases.component';
import { AddRateComponent } from './pages/add-rate/add-rate.component';
import { AddPurchaseRateComponent } from './pages/add-purchase-rate/add-purchase-rate.component';
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
import { PrintPrComponent } from './pages/print-pr/print-pr.component';
import { PrintCustomerLedgerComponent } from './pages/print-customer-ledger/print-customer-ledger.component';
import { PendingComponent } from './pages/pending/pending.component';
import { CustomerLedgerComponent } from './pages/customer-ledger/customer-ledger.component';
import { AuthGuard } from './guards/auth.guard';
import { AdminGuard } from './guards/admin.guard';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'print-dc/:id', component: PrintDcComponent, canActivate: [AuthGuard] },
  { path: 'print-pv/:id', component: PrintPvComponent, canActivate: [AuthGuard] },
  { path: 'print-soa/:accountId', component: PrintSoaComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'print-master-report', component: PrintMasterReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'print-account-closing-report', component: PrintAccountClosingReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'print-product-stock-report', component: PrintProductStockReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'print-sr/:id', component: PrintSrComponent, canActivate: [AuthGuard] },
  { path: 'sale-return', component: SaleReturnComponent, canActivate: [AuthGuard] },
  { path: 'sale-return/:id', component: SaleReturnComponent, canActivate: [AuthGuard] },
  { path: 'pending-sale-returns', component: PendingSaleReturnsComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'add-sale-return-rate/:id', component: AddSaleReturnRateComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'print-pr/:id', component: PrintPrComponent, canActivate: [AuthGuard] },
  { path: 'print-customer-ledger/:accountId', component: PrintCustomerLedgerComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'purchase-return', component: PurchaseReturnComponent, canActivate: [AuthGuard] },
  { path: 'purchase-return/:id', component: PurchaseReturnComponent, canActivate: [AuthGuard] },
  { path: 'pending-purchase-returns', component: PendingPurchaseReturnsComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'add-purchase-return-rate/:id', component: AddPurchaseReturnRateComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'customer-ledger', component: CustomerLedgerComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'pending', component: PendingComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'delivery-challan', component: DeliveryChallanComponent, canActivate: [AuthGuard] },
  { path: 'delivery-challan/:id', component: DeliveryChallanComponent, canActivate: [AuthGuard] },
  { path: 'journal-voucher', component: JournalVoucherComponent, canActivate: [AuthGuard] },
  { path: 'journal-voucher/:id', component: JournalVoucherComponent, canActivate: [AuthGuard] },
  { path: 'receipt-voucher', component: CashVoucherComponent, canActivate: [AuthGuard], data: { mode: 'receipt' } },
  { path: 'receipt-voucher/:id', component: CashVoucherComponent, canActivate: [AuthGuard], data: { mode: 'receipt' } },
  { path: 'payment-voucher', component: CashVoucherComponent, canActivate: [AuthGuard], data: { mode: 'payment' } },
  { path: 'payment-voucher/:id', component: CashVoucherComponent, canActivate: [AuthGuard], data: { mode: 'payment' } },
  { path: 'voucher-editor', component: VoucherEditorComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'purchase-voucher', component: PurchaseVoucherComponent, canActivate: [AuthGuard] },
  { path: 'purchase-voucher/:id', component: PurchaseVoucherComponent, canActivate: [AuthGuard] },
  { path: 'pending-challans', component: PendingChallansComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'pending-purchases', component: PendingPurchasesComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'add-rate/:id', component: AddRateComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'add-purchase-rate/:id', component: AddPurchaseRateComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'soa/:accountId', component: SoaComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'soa', component: SoaComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'master-report', component: MasterReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'account-closing-report', component: AccountClosingReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'sale-purchase-report', component: SalePurchaseReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'product-stock-report', component: ProductStockReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'metadata', component: CategoriesAccountsComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'company-settings', component: CompanySettingsComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'users', component: UserManagementComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: '', redirectTo: 'delivery-challan', pathMatch: 'full' },
  { path: '**', redirectTo: 'delivery-challan' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
