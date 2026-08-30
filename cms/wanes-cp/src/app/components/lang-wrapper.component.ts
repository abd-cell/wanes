import { isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, inject } from '@angular/core';
import { ActivatedRoute, RouterOutlet } from '@angular/router';
import { TranslationService } from '../core/services/translation.service';
import { GlobalService } from '../core/services/global.service';

/** Shell for /:languageCode — sets dir/lang and syncs the translation language. */
@Component({
  selector: 'app-lang-wrapper',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class LangWrapperComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly translation = inject(TranslationService);
  private readonly global = inject(GlobalService);
  private readonly platformId = inject(PLATFORM_ID);

  constructor() {
    const code = (this.route.snapshot.params['languageCode'] as 'en' | 'ar') ?? 'en';
    this.translation.setLang(code);
    this.global.language.set(code);

    if (isPlatformBrowser(this.platformId)) {
      document.documentElement.lang = code;
      document.documentElement.dir = code === 'ar' ? 'rtl' : 'ltr';
    }
  }
}
