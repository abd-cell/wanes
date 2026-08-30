import { PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CanActivateFn, Router } from '@angular/router';
import { GlobalService } from '../services/global.service';
import { TranslationService } from '../services/translation.service';

/**
 * Protects the authenticated shell. The session token lives in localStorage,
 * which the server cannot read, so on the server we defer the decision to the
 * browser (return true) instead of redirecting every deep-link to /login.
 */
export const appAuthGuard: CanActivateFn = (route) => {
  const global = inject(GlobalService);
  const translation = inject(TranslationService);
  const router = inject(Router);
  if (!isPlatformBrowser(inject(PLATFORM_ID))) return true;

  const lang = route.parent?.params['languageCode'] ?? 'en';

  // A stored session that is not an admin one can only collect 403s here, so
  // drop it at the door with the same message the API would have returned.
  if (global.isLoggedIn && global.isKnownNonAdmin) {
    global.errorMsg(translation.translate('error_forbidden'));
    global.clearSession();
    return router.createUrlTree(['/', lang, 'login']);
  }

  if (global.isLoggedIn) return true;

  return router.createUrlTree(['/', lang, 'login']);
};

/** Blocks the login page when already signed in. Deferred to the browser (see above). */
export const loggedInGuard: CanActivateFn = (route) => {
  const global = inject(GlobalService);
  const router = inject(Router);
  if (!isPlatformBrowser(inject(PLATFORM_ID))) return true;
  if (!global.isLoggedIn) return true;

  const lang = route.parent?.params['languageCode'] ?? 'en';
  return router.createUrlTree(['/', lang, 'dashboard']);
};

/** Validates the :languageCode segment. */
export const languageGuard: CanActivateFn = (route) => {
  const code = route.params['languageCode'];
  return code === 'en' || code === 'ar';
};
