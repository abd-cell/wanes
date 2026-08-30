import { Injectable, signal } from '@angular/core';
import { ar } from '../../i18n/ar';
import { en } from '../../i18n/en';

type Lang = 'en' | 'ar';

/** Label lookup by key. A missing key falls back to the key literal. */
@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly maps: Record<Lang, Record<string, string>> = { en, ar };
  readonly lang = signal<Lang>('en');

  setLang(lang: Lang): void {
    this.lang.set(lang);
  }

  translate(key: string): string {
    return this.maps[this.lang()][key] ?? key;
  }
}
