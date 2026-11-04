import type { Routes } from '@angular/router';

// Identidad de prueba temporal. El login futuro resolverá este id desde la
// identidad autenticada; por ahora todos los dispositivos usan el alumno #1.
const ALUMNO_ID_DE_PRUEBA = 1;

export const routes: Routes = [
  {
    path: 'alumno/:alumnoId/planilla',
    loadComponent: () => import('./features/planilla/planilla-page').then((m) => m.PlanillaPage),
    title: 'WorkoutAgentic · Planilla',
  },
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/planilla/planilla-page').then((m) => m.PlanillaPage),
    title: 'WorkoutAgentic · Planilla',
    data: { alumnoId: ALUMNO_ID_DE_PRUEBA },
  },
  { path: '**', redirectTo: '' },
];
