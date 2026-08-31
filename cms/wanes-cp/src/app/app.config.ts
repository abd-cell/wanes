import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideClientHydration } from '@angular/platform-browser';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { refreshInterceptor } from './core/interceptors/refresh.interceptor';
import { AppConfigService } from './core/services/app-config.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(),
    provideHttpClient(
      withFetch(),
      // Order matters on the way back: responses unwind in reverse, so the
      // refresh interceptor listed last gets first look at a 401 and can renew
      // the token before errorInterceptor treats it as the end of the session.
      withInterceptors([authInterceptor, errorInterceptor, refreshInterceptor]),
    ),
    // The admin-controlled brand colour has to land before the first paint,
    // otherwise every screen flashes the default palette on the way in. The
    // initializer resolves from cache first and never rejects, so a backend
    // that is down delays nothing.
    provideAppInitializer(() => inject(AppConfigService).load()),
  ],
};
