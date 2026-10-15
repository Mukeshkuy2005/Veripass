import { inject } from '@angular/core';
import { Routes, Router } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { PublicLayoutComponent } from './features/public/public-layout.component';
import { SupplierLayoutComponent } from './layouts/supplier-layout/supplier-layout.component';
import { BuyerLayoutComponent } from './layouts/buyer-layout/buyer-layout.component';
import { VerifierLayoutComponent } from './layouts/verifier-layout/verifier-layout.component';
import { AdminLayoutComponent } from './layouts/admin-layout/admin-layout.component';

export const routes: Routes = [
  // ==========================================
  // 1. PUBLIC WEBSITE (VpHeader & VpFooter)
  // ==========================================
  {
    path: '',
    component: PublicLayoutComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./features/public/home/home.component').then(m => m.HomeComponent)
      },
      {
        path: 'how-it-works',
        loadComponent: () => import('./features/public/how-it-works/how-it-works.component').then(m => m.HowItWorksComponent)
      },
      {
        path: 'verify',
        loadComponent: () => import('./features/public/verify-business/verify-business.component').then(m => m.VerifyBusinessComponent)
      },
      {
        path: 'verify-business',
        redirectTo: 'verify',
        pathMatch: 'full'
      },
      {
        path: 'business/:passportId',
        loadComponent: () => import('./features/public/public-passport/public-passport.component').then(m => m.PublicPassportComponent)
      },
      {
        path: 'passport/:passportId',
        redirectTo: 'business/:passportId',
        pathMatch: 'full'
      },
      {
        path: 'about',
        loadComponent: () => import('./features/public/about/about.component').then(m => m.AboutComponent)
      },
      {
        path: 'security',
        loadComponent: () => import('./features/public/security/security.component').then(m => m.SecurityComponent)
      },
      {
        path: 'help',
        loadComponent: () => import('./features/public/help/help.component').then(m => m.HelpComponent)
      },
      {
        path: 'faq',
        redirectTo: 'help',
        pathMatch: 'full'
      },
      {
        path: 'contact',
        loadComponent: () => import('./features/public/contact/contact.component').then(m => m.ContactComponent)
      }
    ]
  },

  // ==========================================
  // 2. AUTHENTICATION & ONBOARDING ROUTES
  // ==========================================
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/supplier-register.component').then(m => m.SupplierRegisterComponent)
  },
  {
    path: 'auth/register',
    loadComponent: () => import('./features/auth/register/supplier-register.component').then(m => m.SupplierRegisterComponent)
  },
  {
    path: 'auth/login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'auth/register-supplier',
    loadComponent: () => import('./features/auth/register/supplier-register.component').then(m => m.SupplierRegisterComponent)
  },
  {
    path: 'auth/register-buyer',
    loadComponent: () => import('./features/auth/register/supplier-register.component').then(m => m.SupplierRegisterComponent)
  },
  {
    path: 'buyer/register',
    loadComponent: () => import('./features/auth/register/supplier-register.component').then(m => m.SupplierRegisterComponent)
  },
  {
    path: 'buyer/login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'auth/change-password',
    loadComponent: () => import('./features/auth/change-password/change-password.component').then(m => m.ChangePasswordComponent)
  },
  {
    path: 'change-password',
    redirectTo: 'auth/change-password',
    pathMatch: 'full'
  },

  // ==========================================
  // 3. AUTHENTICATED SUPPLIER WORKSPACE
  // ==========================================
  {
    path: 'supplier',
    component: SupplierLayoutComponent,
    canActivate: [authGuard],
    data: { roles: ['SUPPLIER', 'ADMIN'] },
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/supplier/dashboard/supplier-dashboard.component').then(m => m.SupplierDashboardComponent)
      },
      {
        path: 'profile',
        redirectTo: () => {
          const router = inject(Router);
          return router.createUrlTree(['/supplier/settings'], { queryParams: { tab: 'business-profile' } });
        }
      },
      {
        path: 'identity',
        redirectTo: () => {
          const router = inject(Router);
          return router.createUrlTree(['/supplier/settings'], { queryParams: { tab: 'identity-kyc' } });
        }
      },
      {
        path: 'documents',
        loadComponent: () => import('./features/supplier/documents/supplier-documents.component').then(m => m.SupplierDocumentsComponent)
      },
      {
        path: 'verification',
        loadComponent: () => import('./features/supplier/verification/supplier-verification.component').then(m => m.SupplierVerificationComponent)
      },
      {
        path: 'compliance',
        loadComponent: () => import('./features/supplier/compliance/supplier-compliance.component').then(m => m.SupplierComplianceComponent)
      },
      {
        path: 'compliance/calendar',
        loadComponent: () => import('./features/supplier/compliance/compliance-calendar.component').then(m => m.ComplianceCalendarComponent)
      },
      {
        path: 'passport',
        loadComponent: () => import('./features/supplier/passport/supplier-passport.component').then(m => m.SupplierPassportComponent)
      },
      {
        path: 'passport/preview',
        loadComponent: () => import('./features/supplier/passport/supplier-passport.component').then(m => m.SupplierPassportComponent)
      },
      {
        path: 'passport/share',
        loadComponent: () => import('./features/supplier/passport/supplier-passport.component').then(m => m.SupplierPassportComponent)
      },
      {
        path: 'requests',
        loadComponent: () => import('./features/supplier/buyer-requests/buyer-requests.component').then(m => m.BuyerRequestsComponent)
      },
      {
        path: 'buyer-requests',
        redirectTo: 'requests',
        pathMatch: 'full'
      },
      {
        path: 'history',
        loadComponent: () => import('./features/supplier/history/verification-history.component').then(m => m.VerificationHistoryComponent)
      },
      {
        path: 'notifications',
        loadComponent: () => import('./features/supplier/notifications/supplier-notifications.component').then(m => m.SupplierNotificationsComponent)
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/supplier/settings/supplier-settings.component').then(m => m.SupplierSettingsComponent)
      }
    ]
  },

  // Aliases for Direct Nav / Spec Compatibility
  { path: 'dashboard', redirectTo: 'supplier/dashboard', pathMatch: 'full' },
  { path: 'onboarding/business-profile', redirectTo: 'supplier/profile', pathMatch: 'full' },
  { path: 'business/identity', redirectTo: 'supplier/identity', pathMatch: 'full' },
  { path: 'business/documents', redirectTo: 'supplier/documents', pathMatch: 'full' },
  { path: 'business/verification', redirectTo: 'supplier/verification', pathMatch: 'full' },
  { path: 'business/compliance', redirectTo: 'supplier/compliance', pathMatch: 'full' },
  { path: 'business/compliance/calendar', redirectTo: 'supplier/compliance/calendar', pathMatch: 'full' },
  { path: 'business/passport', redirectTo: 'supplier/passport', pathMatch: 'full' },
  { path: 'business/passport/preview', redirectTo: 'supplier/passport', pathMatch: 'full' },
  { path: 'business/passport/share', redirectTo: 'supplier/passport/share', pathMatch: 'full' },
  { path: 'business/requests', redirectTo: 'supplier/requests', pathMatch: 'full' },
  { path: 'business/history', redirectTo: 'supplier/history', pathMatch: 'full' },
  { path: 'notifications', redirectTo: 'supplier/notifications', pathMatch: 'full' },
  { path: 'settings', redirectTo: 'supplier/settings', pathMatch: 'full' },

  // ==========================================
  // 4. BUYER WORKSPACE (BuyerLayoutComponent)
  // ==========================================
  {
    path: 'buyer',
    component: BuyerLayoutComponent,
    canActivate: [authGuard],
    data: { roles: ['BUYER', 'ADMIN'] },
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/buyer/dashboard/buyer-dashboard.component').then(m => m.BuyerDashboardComponent)
      },
      {
        path: 'discover',
        redirectTo: 'suppliers',
        pathMatch: 'full'
      },
      {
        path: 'discovery',
        redirectTo: 'suppliers',
        pathMatch: 'full'
      },
      {
        path: 'suppliers',
        loadComponent: () => import('./features/buyer/suppliers/supplier-search-results.component').then(m => m.SupplierSearchResultsComponent)
      },
      {
        path: 'suppliers/:supplierId/passport',
        loadComponent: () => import('./features/buyer/suppliers/buyer-passport-viewer.component').then(m => m.BuyerPassportViewerComponent)
      },
      {
        path: 'compare',
        loadComponent: () => import('./features/buyer/comparison/supplier-comparison.component').then(m => m.SupplierComparisonComponent)
      },
      {
        path: 'shortlist',
        loadComponent: () => import('./features/buyer/shortlist/buyer-shortlist.component').then(m => m.BuyerShortlistComponent)
      },
      {
        path: 'verification-requests',
        loadComponent: () => import('./features/buyer/verification-requests/buyer-verification-requests.component').then(m => m.BuyerVerificationRequestsComponent)
      },
      {
        path: 'onboarding',
        loadComponent: () => import('./features/buyer/onboarding/buyer-onboarding.component').then(m => m.BuyerOnboardingComponent)
      },
      {
        path: 'onboarding/:id',
        loadComponent: () => import('./features/buyer/onboarding/buyer-onboarding-detail.component').then(m => m.BuyerOnboardingDetailComponent)
      },
      {
        path: 'activity',
        loadComponent: () => import('./features/buyer/activity/buyer-activity.component').then(m => m.BuyerActivityComponent)
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/buyer/settings/buyer-settings.component').then(m => m.BuyerSettingsComponent)
      }
    ]
  },
  // Buyer App Namespace Aliases (Section 3 Spec)
  { path: 'buyer/app', redirectTo: 'buyer/dashboard', pathMatch: 'full' },
  { path: 'buyer/app/dashboard', redirectTo: 'buyer/dashboard', pathMatch: 'full' },
  { path: 'buyer/app/discover', redirectTo: 'buyer/suppliers', pathMatch: 'full' },
  { path: 'buyer/app/suppliers', redirectTo: 'buyer/suppliers', pathMatch: 'full' },
  { path: 'buyer/app/compare', redirectTo: 'buyer/compare', pathMatch: 'full' },
  { path: 'buyer/app/shortlist', redirectTo: 'buyer/shortlist', pathMatch: 'full' },
  { path: 'buyer/app/verification-requests', redirectTo: 'buyer/verification-requests', pathMatch: 'full' },
  { path: 'buyer/app/onboarding', redirectTo: 'buyer/onboarding', pathMatch: 'full' },
  { path: 'buyer/app/onboarding/:id', redirectTo: 'buyer/onboarding/:id', pathMatch: 'full' },
  { path: 'buyer/app/activity', redirectTo: 'buyer/activity', pathMatch: 'full' },
  { path: 'buyer/app/settings', redirectTo: 'buyer/settings', pathMatch: 'full' },

  // ==========================================
  // 5. VERIFIER WORKSPACE ROUTES (VerifierLayoutComponent)
  // ==========================================
  {
    path: 'verifier',
    component: VerifierLayoutComponent,
    canActivate: [authGuard],
    data: { roles: ['VERIFIER', 'ADMIN'] },
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/verifier/dashboard/verifier-dashboard.component').then(m => m.VerifierDashboardComponent)
      },
      {
        path: 'queue',
        loadComponent: () => import('./features/verifier/queue/verifier-queue.component').then(m => m.VerifierQueueComponent)
      },
      {
        path: 'cases/:caseId',
        loadComponent: () => import('./features/verifier/cases/case-detail/verification-case.component').then(m => m.VerificationCaseComponent)
      },
      {
        path: 'cases/:caseId/review',
        redirectTo: 'cases/:caseId',
        pathMatch: 'full'
      },
      {
        path: 'cases/:caseId/document',
        loadComponent: () => import('./features/verifier/cases/document-review/document-review.component').then(m => m.DocumentReviewComponent)
      },
      {
        path: 'cases/:caseId/decision',
        redirectTo: 'cases/:caseId',
        pathMatch: 'full'
      },
      {
        path: 'cases/:caseId/risk',
        loadComponent: () => import('./features/verifier/risk/risk-review.component').then(m => m.RiskReviewComponent)
      },
      {
        path: 'risk',
        loadComponent: () => import('./features/verifier/risk/risk-review.component').then(m => m.RiskReviewComponent)
      },
      {
        path: 'history',
        loadComponent: () => import('./features/verifier/history/verification-history.component').then(m => m.VerificationHistoryComponent)
      },
      {
        path: 'escalated',
        loadComponent: () => import('./features/verifier/escalated/escalated-cases.component').then(m => m.EscalatedCasesComponent)
      },
      {
        path: 'profile',
        loadComponent: () => import('./features/verifier/profile/verifier-profile.component').then(m => m.VerifierProfileComponent)
      }
    ]
  },
  // Verifier App Namespace Aliases
  { path: 'verifier/app', redirectTo: 'verifier/dashboard', pathMatch: 'full' },
  { path: 'verifier/app/dashboard', redirectTo: 'verifier/dashboard', pathMatch: 'full' },
  { path: 'verifier/app/queue', redirectTo: 'verifier/queue', pathMatch: 'full' },
  { path: 'verifier/app/cases/:caseId', redirectTo: 'verifier/cases/:caseId', pathMatch: 'full' },
  { path: 'verifier/app/cases/:caseId/document', redirectTo: 'verifier/cases/:caseId/document', pathMatch: 'full' },
  { path: 'verifier/app/cases/:caseId/decision', redirectTo: 'verifier/cases/:caseId', pathMatch: 'full' },
  { path: 'verifier/app/cases/:caseId/risk', redirectTo: 'verifier/cases/:caseId/risk', pathMatch: 'full' },
  { path: 'verifier/app/risk', redirectTo: 'verifier/risk', pathMatch: 'full' },
  { path: 'verifier/app/history', redirectTo: 'verifier/history', pathMatch: 'full' },
  { path: 'verifier/app/escalated', redirectTo: 'verifier/escalated', pathMatch: 'full' },
  { path: 'verifier/app/profile', redirectTo: 'verifier/profile', pathMatch: 'full' },

  // ==========================================
  // 6. ADMIN CONSOLE & PLATFORM CONTROL PLANE
  // ==========================================
  {
    path: 'admin',
    component: AdminLayoutComponent,
    canActivate: [authGuard],
    data: { roles: ['ADMIN'] },
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/admin/dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent)
      },
      {
        path: 'businesses',
        loadComponent: () => import('./features/admin/businesses/business-management.component').then(m => m.BusinessManagementComponent)
      },
      {
        path: 'buyers',
        loadComponent: () => import('./features/admin/buyers/buyer-management.component').then(m => m.BuyerManagementComponent)
      },
      {
        path: 'verifiers',
        loadComponent: () => import('./features/admin/verifiers/verifier-management.component').then(m => m.VerifierManagementComponent)
      },
      {
        path: 'verifiers/:id',
        loadComponent: () => import('./features/admin/verifiers/verifier-detail.component').then(m => m.VerifierDetailComponent)
      },
      {
        path: 'verification',
        loadComponent: () => import('./features/admin/verification/verification-management.component').then(m => m.VerificationManagementComponent)
      },
      {
        path: 'verification-rules',
        loadComponent: () => import('./features/admin/verification-rules/verification-rules.component').then(m => m.VerificationRulesComponent)
      },
      {
        path: 'credentials',
        loadComponent: () => import('./features/admin/credentials/credential-management.component').then(m => m.CredentialManagementComponent)
      },
      {
        path: 'documents',
        loadComponent: () => import('./features/admin/documents/document-management.component').then(m => m.DocumentManagementComponent)
      },
      {
        path: 'risk',
        loadComponent: () => import('./features/admin/risk/admin-risk-management.component').then(m => m.AdminRiskManagementComponent)
      },
      {
        path: 'disputes',
        loadComponent: () => import('./features/admin/disputes/dispute-management.component').then(m => m.DisputeManagementComponent)
      },
      {
        path: 'audit',
        loadComponent: () => import('./features/admin/audit/audit-logs.component').then(m => m.AuditLogsComponent)
      },
      {
        path: 'reports',
        loadComponent: () => import('./features/admin/reports/reports-analytics.component').then(m => m.ReportsAnalyticsComponent)
      },
      {
        path: 'users',
        loadComponent: () => import('./features/admin/users/user-role-management.component').then(m => m.UserRoleManagementComponent)
      },
      {
        path: 'configuration',
        loadComponent: () => import('./features/admin/configuration/system-configuration.component').then(m => m.SystemConfigurationComponent)
      },
      {
        path: 'security',
        loadComponent: () => import('./features/admin/security/admin-security.component').then(m => m.AdminSecurityComponent)
      }
    ]
  },
  // Admin App Namespace Aliases
  { path: 'admin/app', redirectTo: 'admin/dashboard', pathMatch: 'full' },
  { path: 'admin/app/dashboard', redirectTo: 'admin/dashboard', pathMatch: 'full' },
  { path: 'admin/app/businesses', redirectTo: 'admin/businesses', pathMatch: 'full' },
  { path: 'admin/app/buyers', redirectTo: 'admin/buyers', pathMatch: 'full' },
  { path: 'admin/app/verifiers', redirectTo: 'admin/verifiers', pathMatch: 'full' },
  { path: 'admin/app/verifiers/:id', redirectTo: 'admin/verifiers/:id', pathMatch: 'full' },
  { path: 'admin/app/verification', redirectTo: 'admin/verification', pathMatch: 'full' },
  { path: 'admin/app/verification-rules', redirectTo: 'admin/verification-rules', pathMatch: 'full' },
  { path: 'admin/app/credentials', redirectTo: 'admin/credentials', pathMatch: 'full' },
  { path: 'admin/app/documents', redirectTo: 'admin/documents', pathMatch: 'full' },
  { path: 'admin/app/risk', redirectTo: 'admin/risk', pathMatch: 'full' },
  { path: 'admin/app/disputes', redirectTo: 'admin/disputes', pathMatch: 'full' },
  { path: 'admin/app/audit', redirectTo: 'admin/audit', pathMatch: 'full' },
  { path: 'admin/app/reports', redirectTo: 'admin/reports', pathMatch: 'full' },
  { path: 'admin/app/users', redirectTo: 'admin/users', pathMatch: 'full' },
  { path: 'admin/app/configuration', redirectTo: 'admin/configuration', pathMatch: 'full' },
  { path: 'admin/app/security', redirectTo: 'admin/security', pathMatch: 'full' },

  // Catch-all
  {
    path: '**',
    redirectTo: ''
  }
];
