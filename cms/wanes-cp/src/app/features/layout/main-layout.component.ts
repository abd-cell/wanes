import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { GlobalService } from '../../core/services/global.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { RESOURCES } from '../data/resource-config';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.css',
})
export class MainLayoutComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly global = inject(GlobalService);
  readonly translation = inject(TranslationService);

  readonly resources = RESOURCES;

  get lang() { return this.translation.lang(); }

  toggleLang(): void {
    const next = this.lang === 'en' ? 'ar' : 'en';
    const rest = this.router.url.split('/').slice(2).join('/');
    this.router.navigateByUrl(`/${next}/${rest}`);
  }

  async logout(): Promise<void> {
    try { await firstValueFrom(this.api.logout()); } catch { /* ignore */ }
    this.global.clearSession();
    this.router.navigate(['/', this.lang, 'login']);
  }
}
