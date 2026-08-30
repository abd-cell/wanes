import { Routes } from '@angular/router';
import { appAuthGuard, languageGuard, loggedInGuard } from './core/guard/guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'en' },
  {
    path: ':languageCode',
    canActivate: [languageGuard],
    loadComponent: () =>
      import('./components/lang-wrapper.component').then((m) => m.LangWrapperComponent),
    children: [
      {
        path: 'login',
        canActivate: [loggedInGuard],
        loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
        data: { title: { en: 'Sign in', ar: 'تسجيل الدخول' } },
      },
      {
        path: '',
        canActivate: [appAuthGuard],
        loadComponent: () =>
          import('./features/layout/main-layout.component').then((m) => m.MainLayoutComponent),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
          {
            path: 'dashboard',
            loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
          },
          {
            path: 'drivers',
            loadComponent: () => import('./features/drivers/drivers.component').then((m) => m.DriversComponent),
          },
          {
            path: 'audit',
            loadComponent: () => import('./features/audit/audit.component').then((m) => m.AuditComponent),
          },
          {
            path: 'data/:resource',
            loadComponent: () => import('./features/data/resource.component').then((m) => m.ResourceComponent),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: 'en' },
];
