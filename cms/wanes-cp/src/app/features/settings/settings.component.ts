import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AppConfiguration, AppFont, CurrencyPosition } from '../../core/api/models';
import { AppConfigService, DEFAULT_CONFIG } from '../../core/services/app-config.service';
import { ensureFontStylesheet, fontStack, rtlFontStack } from '../../core/services/app-font';
import { deriveBrand } from '../../core/services/brand-color';
import { GlobalService } from '../../core/services/global.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/**
 * What the backend will accept for the hail window (MatchRules.Min/MaxHailTtlMinutes).
 * Mirrored here so the form shows the clamped figure the server would store
 * rather than whatever was typed.
 */
const HAIL_TTL_BOUNDS = {
  min: 1,
  max: 240,
  clamp: (value: number) => Math.min(240, Math.max(1, Math.round(value || 0))),
};

/**
 * Fare rates, bounded the way the server bounds them (`Trips.FareRules`).
 *
 * Zero is allowed on both: a flat flag-fall with no distance component, or a
 * fare that is all distance, are both real pricing choices. Negative is not —
 * it would price a long ride below a short one — and the ceiling is there so a
 * slipped decimal cannot list a trip at a fortune.
 */
/** The distance the fare preview quotes for — an ordinary city trip. */
const FARE_PREVIEW_KM = 10;

const FARE_RATE_BOUNDS = {
  min: 0,
  max: 1000,
  clamp: (value: number) => Math.min(1000, Math.max(0, Number(value) || 0)),
};

/** Ready-made brand colours, so an admin without a hex code still has good options. */
const PRESETS = ['#0FAE9E', '#2563EB', '#7C3AED', '#DB2777', '#E5484D', '#F97316', '#CA8A04', '#16A34A'];

/**
 * The typefaces on offer, in the order they are shown. Each is a Latin + Arabic
 * pairing — see `core/services/app-font.ts` for the faces behind each value.
 */
const FONTS: { value: AppFont; labelKey: string; faceKey: string }[] = [
  { value: AppFont.Jakarta, labelKey: 'cfg_font_jakarta', faceKey: 'cfg_font_jakarta_faces' },
  { value: AppFont.Inter, labelKey: 'cfg_font_inter', faceKey: 'cfg_font_inter_faces' },
  { value: AppFont.Rubik, labelKey: 'cfg_font_rubik', faceKey: 'cfg_font_rubik_faces' },
  { value: AppFont.Noto, labelKey: 'cfg_font_noto', faceKey: 'cfg_font_noto_faces' },
  { value: AppFont.Tajawal, labelKey: 'cfg_font_tajawal', faceKey: 'cfg_font_tajawal_faces' },
  { value: AppFont.System, labelKey: 'cfg_font_system', faceKey: 'cfg_font_system_faces' },
];

/** Currencies the platform is likely to run in. Picking one fills code + symbol + side. */
const CURRENCIES: { code: string; symbol: string; position: CurrencyPosition; decimals: number }[] = [
  { code: 'JOD', symbol: 'د.أ', position: CurrencyPosition.After, decimals: 3 },
  { code: 'SAR', symbol: 'ر.س', position: CurrencyPosition.After, decimals: 2 },
  { code: 'AED', symbol: 'د.إ', position: CurrencyPosition.After, decimals: 2 },
  { code: 'EGP', symbol: 'ج.م', position: CurrencyPosition.After, decimals: 2 },
  { code: 'USD', symbol: '$', position: CurrencyPosition.Before, decimals: 2 },
  { code: 'EUR', symbol: '€', position: CurrencyPosition.Before, decimals: 2 },
  { code: 'GBP', symbol: '£', position: CurrencyPosition.Before, decimals: 2 },
];

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.css',
})
export class SettingsComponent implements OnInit {
  private readonly appConfig = inject(AppConfigService);
  private readonly global = inject(GlobalService);
  private readonly translation = inject(TranslationService);
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** Where the preview's webfont goes — its own id, so it never disturbs the applied one. */
  private static readonly previewFontId = 'wanes-font-preview';

  readonly presets = PRESETS;
  readonly currencies = CURRENCIES;
  readonly fonts = FONTS;
  readonly positions = [
    { value: CurrencyPosition.Before, labelKey: 'cfg_position_before' },
    { value: CurrencyPosition.After, labelKey: 'cfg_position_after' },
  ];

  readonly model = signal<AppConfiguration>({ ...DEFAULT_CONFIG });
  readonly saving = signal(false);

  /** Live preview of the derived shades — what saving would apply, before it is applied. */
  readonly shades = computed(() => deriveBrand(this.model().primaryColor));

  /**
   * Stacks for the type specimen, so the admin reads the font before committing
   * to it. Both directions are shown: a choice that suits the Latin face can
   * still be the wrong call for Arabic, and that is only visible side by side.
   */
  readonly fontPreview = computed(() => fontStack(this.model().fontFamily));
  readonly fontPreviewRtl = computed(() => rtlFontStack(this.model().fontFamily));

  /** A sample price rendered with the currency settings currently in the form. */
  readonly sample = computed(() => {
    const m = this.model();
    const digits = Math.min(3, Math.max(0, Number(m.currencyDecimals) || 0));
    const text = new Intl.NumberFormat(this.translation.lang(), {
      minimumFractionDigits: digits,
      maximumFractionDigits: digits,
    }).format(1234.5);
    return Number(m.currencyPosition) === CurrencyPosition.After
      ? `${text} ${m.currencySymbol}`
      : `${m.currencySymbol}${text}`;
  });

  /**
   * The window in words, so an admin typing 90 sees "1 h 30 min" rather than
   * having to divide. Built here rather than in the template because the
   * translate pipe takes no parameters.
   */
  readonly hailTtlPreview = computed(() => {
    const minutes = HAIL_TTL_BOUNDS.clamp(Number(this.model().hailRequestTtlMinutes) || 0);
    const hours = Math.floor(minutes / 60);
    const rest = minutes % 60;
    const parts = [
      hours ? `${hours} ${this.translation.translate('cfg_unit_hours')}` : '',
      rest ? `${rest} ${this.translation.translate('cfg_unit_minutes')}` : '',
    ].filter(Boolean);
    return `${this.translation.translate('cfg_hail_ttl_preview')} ${parts.join(' ')}`;
  });

  /**
   * What a typical ride would list at, so an admin setting rates sees a price
   * rather than two abstract numbers. Ten kilometres because it is the length
   * of an ordinary city trip here, and one seat because that is the figure the
   * server actually stores.
   */
  readonly farePreview = computed(() => {
    const m = this.model();
    const perSeat =
      FARE_RATE_BOUNDS.clamp(Number(m.fareBaseAmount)) +
      FARE_RATE_BOUNDS.clamp(Number(m.farePerKm)) * FARE_PREVIEW_KM;
    // Same locale and precision as the currency sample above, so the two
    // previews on this page cannot disagree about how money is written.
    const digits = Math.min(3, Math.max(0, Number(m.currencyDecimals) || 0));
    const text = new Intl.NumberFormat(this.translation.lang(), {
      minimumFractionDigits: digits,
      maximumFractionDigits: digits,
    }).format(perSeat);
    const amount = Number(m.currencyPosition) === CurrencyPosition.After
      ? `${text} ${m.currencySymbol}`
      : `${m.currencySymbol}${text}`;
    return `${this.translation.translate('cfg_fare_preview')} ${amount}`;
  });

  ngOnInit(): void {
    // Bootstrap already fetched this, so the form opens on the live values
    // without a second round trip.
    this.model.set({ ...this.appConfig.config() });
  }

  /**
   * Picks a font and fetches its faces, so the specimen below renders in the
   * real type rather than in the fallback. Separate from [[set]] because only
   * this field has anything to download.
   */
  chooseFont(font: AppFont): void {
    this.set('fontFamily', font);
    if (this.isBrowser) {
      ensureFontStylesheet(this.document, font, SettingsComponent.previewFontId);
    }
  }

  set<K extends keyof AppConfiguration>(key: K, value: AppConfiguration[K]): void {
    this.model.update((m) => ({ ...m, [key]: value }));
  }

  /** A preset currency fills the symbol, side and precision that go with the code. */
  applyCurrency(code: string): void {
    const c = CURRENCIES.find((x) => x.code === code);
    if (!c) return;
    this.model.update((m) => ({
      ...m,
      currencyCode: c.code,
      currencySymbol: c.symbol,
      currencyPosition: c.position,
      currencyDecimals: c.decimals,
    }));
  }

  reset(): void {
    this.model.set({ ...this.appConfig.config() });
  }

  async save(): Promise<void> {
    if (!this.isBrowser || this.saving()) return;
    const m = this.model();

    if (!/^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(m.primaryColor.trim())) {
      this.global.errorMsg(this.translation.translate('cfg_invalid_color'));
      return;
    }
    if (!m.currencyCode.trim() || !m.currencySymbol.trim()) {
      this.global.errorMsg(this.translation.translate('cfg_invalid_currency'));
      return;
    }
    // Support channels are optional, so each check only fires on a non-empty
    // value — clearing a field has to stay a valid edit.
    const email = (m.supportEmail ?? '').trim();
    if (email && !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) {
      this.global.errorMsg(this.translation.translate('cfg_invalid_email'));
      return;
    }
    const website = (m.supportWebsite ?? '').trim();
    if (website && !/^https?:\/\/\S+$/.test(website)) {
      this.global.errorMsg(this.translation.translate('cfg_invalid_website'));
      return;
    }
    for (const phone of [(m.supportPhone ?? '').trim(), (m.supportWhatsApp ?? '').trim()]) {
      if (phone && !/^\+?[0-9][0-9\s\-()]{4,}$/.test(phone)) {
        this.global.errorMsg(this.translation.translate('cfg_invalid_phone'));
        return;
      }
    }

    this.saving.set(true);
    try {
      const saved = await this.appConfig.save({
        ...m,
        currencyCode: m.currencyCode.trim(),
        currencySymbol: m.currencySymbol.trim(),
        currencyPosition: Number(m.currencyPosition),
        currencyDecimals: Number(m.currencyDecimals),
        primaryColor: m.primaryColor.trim(),
        fontFamily: Number(m.fontFamily),
        hailRequestTtlMinutes: HAIL_TTL_BOUNDS.clamp(Number(m.hailRequestTtlMinutes)),
        fareBaseAmount: FARE_RATE_BOUNDS.clamp(Number(m.fareBaseAmount)),
        farePerKm: FARE_RATE_BOUNDS.clamp(Number(m.farePerKm)),
        supportPhone: (m.supportPhone ?? '').trim(),
        supportWhatsApp: (m.supportWhatsApp ?? '').trim(),
        supportEmail: email,
        supportWebsite: website,
        supportHours: (m.supportHours ?? '').trim(),
      });
      if (saved) {
        // The server normalises the hex, so mirror what it stored rather than
        // leaving the form showing the shorthand the admin typed.
        this.model.set({ ...saved });
        this.global.successMsg(this.translation.translate('saved'));
      } else {
        this.global.errorMsg(this.translation.translate('error_generic'));
      }
    } finally {
      this.saving.set(false);
    }
  }
}
