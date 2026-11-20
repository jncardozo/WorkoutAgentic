import { inject } from '@angular/core';
import type { Routes } from '@angular/router';
import { AuthApi, requireAccount } from './core/auth-api';
import type { PlanillaPage } from './features/planilla/planilla-page';

export const routes: Routes = [
  {
    path: 'legal/confidencialidad', data: { preview: true }, title: 'WorkoutAgentic · Acuerdo',
    loadComponent: () => import('./features/legal/agreement-page').then(m => m.AgreementPage),
  },
  {
    path: 'acuerdo-qa', canActivate: [requireAccount], data: { allowWithoutAgreement: true },
    title: 'WorkoutAgentic · Acuerdo',
    loadComponent: () => import('./features/legal/agreement-page').then(m => m.AgreementPage),
  },
  ...([
    ['login', 'login'], ['registro', 'register'], ['recuperar-clave', 'forgot'], ['restablecer-clave', 'reset'],
  ] as const).map(([path, mode]) => ({
    path, data: { mode }, title: 'WorkoutAgentic · Cuenta',
    loadComponent: () => import('./features/auth/auth-page').then(m => m.AuthPage),
  })),
  {
    path: '', pathMatch: 'full', canActivate: [requireAccount],
    resolve: { alumnoId: () => inject(AuthApi).account()!.studentId },
    loadComponent: () => import('./features/planilla/planilla-page').then(m => m.PlanillaPage),
    title: 'WorkoutAgentic · Planilla',
    canDeactivate: [(component: PlanillaPage) => component.canLeave()],
  },
  { path: 'alumno/:alumnoId/planilla', redirectTo: '' },
  { path: '**', redirectTo: '' },
];
