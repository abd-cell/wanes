import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AppConfiguration, CurrencyPosition } from '../../core/api/models';
import { AppConfigService, DEFAULT_CONFIG } from '../../core/services/app-config.service';
import { deriveBrand } from '../../core/services/brand-color';
import { GlobalService } from '../../core/services/global.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

/** Ready-made brand colours, so an admin without a hex code still has good options. */
const PRESETS = ['#0FAE9E', '#2563EB', '#7C3AED', '#DB2777', '#E5484D', '#F97316', '#CA8A04', '#16A34A'];

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
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly presets = PRESETS;
  readonly currencies = CURRENCIES;
  readonly positions = [
    { value: CurrencyPosition.Before, labelKey: 'cfg_position_before' },
    { value: CurrencyPosition.After, labelKey: 'cfg_position_after' },
  ];

  readonly model = signal<AppConfiguration>({ ...DEFAULT_CONFIG });
  readonly saving = signal(false);

  /** Live preview of the derived shades — what saving would apply, before it is applied. */
  readonly shades = computed(() => deriveBrand(this.model().primaryColor));

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

  ngOnInit(): void {
    // Bootstrap already fetched this, so the form opens on the live values
    // without a second round trip.
    this.model.set({ ...this.appConfig.config() });
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
