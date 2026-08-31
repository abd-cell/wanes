import { HttpContextToken } from '@angular/common/http';

/**
 * Marks the token-refresh call itself, which must sit outside the 401 handling
 * the other interceptors apply — otherwise refreshing a dead session would try
 * to refresh again, and its 401 would be reported twice.
 */
export const SKIP_AUTH_HANDLING = new HttpContextToken<boolean>(() => false);
