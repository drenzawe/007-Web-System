import { Routes } from '@angular/router';
import { HomePage } from './home/home';
import { Schedule } from './schedule/schedule';
import { adminGuard, customerGuard } from './auth/auth.guards';
import { Register } from './auth/register';
import { ConfirmEmail } from './auth/confirm-email';
import { AdminPanel } from './admin/admin';
import { MyReservations } from './auth/my-reservations';

export const routes: Routes = [
  { path: '', component: HomePage },
  { path: 'schedule', component: Schedule },
  { path: 'register', component: Register },
  { path: 'confirm-email', component: ConfirmEmail },
  { path: 'my-reservations', component: MyReservations, canActivate: [customerGuard] },
  { path: 'admin', component: AdminPanel, canActivate: [adminGuard] },
  { path: '**', redirectTo: '' },
];
