import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { environment } from '../../environment';
import { Profile, Roles } from '../api/models';

export interface Toast {
  id: number;
  text: string;
  kind: 'success' | 'error';
}

/** Session, language, and toast state. SSR-safe (guards localStorage access). */
@Injectable({ providedIn: 'root' })
export class GlobalService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);

  private readonly tokenKey = environment.APP_NAME;
  private readonly userKey = environment.APP_NAME + 'UserData';

  readonly toasts = signal<Toast[]>([]);
  readonly language = signal<'en' | 'ar'>('en');
  private toastSeq = 0;

  // ── session ──
  get token(): string | null {
    return this.isBrowser ? localStorage.getItem(this.tokenKey) : null;
  }

  get user(): Profile | null {
    if (!this.isBrowser) return null;
    const raw = localStorage.getItem(this.userKey);
    return raw ? (JSON.parse(raw) as Profile) : null;
  }

  get isLoggedIn(): boolean {
    return !!this.token;
  }

  /** Roles on the stored profile, or null when the API reported none. */
  private get roles(): Roles[] | null {
    const roles = this.user?.roles;
    return roles?.length ? roles : null;
  }

  /** The control panel is Admin-only; every admin/* call 403s without this role. */
  get isAdmin(): boolean {
    return !!this.roles?.includes(Roles.Admin);
  }

  /**
   * True only when the profile positively says the account holds no Admin role.
   * An API that reports no roles at all leaves this false — the backend 403 is
   * the real gate, this check only saves the round trip.
   */
  get isKnownNonAdmin(): boolean {
    const roles = this.roles;
    return !!roles && !roles.includes(Roles.Admin);
  }

  setSession(token: string, user: Profile): void {
    if (!this.isBrowser) return;
    localStorage.setItem(this.tokenKey, token);
    localStorage.setItem(this.userKey, JSON.stringify(user));
  }

  clearSession(): void {
    if (!this.isBrowser) return;
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
  }

  // ── toasts ──
  successMsg(text: string): void { this.push(text, 'success'); }
  errorMsg(text: string): void { this.push(text, 'error'); }

  private push(text: string, kind: Toast['kind']): void {
    const id = ++this.toastSeq;
    this.toasts.update((list) => [...list, { id, text, kind }]);
    if (this.isBrowser) setTimeout(() => this.dismiss(id), 3500);
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }
}
