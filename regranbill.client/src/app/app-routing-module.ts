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
import { AuthGuard } from './guards/auth.guard';
import { AdminGuard } from './guards/admin.guard';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'print-dc/:id', component: PrintDcComponent, canActivate: [AuthGuard] },
  { path: 'print-pv/:id', component: PrintPvComponent, canActivate: [AuthGuard] },
  { path: 'print-soa/:accountId', component: PrintSoaComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'delivery-challan', component: DeliveryChallanComponent, canActivate: [AuthGuard] },
  { path: 'delivery-challan/:id', component: DeliveryChallanComponent, canActivate: [AuthGuard] },
  { path: 'journal-voucher', component: JournalVoucherComponent, canActivate: [AuthGuard] },
  { path: 'journal-voucher/:id', component: JournalVoucherComponent, canActivate: [AuthGuard] },
  { path: 'receipt-voucher', component: CashVoucherComponent, canActivate: [AuthGuard], data: { mode: 'receipt' } },
  { path: 'receipt-voucher/:id', component: CashVoucherComponent, canActivate: [AuthGuard], data: { mode: 'receipt' } },
  { path: 'payment-voucher', component: CashVoucherComponent, canActivate: [AuthGuard], data: { mode: 'payment' } },
  { path: 'payment-voucher/:id', component: CashVoucherComponent, canActivate: [AuthGuard], data: { mode: 'payment' } },
  { path: 'voucher-editor', component: VoucherEditorComponent, canActivate: [AuthGuard] },
  { path: 'purchase-voucher', component: PurchaseVoucherComponent, canActivate: [AuthGuard] },
  { path: 'purchase-voucher/:id', component: PurchaseVoucherComponent, canActivate: [AuthGuard] },
  { path: 'pending-challans', component: PendingChallansComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'pending-purchases', component: PendingPurchasesComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'add-rate/:id', component: AddRateComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'add-purchase-rate/:id', component: AddPurchaseRateComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'soa/:accountId', component: SoaComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'soa', component: SoaComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'master-report', component: MasterReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'product-stock-report', component: ProductStockReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'metadata', component: CategoriesAccountsComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: '', redirectTo: 'delivery-challan', pathMatch: 'full' },
  { path: '**', redirectTo: 'delivery-challan' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
