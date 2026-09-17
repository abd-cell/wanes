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

/** A whole-minute bound, mirrored from the server so the form shows what it would store. */
const minutes = (min: number, max: number) => ({
  min,
  max,
  clamp: (value: number) => Math.min(max, Math.max(min, Math.round(value || 0))),
});

/**
 * The windows the server will accept, mirrored here so the form shows the
 * clamped figure it would actually store rather than whatever was typed.
 *
 * `TripConfirmationRules` and `RiderTripRules` on the server are the
 * authority; these are the same numbers, not a second opinion.
 */
const CONFIRM_CUTOFF_BOUNDS = minutes(5, 720);
const CONFIRM_LEAD_BOUNDS = minutes(1, 240);

/** The seeded passenger threshold, bounded by what an ordinary car can seat. */
const MIN_PASSENGERS_BOUNDS = {
  min: 1,
  max: 8,
  clamp: (value: number) => Math.min(8, Math.max(1, Math.round(value || 0))),
};

/** The offer window. Zero is the meaningful default, not a missing value. */
const SELECTION_WINDOW_BOUNDS = {
  min: 0,
  max: 120,
  clamp: (value: number) => Math.min(120, Math.max(0, Math.round(value || 0))),
};
const SPEED_BOUNDS = {
  min: 5,
  max: 120,
  clamp: (value: number) => Math.min(120, Math.max(5, Math.round(value || 0))),
};

/**
 * Fare rates, bounded the way the server bounds them (`Trips.FareRules`).
 *
 * Zero is allowed on both: a flat flag-fall with no distance component, or a
 * fare that is all distance, are both real pricing choices. Negative is not —
 * it would price a long ride below a short one — and the ceiling is there so a
 * slipped decimal cannot list a trip at a fortune.
 */
/** The distance the fare and speed previews quote for — an ordinary city trip. */
const FARE_PREVIEW_KM = 10;
const SPEED_PREVIEW_KM = 10;

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
  { value: AppFont.Almarai, labelKey: 'cfg_font_almarai', faceKey: 'cfg_font_almarai_faces' },
  { value: AppFont.ReadexPro, labelKey: 'cfg_font_readex', faceKey: 'cfg_font_readex_faces' },
  { value: AppFont.Alexandria, labelKey: 'cfg_font_alexandria', faceKey: 'cfg_font_alexandria_faces' },
  { value: AppFont.Poppins, labelKey: 'cfg_font_poppins', faceKey: 'cfg_font_poppins_faces' },
  { value: AppFont.Montserrat, labelKey: 'cfg_font_montserrat', faceKey: 'cfg_font_montserrat_faces' },
  { value: AppFont.Amiri, labelKey: 'cfg_font_amiri', faceKey: 'cfg_font_amiri_faces' },
  // Last on purpose: it is the opt-out of the choice above, not another face.
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
  /**
   * The two confirm windows read as one sentence, because that is how they act:
   * the driver is asked at one moment and answered for at the next, and an
   * admin setting them apart needs to see the gap.
   */
  readonly confirmPreview = computed(() => {
    const cutoff = CONFIRM_CUTOFF_BOUNDS.clamp(Number(this.model().confirmCutoffMinutes) || 0);
    const lead = CONFIRM_LEAD_BOUNDS.clamp(Number(this.model().confirmDecisionLeadMinutes) || 0);
    return this.translation
      .translate('cfg_confirm_preview')
      .replace('{ask}', this.spell(cutoff + lead))
      .replace('{cutoff}', this.spell(cutoff));
  });

  /**
   * How long the lead-time rule makes a rider wait for four seats — the figure
   * that actually bites, since the earliest departure is one leg-time per seat.
   */
  readonly speedPreview = computed(() => {
    const kmh = SPEED_BOUNDS.clamp(Number(this.model().averageSpeedKmh) || 0);
    const legMinutes = Math.round((SPEED_PREVIEW_KM / kmh) * 60);
    return this.translation
      .translate('cfg_speed_preview')
      .replace('{leg}', this.spell(legMinutes))
      .replace('{four}', this.spell(Math.min(legMinutes * 4, 360)));
  });

  /**
   * Minutes in words, so an admin typing 90 sees "1 h 30 min" rather than
   * having to divide. Here rather than in the template because the translate
   * pipe takes no parameters.
   */
  private spell(total: number): string {
    const hours = Math.floor(total / 60);
    const rest = total % 60;
    const parts = [
      hours ? `${hours} ${this.translation.translate('cfg_unit_hours')}` : '',
      rest ? `${rest} ${this.translation.translate('cfg_unit_minutes')}` : '',
    ].filter(Boolean);
    return parts.length ? parts.join(' ') : `0 ${this.translation.translate('cfg_unit_minutes')}`;
  }

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
    const shareUrl = (m.shareBaseUrl ?? '').trim();
    if (shareUrl && !/^https?:\/\/\S+$/.test(shareUrl)) {
      this.global.errorMsg(this.translation.translate('cfg_invalid_website'));
      return;
    }
    if (!/^\+?[0-9]{2,15}$/.test((m.emergencyNumber ?? '').trim())) {
      this.global.errorMsg(this.translation.translate('cfg_invalid_emergency'));
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
        confirmCutoffMinutes: CONFIRM_CUTOFF_BOUNDS.clamp(Number(m.confirmCutoffMinutes)),
        confirmDecisionLeadMinutes:
          CONFIRM_LEAD_BOUNDS.clamp(Number(m.confirmDecisionLeadMinutes)),
        minimumPassengersDefault:
          MIN_PASSENGERS_BOUNDS.clamp(Number(m.minimumPassengersDefault)),
        driverSelectionWindowMinutes:
          SELECTION_WINDOW_BOUNDS.clamp(Number(m.driverSelectionWindowMinutes)),
        averageSpeedKmh: SPEED_BOUNDS.clamp(Number(m.averageSpeedKmh)),
        fareBaseAmount: FARE_RATE_BOUNDS.clamp(Number(m.fareBaseAmount)),
        farePerKm: FARE_RATE_BOUNDS.clamp(Number(m.farePerKm)),
        supportPhone: (m.supportPhone ?? '').trim(),
        supportWhatsApp: (m.supportWhatsApp ?? '').trim(),
        supportEmail: email,
        supportWebsite: website,
        supportHours: (m.supportHours ?? '').trim(),
        scheduledSelectionWindowMinutes:
          SELECTION_WINDOW_BOUNDS.clamp(Number(m.scheduledSelectionWindowMinutes)),
        freeCancelGraceMinutes: Math.min(60, Math.max(0, Math.round(Number(m.freeCancelGraceMinutes) || 0))),
        lateCancelLeadMinutes: Math.min(1440, Math.max(0, Math.round(Number(m.lateCancelLeadMinutes) || 0))),
        reliabilityWarnPoints: Math.min(100, Math.max(1, Math.round(Number(m.reliabilityWarnPoints) || 1))),
        reliabilitySuspendPoints: Math.min(100, Math.max(1, Math.round(Number(m.reliabilitySuspendPoints) || 1))),
        reliabilityWindowDays: Math.min(365, Math.max(1, Math.round(Number(m.reliabilityWindowDays) || 1))),
        suspensionDays: Math.min(90, Math.max(0, Math.round(Number(m.suspensionDays) || 0))),
        emergencyNumber: (m.emergencyNumber ?? '').trim() || '911',
        shareBaseUrl: (m.shareBaseUrl ?? '').trim(),
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
