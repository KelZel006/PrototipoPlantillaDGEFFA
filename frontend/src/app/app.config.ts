import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { rutas } from './app.routes';
import { usuarioPruebaInterceptor } from './core/usuario-prueba.service';

registerLocaleData(localeEs);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch(), withInterceptors([usuarioPruebaInterceptor])),
    // Los parámetros de la ruta llegan como inputs de los componentes (por ejemplo, el id del consolidado).
    provideRouter(rutas, withComponentInputBinding()),
    { provide: LOCALE_ID, useValue: 'es' },
  ],
};
