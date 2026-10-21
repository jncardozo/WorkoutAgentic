import type { Routes } from '@angular/router';

// Identidad provisional (hasta AuthApi, arquitectura S6): el alumno entra por
// una pantalla de entrada que guarda su id en localStorage y navega explícito.
// Con la cookie auth esto se apaga: la ruta resuelve el alumno logueado.
export const routes: Routes = [
  {
    path: 'entrada',
    loadComponent: () => import('./features/planilla/alumno-entrada').then((m) => m.AlumnoEntrada),
    title: 'WorkoutAgentic · ¿Quién carga?',
  },
  {
    path: 'alumno/:alumnoId/planilla',
    loadComponent: () => import('./features/planilla/planilla-page').then((m) => m.PlanillaPage),
    title: 'WorkoutAgentic · Planilla',
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: () => {
      const alumnoGuardado = localStorage.getItem('wa.alumnoId');
      return alumnoGuardado ? `/alumno/${alumnoGuardado}/planilla` : '/entrada';
    },
  },
  { path: '**', redirectTo: 'entrada' },
];
