import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { SessionRefreshService } from '../services/session-refresh.service';
import { SKIP_AUTH_HANDLING } from './auth-context';

/**
 * Renews an expired access token and replays the call that hit the 401.
 *
 * Registered last, because responses travel back up the chain in reverse: this
 * has to see the 401 before {@link errorInterceptor} does, or a routine token
 * expiry would sign the admin out instead of being repaired silently. When the
 * refresh yields nothing the original 401 carries on to that interceptor, which
 * clears the session and returns to login.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const sessionRefresh = inject(SessionRefreshService);

  if (req.context.get(SKIP_AUTH_HANDLING)) return next(req);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401) return throwError(() => err);

      return sessionRefresh.refresh().pipe(
        switchMap((token) => {
          if (!token) return throwError(() => err);
          // The retry re-enters the chain below authInterceptor, so the new
          // token is attached here rather than inherited from the first attempt.
          return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
        }),
      );
    }),
  );
};
