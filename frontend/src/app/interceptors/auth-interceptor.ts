import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { AuthService } from '../services/auth-service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (req.url.endsWith('/auth/login')) {
    return next(req);
  }

  const user = auth.user();
  const request = user ? req.clone({ setHeaders: { Authorization: `Bearer ${user.token}` } }) : req;

  return next(request).pipe(
    tap({
      error: (error: HttpErrorResponse) => {
        if (error.status === 401) {
          auth.logout();
          router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
        }
      },
    }),
  );
};
