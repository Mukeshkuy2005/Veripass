import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    if (state.url.startsWith('/verifier')) {
      authService.switchDemoRole('VERIFIER');
      return true;
    }
    if (state.url.startsWith('/buyer')) {
      authService.switchDemoRole('BUYER');
      return true;
    }
    if (state.url.startsWith('/supplier')) {
      authService.switchDemoRole('SUPPLIER');
      return true;
    }
    router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }

  const expectedRoles = route.data?.['roles'] as string[] | undefined;
  if (expectedRoles && expectedRoles.length > 0) {
    const userRole = authService.currentRole();
    if (!expectedRoles.includes(userRole) && userRole !== 'ADMIN') {
      if (expectedRoles.includes('VERIFIER') && state.url.startsWith('/verifier')) {
        authService.switchDemoRole('VERIFIER');
        return true;
      }
      if (expectedRoles.includes('BUYER') && state.url.startsWith('/buyer')) {
        authService.switchDemoRole('BUYER');
        return true;
      }
      router.navigate(['/']);
      return false;
    }
  }

  return true;
};
