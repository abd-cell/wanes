import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environment';
import { ApiService } from '../api/api.service';
import { AppConfiguration, AppFont, CurrencyPosition } from '../api/models';
import { ensureFontStylesheet, fontStack, rtlFontStack } from './app-font';
import { deriveBrand } from './brand-color';

/** What the CMS falls back to before (or instead of) an answer from the API. */
export const DEFAULT_CONFIG: AppConfiguration = {
  currencyCode: 'JOD',
  currencySymbol: 'د.أ',
  currencyPosition: CurrencyPosition.After,
  currencyDecimals: 3,
  primaryColor: '#0FAE9E',
  fontFamily: AppFont.Jakarta,
  hailRequestTtlMinutes: 10,
  fareBaseAmount: 2.5,
  farePerKm: 1.2,
  supportPhone: '',
  supportWhatsApp: '',
  supportEmail: '',
  supportWebsite: '',
  supportHours: '',
};

/**
 * Platform settings, applied to the running CMS.
 *
 * Loaded once at bootstrap and mirrored into localStorage, so a reload repaints
 * the brand from the cache immediately rather than flashing the default teal
 * while the request is in flight. The settings screen calls [[save]], which
 * re-applies straight away — no reload needed to see a colour change.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly api = inject(ApiService);
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly cacheKey = environment.APP_NAME + 'Config';
  private static readonly styleId = 'wanes-brand';
  private static readonly fontStyleId = 'wanes-font';
  private static readonly fontLinkId = 'wanes-font-webfont';

  readonly config = signal<AppConfiguration>(DEFAULT_CONFIG);

  /**
   * Paints the cached brand, then refreshes from the API. Never rejects: the
   * control panel has to come up even when the backend is down, just in the
   * colours it last saw.
   */
  async load(): Promise<void> {
    if (!this.isBrowser) return;

    const cached = this.readCache();
    if (cached) this.adopt(cached, false);

    try {
      const res = await firstValueFrom(this.api.configuration());
      if (res.success && res.data) this.adopt(res.data, true);
    } catch {
      /* offline or backend down — the cache (or the default) stands */
    }
  }

  /** Persists the admin's changes and repaints the CMS with them. */
  async save(next: AppConfiguration): Promise<AppConfiguration | null> {
    const res = await firstValueFrom(this.api.updateConfiguration(next));
    if (!res.success || !res.data) return null;
    this.adopt(res.data, true);
    return res.data;
  }

  // ── money ──

  /**
   * An amount in the configured currency. Grouping and digit shaping come from
   * the active locale; the symbol and its side come from the settings.
   */
  format(amount: number | null | undefined, locale = 'en'): string {
    if (amount === null || amount === undefined || Number.isNaN(amount)) return '—';
    const c = this.config();
    const digits = Math.min(3, Math.max(0, c.currencyDecimals));
    const text = new Intl.NumberFormat(locale, {
      minimumFractionDigits: digits,
      maximumFractionDigits: digits,
    }).format(amount);

    return c.currencyPosition === CurrencyPosition.After
      ? `${text} ${c.currencySymbol}`
      : `${c.currencySymbol}${text}`;
  }

  // ── internals ──

  private adopt(config: AppConfiguration, cache: boolean): void {
    this.config.set(config);
    this.applyBrand(config.primaryColor);
    this.applyFont(config.fontFamily);
    if (cache) this.writeCache(config);
  }

  /**
   * Writes the derived shades as a stylesheet rather than inline styles on
   * `<html>`: the dark palette lives behind a `[data-theme='dark']` selector,
   * which an inline style cannot express, and one rule keeps both themes in step.
   */
  private applyBrand(primary: string): void {
    if (!this.isBrowser) return;
    const b = deriveBrand(primary);

    let style = this.document.getElementById(AppConfigService.styleId) as HTMLStyleElement | null;
    if (!style) {
      style = this.document.createElement('style');
      style.id = AppConfigService.styleId;
      this.document.head.appendChild(style);
    }
    style.textContent = `
:root {
  --app-primary: ${b.primary};
  --app-primary-deep: ${b.primaryDeep};
  --app-on-primary: ${b.onPrimary};
}
[data-theme='dark'] {
  --app-primary: ${b.darkPrimary};
  --app-primary-deep: ${b.darkPrimaryDeep};
  --app-on-primary: ${b.onPrimary};
}`.trim();
  }

  /**
   * Points `--app-font` at the configured stack and requests the faces it needs.
   *
   * Two elements rather than one because they do different jobs: the `<link>`
   * fetches the faces, and the rule tells the page to use them — including the
   * RTL variant, which leads with the Arabic face and so cannot be expressed as
   * a single value.
   */
  private applyFont(font: AppFont): void {
    if (!this.isBrowser) return;
    ensureFontStylesheet(this.document, font, AppConfigService.fontLinkId);

    let style = this.document.getElementById(
      AppConfigService.fontStyleId,
    ) as HTMLStyleElement | null;
    if (!style) {
      style = this.document.createElement('style');
      style.id = AppConfigService.fontStyleId;
      this.document.head.appendChild(style);
    }
    style.textContent = `
:root { --app-font: ${fontStack(font)}; }
[dir='rtl'] { --app-font: ${rtlFontStack(font)}; }`.trim();
  }

  private readCache(): AppConfiguration | null {
    try {
      const raw = localStorage.getItem(this.cacheKey);
      return raw ? { ...DEFAULT_CONFIG, ...(JSON.parse(raw) as AppConfiguration) } : null;
    } catch {
      return null;
    }
  }

  private writeCache(config: AppConfiguration): void {
    try {
      localStorage.setItem(this.cacheKey, JSON.stringify(config));
    } catch {
      /* private mode / quota — the brand still applied for this session */
    }
  }
}
