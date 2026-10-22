import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { Role } from './models';

/** Lets in signed-in users with one of the roles; no roles means any signed-in user. */
export function roleGuard(...roles: Role[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isLoggedIn()) return router.parseUrl('/login');
    if (roles.length === 0 || auth.hasRole(...roles)) return true;
    return router.parseUrl(auth.homeUrl());
  };
}

/** "/" sends each role to its own start page. */
export const homeRedirect: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return router.parseUrl(auth.isLoggedIn() ? auth.homeUrl() : '/login');
};
