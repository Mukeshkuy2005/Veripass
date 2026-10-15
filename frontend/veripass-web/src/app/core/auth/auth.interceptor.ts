import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.token();

  const user = authService.currentUser();
  let headers = req.headers;

  if (token && !token.includes('demo_token')) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  if (user?.currentOrganizationId) {
    headers = headers.set('X-Organization-Id', user.currentOrganizationId);
  }

  const authReq = req.clone({ headers });
  return next(authReq);
};
