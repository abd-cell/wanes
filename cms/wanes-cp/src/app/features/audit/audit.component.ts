import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { AuditRow } from '../../core/api/models';

@Component({
  selector: 'app-audit',
  standalone: true,
  imports: [FormsModule, TranslatePipe, DatePipe],
  templateUrl: './audit.component.html',
})
export class AuditComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly rows = signal<AuditRow[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly filter = signal('');

  ngOnInit(): void { if (this.isBrowser) this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const res = await firstValueFrom(
        this.api.audit({ pageNumber: 1, pageSize: 50 }, this.filter() || undefined),
      );
      if (res.success && res.data) {
        this.rows.set(res.data.data);
        this.total.set(res.data.totalRows);
      }
    } finally {
      this.loading.set(false);
    }
  }
}
