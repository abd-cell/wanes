import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { GlobalService } from '../services/global.service';
import { TranslationService } from '../services/translation.service';
import { SKIP_AUTH_HANDLING } from './auth-context';

/**
 * Toasts transport errors; on 401/403 clears the session and returns to login.
 * 403 means the account is signed in but lacks the Admin role every admin/*
 * endpoint requires — the backend sends the reason in the envelope `message`,
 * so show that and fall back to our own copy if the body is empty.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const global = inject(GlobalService);
  const translation = inject(TranslationService);
  const router = inject(Router);

  // The refresh call carries the session's fate on its own: whatever it returns
  // reaches the caller that triggered it, and toasting here would double up.
  if (req.context.get(SKIP_AUTH_HANDLING)) return next(req);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 || err.status === 403) {
        const fallback = err.status === 403 ? 'error_forbidden' : 'error_session_expired';
        global.errorMsg(serverMessage(err) ?? translation.translate(fallback));
        global.clearSession();
        router.navigate(['/', translation.lang(), 'login']);
      } else {
        global.errorMsg(translation.translate('error_generic'));
      }
      return throwError(() => err);
    }),
  );
};

/** The `message` off a BaseResponse body, when the server sent one. */
function serverMessage(err: HttpErrorResponse): string | null {
  const body = err.error as { message?: unknown } | string | null;
  if (body && typeof body === 'object' && typeof body.message === 'string' && body.message)
    return body.message;
  return null;
}
