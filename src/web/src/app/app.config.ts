import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { routes } from './app.routes';
import { FABRICA_RECONOCEDOR, fabricaDelNavegador } from './features/planilla/dictado-voz';

// Fechas y horas de la UI en español (variantes regionales del producto).
registerLocaleData(localeEs, 'es');

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
    provideRouter(routes, withComponentInputBinding()),
    { provide: LOCALE_ID, useValue: 'es' },
    // Dictado por voz (ADR-0011): el reconocedor real del navegador; en specs
    // se reemplaza por un falso por el token.
    { provide: FABRICA_RECONOCEDOR, useValue: fabricaDelNavegador },
  ],
};
