import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth-service';

export const adminGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAdmin()) {
    return true;
  }
  if (auth.user()) {
    return router.createUrlTree(['/']);
  }
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
