import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { GlobalService } from '../../core/services/global.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { DriverRow } from '../../core/api/models';

@Component({
  selector: 'app-drivers',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './drivers.component.html',
})
export class DriversComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly global = inject(GlobalService);
  private readonly translation = inject(TranslationService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly rows = signal<DriverRow[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  ngOnInit(): void { if (this.isBrowser) this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const res = await firstValueFrom(this.api.pendingDrivers({ pageNumber: 1, pageSize: 50 }));
      if (res.success && res.data) {
        this.rows.set(res.data.data);
        this.total.set(res.data.totalRows);
      }
    } finally {
      this.loading.set(false);
    }
  }

  async decide(row: DriverRow, approve: boolean): Promise<void> {
    const res = await firstValueFrom(this.api.verifyDriver(row.id, approve));
    if (res.success) {
      this.global.successMsg(this.translation.translate(approve ? 'driver_approved' : 'driver_rejected'));
      this.rows.update((list) => list.filter((r) => r.id !== row.id));
      this.total.update((n) => Math.max(0, n - 1));
    }
  }
}
