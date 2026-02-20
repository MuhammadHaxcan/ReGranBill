import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LoginComponent } from './pages/login/login.component';
import { DeliveryChallanComponent } from './pages/delivery-challan/delivery-challan.component';
import { CategoriesAccountsComponent } from './pages/categories-accounts/categories-accounts.component';
import { PendingChallansComponent } from './pages/pending-challans/pending-challans.component';
import { AddRateComponent } from './pages/add-rate/add-rate.component';
import { SoaComponent } from './pages/soa/soa.component';
import { MasterReportComponent } from './pages/master-report/master-report.component';
import { AuthGuard } from './guards/auth.guard';
import { AdminGuard } from './guards/admin.guard';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'delivery-challan', component: DeliveryChallanComponent, canActivate: [AuthGuard] },
  { path: 'delivery-challan/:id', component: DeliveryChallanComponent, canActivate: [AuthGuard] },
  { path: 'pending-challans', component: PendingChallansComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'add-rate/:id', component: AddRateComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'soa/:accountId', component: SoaComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'soa', component: SoaComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'master-report', component: MasterReportComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: 'metadata', component: CategoriesAccountsComponent, canActivate: [AuthGuard, AdminGuard] },
  { path: '', redirectTo: 'delivery-challan', pathMatch: 'full' },
  { path: '**', redirectTo: 'delivery-challan' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
