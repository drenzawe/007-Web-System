import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const customerGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = auth.user() ?? await auth.loadSession();
  return user ? true : router.createUrlTree(['/register']);
};

export const adminGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = auth.user() ?? await auth.loadSession();
  return user?.isAdmin ? true : router.createUrlTree(['/schedule']);
};
