import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { SharedTrip } from '../../core/api/models';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { TranslationService } from '../../core/services/translation.service';

/**
 * The page a rider's "follow my trip" link opens.
 *
 * Public: the person following the ride has no account, and the unguessable
 * token in the address is the whole of the access. It shows who is driving,
 * the car, where the ride is going, and — only while the ride is under way —
 * where the car is. It refreshes itself every 30 seconds.
 */
@Component({
  selector: 'app-shared-trip',
  standalone: true,
  imports: [DatePipe, TranslatePipe],
  template: `
    <div class="share-page">
      <div class="brand"><span class="dot"></span><span class="name">WANES</span></div>
      <h1>{{ 'share_title' | translate }}</h1>

      @if (loading() && !trip()) {
        <p class="muted">{{ 'share_loading' | translate }}</p>
      } @else if (!trip()) {
        <div class="card"><p>{{ 'share_gone' | translate }}</p></div>
      } @else {
        @let t = trip()!;
        <div class="card">
          <h2>{{ riderLine() }}</h2>
          <p class="route">{{ t.originAddress }} → {{ t.destinationAddress }}</p>
          <dl>
            <dt>{{ 'share_departs' | translate }}</dt>
            <dd>{{ t.departAt | date: 'EEE d MMM, HH:mm' }}</dd>
            <dt>{{ 'share_status' | translate }}</dt>
            <dd>{{ ('tripstatus_' + t.tripStatus) | translate }} · {{ ('bookingstatus_' + t.bookingStatus) | translate }}</dd>
            <dt>{{ 'share_driver' | translate }}</dt>
            <dd>{{ t.driverFirstName || '—' }}@if (t.driverRating > 0) { · ★ {{ t.driverRating.toFixed(1) }} }</dd>
            <dt>{{ 'share_car' | translate }}</dt>
            <dd>{{ carLine() }}</dd>
            <dt>{{ 'share_location' | translate }}</dt>
            <dd>
              @if (t.driverLat != null && t.driverLng != null) {
                <a [href]="mapsLink()" target="_blank" rel="noopener">{{ 'share_open_map' | translate }}</a>
              } @else {
                <span class="muted">{{ 'share_location_hidden' | translate }}</span>
              }
            </dd>
          </dl>
          @if (mapUrl(); as url) {
            <iframe class="map" [src]="url" title="map" loading="lazy"></iframe>
          }
          <p class="muted small">{{ updatedLine() }}</p>
        </div>
        <p class="emergency">{{ 'share_emergency' | translate }}</p>
      }
    </div>
  `,
  styles: [`
    .share-page { max-width: 560px; margin: 0 auto; padding: 24px 16px 48px; }
    .brand { display: flex; align-items: center; gap: 8px; font-weight: 800; letter-spacing: 2px; }
    .dot { width: 12px; height: 12px; border-radius: 50%; background: var(--app-primary, #0FAE9E); }
    h1 { font-size: 22px; margin: 18px 0 12px; }
    h2 { font-size: 18px; margin: 0 0 4px; }
    .card { background: var(--app-surface, #fff); border: 1px solid var(--app-line, #e5e7eb);
            border-radius: 16px; padding: 16px; }
    .route { font-weight: 600; margin: 0 0 12px; }
    dl { display: grid; grid-template-columns: max-content 1fr; gap: 6px 14px; margin: 0 0 12px; }
    dt { color: var(--app-muted, #6b7280); }
    dd { margin: 0; font-weight: 600; }
    .map { width: 100%; height: 260px; border: 0; border-radius: 12px; margin: 6px 0; }
    .muted { color: var(--app-muted, #6b7280); }
    .small { font-size: 12px; }
    .emergency { margin-top: 14px; font-weight: 700; color: #c2410c; }
  `],
})
export class SharedTripComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly translation = inject(TranslationService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly trip = signal<SharedTrip | null>(null);
  readonly loading = signal(true);
  private timer?: ReturnType<typeof setInterval>;

  readonly riderLine = computed(() =>
    this.translation.translate('share_rider').replace('{name}', this.trip()?.riderFirstName || '—'));

  readonly carLine = computed(() => {
    const t = this.trip();
    if (!t) return '—';
    return [t.vehicleLabel, t.vehicleColor, t.vehiclePlate].filter(Boolean).join(' · ') || '—';
  });

  readonly updatedLine = computed(() => {
    const at = this.trip()?.updatedAt;
    const time = at ? new Date(at).toLocaleTimeString() : '—';
    return this.translation.translate('share_updated').replace('{time}', time);
  });

  readonly mapsLink = computed(() => {
    const t = this.trip();
    return t?.driverLat != null ? `https://maps.google.com/?q=${t.driverLat},${t.driverLng}` : '';
  });

  /** An OpenStreetMap embed around the car, or around the pickup before it moves. */
  readonly mapUrl = computed<SafeResourceUrl | null>(() => {
    const t = this.trip();
    if (!t) return null;
    const lat = t.driverLat ?? t.originLat;
    const lng = t.driverLng ?? t.originLng;
    if (!lat && !lng) return null;
    const d = 0.02;
    const url = `https://www.openstreetmap.org/export/embed.html?bbox=${lng - d},${lat - d},${lng + d},${lat + d}`
      + `&layer=mapnik&marker=${lat},${lng}`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  });

  ngOnInit(): void {
    if (!this.isBrowser) return;
    this.load();
    this.timer = setInterval(() => this.load(), 30_000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }

  private async load(): Promise<void> {
    const token = this.route.snapshot.paramMap.get('token') ?? '';
    try {
      const res = await firstValueFrom(this.api.sharedTrip(token));
      this.trip.set(res.success && res.data ? res.data : null);
    } catch {
      this.trip.set(null);
    } finally {
      this.loading.set(false);
    }
  }
}
