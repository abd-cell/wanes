import { Injectable, inject } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay } from 'rxjs';
import { ApiService } from '../api/api.service';
import { GlobalService } from './global.service';

/**
 * Owns the access-token renewal. Access tokens are short-lived by design, so a
 * 401 on a normal call is routine: this trades the stored refresh token for a
 * fresh pair and hands back the new access token.
 */
@Injectable({ providedIn: 'root' })
export class SessionRefreshService {
  private readonly api = inject(ApiService);
  private readonly global = inject(GlobalService);

  /** The refresh currently in flight, if any. */
  private inFlight: Observable<string | null> | null = null;

  /**
   * Renews the session, or resolves to null when it cannot be renewed.
   *
   * Everything that 401s while a refresh is running joins that one call: the
   * server rotates the refresh token on use, so a second concurrent refresh
   * would come back holding a token that has already been retired.
   */
  refresh(): Observable<string | null> {
    if (this.inFlight) return this.inFlight;

    const refreshToken = this.global.refreshToken;
    if (!refreshToken) return of(null);

    this.inFlight = this.api.refreshSession(refreshToken).pipe(
      map((res) => {
        if (!res.success || !res.data?.token) return null;
        this.global.setSession(res.data.token, res.data.refreshToken, res.data.profile);
        return res.data.token;
      }),
      catchError(() => of(null)),
      finalize(() => (this.inFlight = null)),
      shareReplay(1),
    );

    return this.inFlight;
  }
}
