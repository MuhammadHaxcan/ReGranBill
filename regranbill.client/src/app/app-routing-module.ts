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
import { RoleManagementComponent } from './pages/role-management/role-management.component';
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
import { RatedVouchersComponent } from './pages/rated-vouchers/rated-vouchers.component';
import { CustomerLedgerComponent } from './pages/customer-ledger/customer-ledger.component';
import { AuthGuard } from './guards/auth.guard';
import { PageAccessGuard } from './guards/page-access.guard';

const guarded = [AuthGuard, PageAccessGuard];

const routes: Routes = [
  { path: 'login', component: LoginComponent },

  // Delivery Challan + supporting routes
  { path: 'delivery-challan',          component: DeliveryChallanComponent,    canActivate: guarded, data: { pageKey: 'delivery-challan' } },
  { path: 'delivery-challan/:id',      component: DeliveryChallanComponent,    canActivate: guarded, data: { pageKey: 'delivery-challan' } },
  { path: 'print-dc/:id',              component: PrintDcComponent,            canActivate: guarded, data: { pageKey: 'delivery-challan' } },
  { path: 'add-rate/:id',              component: AddRateComponent,            canActivate: guarded, data: { pageKey: 'voucher-rates' } },
  { path: 'pending-challans',          component: PendingChallansComponent,    canActivate: guarded, data: { pageKey: 'delivery-challan' } },

  // Purchase Voucher + supporting routes
  { path: 'purchase-voucher',          component: PurchaseVoucherComponent,    canActivate: guarded, data: { pageKey: 'purchase-voucher' } },
  { path: 'purchase-voucher/:id',      component: PurchaseVoucherComponent,    canActivate: guarded, data: { pageKey: 'purchase-voucher' } },
  { path: 'print-pv/:id',              component: PrintPvComponent,            canActivate: guarded, data: { pageKey: 'purchase-voucher' } },
  { path: 'add-purchase-rate/:id',     component: AddPurchaseRateComponent,    canActivate: guarded, data: { pageKey: 'voucher-rates' } },
  { path: 'pending-purchases',         component: PendingPurchasesComponent,   canActivate: guarded, data: { pageKey: 'purchase-voucher' } },

  // Sale Return + supporting routes
  { path: 'sale-return',               component: SaleReturnComponent,         canActivate: guarded, data: { pageKey: 'sale-return' } },
  { path: 'sale-return/:id',           component: SaleReturnComponent,         canActivate: guarded, data: { pageKey: 'sale-return' } },
  { path: 'print-sr/:id',              component: PrintSrComponent,            canActivate: guarded, data: { pageKey: 'sale-return' } },
  { path: 'add-sale-return-rate/:id',  component: AddSaleReturnRateComponent,  canActivate: guarded, data: { pageKey: 'voucher-rates' } },
  { path: 'pending-sale-returns',      component: PendingSaleReturnsComponent, canActivate: guarded, data: { pageKey: 'sale-return' } },

  // Purchase Return + supporting routes
  { path: 'purchase-return',           component: PurchaseReturnComponent,         canActivate: guarded, data: { pageKey: 'purchase-return' } },
  { path: 'purchase-return/:id',       component: PurchaseReturnComponent,         canActivate: guarded, data: { pageKey: 'purchase-return' } },
  { path: 'print-pr/:id',              component: PrintPrComponent,                canActivate: guarded, data: { pageKey: 'purchase-return' } },
  { path: 'add-purchase-return-rate/:id', component: AddPurchaseReturnRateComponent, canActivate: guarded, data: { pageKey: 'voucher-rates' } },
  { path: 'pending-purchase-returns',  component: PendingPurchaseReturnsComponent, canActivate: guarded, data: { pageKey: 'purchase-return' } },

  // Cash voucher (split into receipt vs payment by data.mode)
  { path: 'receipt-voucher',           component: CashVoucherComponent, canActivate: guarded, data: { pageKey: 'receipt-voucher', mode: 'receipt' } },
  { path: 'receipt-voucher/:id',       component: CashVoucherComponent, canActivate: guarded, data: { pageKey: 'receipt-voucher', mode: 'receipt' } },
  { path: 'payment-voucher',           component: CashVoucherComponent, canActivate: guarded, data: { pageKey: 'payment-voucher', mode: 'payment' } },
  { path: 'payment-voucher/:id',       component: CashVoucherComponent, canActivate: guarded, data: { pageKey: 'payment-voucher', mode: 'payment' } },

  // Journal voucher
  { path: 'journal-voucher',           component: JournalVoucherComponent,     canActivate: guarded, data: { pageKey: 'journal-voucher' } },
  { path: 'journal-voucher/:id',       component: JournalVoucherComponent,     canActivate: guarded, data: { pageKey: 'journal-voucher' } },

  // Review group
  { path: 'pending',                   component: PendingComponent,            canActivate: guarded, data: { pageKey: 'pending' } },
  { path: 'rated-vouchers',            component: RatedVouchersComponent,      canActivate: guarded, data: { pageKey: 'rated-vouchers' } },
  { path: 'voucher-editor',            component: VoucherEditorComponent,      canActivate: guarded, data: { pageKey: 'voucher-editor' } },

  // Reports
  { path: 'soa',                       component: SoaComponent,                canActivate: guarded, data: { pageKey: 'soa' } },
  { path: 'soa/:accountId',            component: SoaComponent,                canActivate: guarded, data: { pageKey: 'soa' } },
  { path: 'print-soa/:accountId',      component: PrintSoaComponent,           canActivate: guarded, data: { pageKey: 'soa' } },
  { path: 'customer-ledger',           component: CustomerLedgerComponent,     canActivate: guarded, data: { pageKey: 'customer-ledger' } },
  { path: 'print-customer-ledger/:accountId', component: PrintCustomerLedgerComponent, canActivate: guarded, data: { pageKey: 'customer-ledger' } },
  { path: 'master-report',             component: MasterReportComponent,       canActivate: guarded, data: { pageKey: 'master-report' } },
  { path: 'print-master-report',       component: PrintMasterReportComponent,  canActivate: guarded, data: { pageKey: 'master-report' } },
  { path: 'account-closing-report',    component: AccountClosingReportComponent, canActivate: guarded, data: { pageKey: 'account-closing-report' } },
  { path: 'print-account-closing-report', component: PrintAccountClosingReportComponent, canActivate: guarded, data: { pageKey: 'account-closing-report' } },
  { path: 'sale-purchase-report',      component: SalePurchaseReportComponent, canActivate: guarded, data: { pageKey: 'sale-purchase-report' } },
  { path: 'product-stock-report',      component: ProductStockReportComponent, canActivate: guarded, data: { pageKey: 'product-stock-report' } },
  { path: 'print-product-stock-report', component: PrintProductStockReportComponent, canActivate: guarded, data: { pageKey: 'product-stock-report' } },

  // Masters
  { path: 'metadata',                  component: CategoriesAccountsComponent, canActivate: guarded, data: { pageKey: 'metadata' } },

  // Admin
  { path: 'users',                     component: UserManagementComponent,     canActivate: guarded, data: { pageKey: 'users' } },
  { path: 'roles',                     component: RoleManagementComponent,     canActivate: guarded, data: { pageKey: 'roles' } },
  { path: 'company-settings',          component: CompanySettingsComponent,    canActivate: guarded, data: { pageKey: 'company-settings' } },

  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'login' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
