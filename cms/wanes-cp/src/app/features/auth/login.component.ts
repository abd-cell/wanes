import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { Roles } from '../../core/api/models';
import { GlobalService } from '../../core/services/global.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
})
export class LoginComponent {
  private readonly api = inject(ApiService);
  private readonly global = inject(GlobalService);
  private readonly translation = inject(TranslationService);
  private readonly router = inject(Router);

  readonly phone = signal('');
  readonly code = signal('');
  readonly codeSent = signal(false);
  readonly busy = signal(false);

  async sendCode(): Promise<void> {
    if (!this.phone() || this.busy()) return;
    this.busy.set(true);
    try {
      const res = await firstValueFrom(this.api.requestOtp(this.phone()));
      if (res.success) {
        this.codeSent.set(true);
        this.global.successMsg(this.translation.translate('login_code_sent'));
      }
    } finally {
      this.busy.set(false);
    }
  }

  async verify(): Promise<void> {
    if (!this.code() || this.busy()) return;
    this.busy.set(true);
    try {
      const res = await firstValueFrom(this.api.verifyOtp(this.phone(), this.code()));
      if (res.success && res.data) {
        // The panel only talks to admin/* endpoints — refuse a non-admin here
        // rather than signing them into a shell where every call comes back 403.
        // Only when the API actually reports roles: an older API that omits them
        // must fall through and let the backend 403 decide, not lock admins out.
        const roles = res.data.profile.roles;
        if (roles?.length && !roles.includes(Roles.Admin)) {
          this.global.errorMsg(this.translation.translate('login_not_admin'));
          return;
        }
        this.global.setSession(res.data.token, res.data.profile);
        this.global.successMsg(this.translation.translate('login_success'));
        this.router.navigate(['/', this.translation.lang(), 'dashboard']);
      } else {
        this.global.errorMsg(this.translation.translate('login_failed'));
      }
    } finally {
      this.busy.set(false);
    }
  }
}
