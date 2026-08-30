import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import {
  AnalyticsBreakdowns, AnalyticsLeaderboards, AnalyticsOperations, AnalyticsOverview,
  AnalyticsTimeSeries, EndpointStat, LeaderRow, MetricPoint, SeriesPoint,
} from '../../core/api/models';
import { BarListComponent } from './charts/bar-list.component';
import { ChartCardComponent } from './charts/chart-card.component';
import { ColumnChartComponent } from './charts/column-chart.component';
import { DonutChartComponent } from './charts/donut-chart.component';
import { LineChartComponent } from './charts/line-chart.component';
import { StatTileComponent } from './charts/stat-tile.component';
import { LineSeries, Slice, TableView, compact, exact, percent } from './charts/chart.types';

/** One card in a repeated breakdown grid. */
interface CardSpec {
  key: string;
  titleKey: string;
  kind: 'donut' | 'bars';
  slices: Slice[];
  table: TableView;
  center?: string;
  unit?: string;
}

interface Figure {
  labelKey: string;
  value: string;
}

const RANGES = [7, 30, 90];

/**
 * The analytics dashboard. One filter row at the top scopes every number and
 * chart below it, so the page always shows one consistent slice of the data.
 * The five backend endpoints are fetched together and each section renders from
 * its own signal.
 */
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    TranslatePipe, StatTileComponent, ChartCardComponent,
    LineChartComponent, ColumnChartComponent, DonutChartComponent, BarListComponent,
  ],
  templateUrl: './dashboard.component.html',
  styles: [`
    .range-row { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin-bottom: 18px; }
    .range-row .label { font-size: 12.5px; color: var(--app-ink-soft); margin-inline-end: 4px; }
    .range-row .who { margin-inline-start: auto; font-size: 12.5px; color: var(--app-ink-soft); }
  `],
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly translation = inject(TranslationService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly ranges = RANGES;
  readonly days = signal(30);
  readonly loading = signal(false);
  readonly loaded = signal(false);

  readonly overview = signal<AnalyticsOverview | null>(null);
  readonly series = signal<AnalyticsTimeSeries | null>(null);
  readonly breakdowns = signal<AnalyticsBreakdowns | null>(null);
  readonly operations = signal<AnalyticsOperations | null>(null);
  readonly leaders = signal<AnalyticsLeaderboards | null>(null);

  ngOnInit(): void {
    if (this.isBrowser) this.load();
  }

  setRange(days: number): void {
    if (this.days() === days) return;
    this.days.set(days);
    this.load();
  }

  /** Refetch holds the previous render at reduced opacity — no skeleton flash. */
  async load(): Promise<void> {
    const days = this.days();
    this.loading.set(true);
    try {
      const [overview, series, breakdowns, operations, leaders] = await Promise.all([
        firstValueFrom(this.api.analyticsOverview(days)),
        firstValueFrom(this.api.analyticsTimeSeries(days)),
        firstValueFrom(this.api.analyticsBreakdowns(days)),
        firstValueFrom(this.api.analyticsOperations(days)),
        firstValueFrom(this.api.analyticsLeaderboards(days)),
      ]);
      if (overview.data) this.overview.set(overview.data);
      if (series.data) this.series.set(series.data);
      if (breakdowns.data) this.breakdowns.set(breakdowns.data);
      if (operations.data) this.operations.set(operations.data);
      if (leaders.data) this.leaders.set(leaders.data);
      this.loaded.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  // ── headline tiles ──

  readonly rangeNote = computed(() =>
    this.t('dash_vs_previous').replace('{n}', String(this.days())));

  readonly heroValue = computed(() => compact(this.overview()?.newBookings ?? 0));

  readonly fillRateText = computed(() => percent(this.overview()?.seatFillRate ?? 0));
  readonly matchRateText = computed(() => percent(this.overview()?.matchRate ?? 0));
  readonly cancelRateText = computed(() => percent(this.overview()?.bookingCancelRate ?? 0));
  readonly errorRateText = computed(() => percent(this.overview()?.apiErrorRate ?? 0, 2));
  readonly avgRatingText = computed(() => {
    const o = this.overview();
    return o && o.ratings > 0 ? `${o.avgRating.toFixed(2)} ★` : '—';
  });

  readonly apiSummary = computed(() => {
    const o = this.operations();
    if (!o) return '';
    return this.t('chart_api_classes_sub')
      .replace('{avg}', exact(o.avgDurationMs))
      .replace('{slow}', exact(o.slowCalls));
  });

  readonly figures = computed<Figure[]>(() => {
    const o = this.overview();
    if (!o) return [];
    return [
      { labelKey: 'tot_users', value: exact(o.users) },
      { labelKey: 'tot_riders', value: exact(o.riders) },
      { labelKey: 'tot_drivers', value: exact(o.drivers) },
      { labelKey: 'tot_verified_drivers', value: exact(o.verifiedDrivers) },
      { labelKey: 'tot_online_drivers', value: exact(o.onlineDrivers) },
      { labelKey: 'tot_disabled', value: exact(o.disabledUsers) },
      { labelKey: 'tot_vehicles', value: exact(o.vehicles) },
      { labelKey: 'tot_fleet_seats', value: exact(o.vehicleSeats) },
      { labelKey: 'tot_trips', value: exact(o.trips) },
      { labelKey: 'tot_active_trips', value: exact(o.activeTrips) },
      { labelKey: 'tot_completed_trips', value: exact(o.completedTrips) },
      { labelKey: 'tot_cancelled_trips', value: exact(o.cancelledTrips) },
      { labelKey: 'tot_seats_offered', value: exact(o.seatsOffered) },
      { labelKey: 'tot_seats_taken', value: exact(o.seatsTaken) },
      { labelKey: 'tot_bookings', value: exact(o.bookings) },
      { labelKey: 'tot_avg_seats', value: exact(o.avgSeatsPerBooking) },
      { labelKey: 'tot_requests', value: exact(o.requests) },
      { labelKey: 'tot_open_requests', value: exact(o.openRequests) },
      { labelKey: 'tot_ratings', value: exact(o.ratings) },
      { labelKey: 'tot_low_ratings', value: exact(o.lowRatings) },
      { labelKey: 'tot_notifications', value: exact(o.notifications) },
      { labelKey: 'tot_unread', value: exact(o.unreadNotifications) },
      { labelKey: 'tot_places', value: exact(o.savedPlaces) },
      { labelKey: 'tot_sessions', value: exact(o.activeSessions) },
      { labelKey: 'tot_logins', value: exact(o.newLogins) },
      { labelKey: 'tot_audit', value: exact(o.auditEvents) },
      { labelKey: 'tot_api_calls', value: exact(o.apiCalls) },
      { labelKey: 'tot_avg_response', value: `${exact(o.avgResponseMs)} ms` },
    ];
  });

  // ── time series ──

  readonly dayLabels = computed(() => (this.series()?.newBookings ?? []).map((p) => dayLabel(p.date)));

  readonly activitySeries = computed<LineSeries[]>(() => {
    const s = this.series();
    if (!s) return [];
    return [
      { name: this.t('series_users'), slot: 0, values: values(s.newUsers) },
      { name: this.t('series_trips'), slot: 1, values: values(s.newTrips) },
      { name: this.t('series_bookings'), slot: 2, values: values(s.newBookings) },
      { name: this.t('series_requests'), slot: 3, values: values(s.newRequests) },
    ];
  });

  readonly outcomeSeries = computed<LineSeries[]>(() => {
    const s = this.series();
    if (!s) return [];
    return [
      { name: this.t('series_completed'), slot: 0, values: values(s.completedTrips) },
      { name: this.t('series_cancelled'), slot: 1, values: values(s.cancelledTrips) },
    ];
  });

  readonly seatsSeries = computed<LineSeries[]>(() => {
    const s = this.series();
    return s ? [{ name: this.t('series_seats'), slot: 0, values: values(s.seatsBooked) }] : [];
  });

  readonly loginsSeries = computed<LineSeries[]>(() => {
    const s = this.series();
    return s ? [{ name: this.t('series_logins'), slot: 2, values: values(s.logins) }] : [];
  });

  readonly trafficSeries = computed<LineSeries[]>(() => {
    const o = this.operations();
    if (!o) return [];
    return [
      { name: this.t('series_calls'), slot: 2, values: values(o.callsByDay) },
      { name: this.t('series_errors'), slot: 1, values: values(o.errorsByDay) },
    ];
  });

  readonly auditSeries = computed<LineSeries[]>(() => {
    const o = this.operations();
    return o ? [{ name: this.t('series_events'), slot: 5, values: values(o.auditByDay) }] : [];
  });

  readonly activityTable = computed(() => this.seriesTable(this.activitySeries()));
  readonly outcomeTable = computed(() => this.seriesTable(this.outcomeSeries()));
  readonly seatsTable = computed(() => this.seriesTable(this.seatsSeries()));
  readonly loginsTable = computed(() => this.seriesTable(this.loginsSeries()));
  readonly trafficTable = computed(() => this.seriesTable(this.trafficSeries()));
  readonly auditTable = computed(() => this.seriesTable(this.auditSeries()));

  // ── column charts ──

  readonly demandByHour = computed<Slice[]>(() =>
    (this.breakdowns()?.demandByHour ?? []).map((p) => ({
      label: p.label,
      value: p.value,
      sublabel: `${p.label}:00`,
    })));

  readonly tripsByWeekday = computed<Slice[]>(() =>
    (this.breakdowns()?.tripsByWeekday ?? []).map((p) => ({
      label: this.t(`weekday_${p.key}`),
      value: p.value,
    })));

  readonly ratingsByStars = computed<Slice[]>(() =>
    (this.breakdowns()?.ratingsByStars ?? []).map((p) => ({
      label: `${p.key}★`,
      value: p.value,
    })));

  /** Status classes get the reserved status palette, always with a visible label. */
  readonly httpClasses = computed<Slice[]>(() => {
    const o = this.operations();
    if (!o) return [];
    return [
      { label: this.t('http_2xx'), value: o.ok2xx, color: 'var(--viz-good)' },
      { label: this.t('http_3xx'), value: o.redirect3xx, color: 'var(--viz-warning)' },
      { label: this.t('http_4xx'), value: o.clientError4xx, color: 'var(--viz-serious)' },
      { label: this.t('http_5xx'), value: o.serverError5xx, color: 'var(--viz-critical)' },
    ];
  });

  readonly demandTable = computed(() => this.sliceTable('col_hour', this.demandByHour()));
  readonly weekdayTable = computed(() => this.sliceTable('col_day', this.tripsByWeekday()));
  readonly ratingsTable = computed(() => this.sliceTable('col_stars', this.ratingsByStars()));
  readonly httpTable = computed(() => this.sliceTable('col_status_code', this.httpClasses()));

  // ── repeated breakdown grids ──

  readonly statusCards = computed<CardSpec[]>(() => {
    const b = this.breakdowns();
    if (!b) return [];
    return [
      this.donut('trips', 'chart_trips_status', b.tripsByStatus, 'tripstatus', this.t('res_trips')),
      this.donut('bookings', 'chart_bookings_status', b.bookingsByStatus, 'bookingstatus', this.t('res_bookings')),
      this.donut('requests', 'chart_requests_status', b.requestsByStatus, 'requeststatus', this.t('res_requests')),
    ];
  });

  readonly communityCards = computed<CardSpec[]>(() => {
    const b = this.breakdowns();
    if (!b) return [];
    return [
      this.bars('pipeline', 'chart_driver_pipeline', b.usersByDriverStatus, 'driverstatus'),
      this.bars('notiftypes', 'chart_notif_types', b.notificationsByType, 'notiftype'),
      this.donut('devices', 'chart_sessions_device', b.sessionsByDevice, 'devicetype', this.t('res_sessions')),
      this.donut('language', 'chart_users_language', b.usersByLanguage, 'language', this.t('res_users')),
      this.donut('gender', 'chart_users_gender', b.usersByGender, 'gender', this.t('res_users')),
      this.bars('places', 'chart_places_label', b.placesByLabel, 'placelabel'),
    ];
  });

  readonly endpointCards = computed<CardSpec[]>(() => {
    const o = this.operations();
    if (!o) return [];
    return [
      this.endpoints('top-endpoints', 'chart_top_endpoints', o.topEndpoints, 'calls'),
      this.endpoints('slow-endpoints', 'chart_slow_endpoints', o.slowestEndpoints, 'duration'),
      this.leaderCard('audit-actions', 'chart_top_audit', o.topAuditActions, 'col_action', 'col_count'),
    ];
  });

  readonly leaderCards = computed<CardSpec[]>(() => {
    const l = this.leaders();
    if (!l) return [];
    return [
      this.leaderCard('drivers', 'chart_top_drivers', l.topDrivers, 'col_driver', 'col_trips', 'col_seats_total'),
      this.leaderCard('riders', 'chart_top_riders', l.topRiders, 'col_rider', 'col_bookings', 'col_seats'),
      this.leaderCard('routes', 'chart_top_routes', l.topRoutes, 'col_route', 'col_trips'),
      this.leaderCard('origins', 'chart_top_origins', l.topOrigins, 'col_origin', 'col_trips'),
      this.leaderCard('destinations', 'chart_top_destinations', l.topDestinations, 'col_destination', 'col_trips'),
      this.leaderCard('rated', 'chart_top_rated', l.topRatedDrivers, 'col_driver', 'col_count', 'col_stars'),
    ];
  });

  // ── builders ──

  private donut(key: string, titleKey: string, points: MetricPoint[], prefix: string, center: string): CardSpec {
    const slices = this.enumSlices(points, prefix);
    return { key, titleKey, kind: 'donut', slices, center, table: this.sliceTable('col_status', slices) };
  }

  /**
   * Ranked bars are one series: bar length already carries the magnitude, so every
   * row keeps slot 1 rather than spending a hue on what the label already says.
   */
  private bars(key: string, titleKey: string, points: MetricPoint[], prefix: string): CardSpec {
    const slices = this.enumSlices(points, prefix, false);
    return { key, titleKey, kind: 'bars', slices, table: this.sliceTable('col_category', slices) };
  }

  private endpoints(key: string, titleKey: string, stats: EndpointStat[], by: 'calls' | 'duration'): CardSpec {
    const slices: Slice[] = stats.map((s) => ({
      label: `${s.method} ${s.path}`,
      value: by === 'calls' ? s.calls : s.avgDurationMs,
    }));
    return {
      key, titleKey, kind: 'bars', slices,
      unit: by === 'duration' ? ' ms' : '',
      table: {
        columns: [this.t('col_endpoint'), this.t('col_calls'), this.t('col_avg_ms'), this.t('col_errors')],
        rows: stats.map((s) => [`${s.method} ${s.path}`, s.calls, s.avgDurationMs, s.errors]),
      },
    };
  }

  private leaderCard(
    key: string, titleKey: string, rows: LeaderRow[],
    nameKey: string, valueKey: string, secondaryKey?: string,
  ): CardSpec {
    const slices: Slice[] = rows.map((r) => ({
      label: r.label || '—',
      value: r.value,
      sublabel: r.sublabel,
    }));
    const columns = [this.t(nameKey), this.t(valueKey)];
    if (secondaryKey) columns.push(this.t(secondaryKey));
    return {
      key, titleKey, kind: 'bars', slices,
      table: {
        columns,
        rows: rows.map((r) => {
          const cells: (string | number)[] = [r.sublabel ? `${r.label} → ${r.sublabel}` : r.label, r.value];
          if (secondaryKey) cells.push(r.secondary ?? 0);
          return cells;
        }),
      },
    };
  }

  /**
   * `perSlice` gives each category its own categorical slot — right for a donut,
   * wrong for a single-series bar list.
   */
  private enumSlices(points: MetricPoint[], prefix: string, perSlice = true): Slice[] {
    return points.map((p, index) => ({
      label: this.translation.translate(`${prefix}_${p.key}`),
      value: p.value,
      slot: perSlice ? index : 0,
    }));
  }

  private sliceTable(headerKey: string, slices: Slice[]): TableView {
    const total = slices.reduce((sum, s) => sum + s.value, 0);
    return {
      columns: [this.t(headerKey), this.t('col_count'), this.t('col_share')],
      rows: slices.map((s) => [s.label, s.value, total ? percent(s.value / total) : '—']),
    };
  }

  private seriesTable(series: LineSeries[]): TableView {
    const labels = this.dayLabels();
    return {
      columns: [this.t('col_day'), ...series.map((s) => s.name)],
      rows: labels.map((label, i) => [label, ...series.map((s) => s.values[i] ?? 0)]),
    };
  }

  private t(key: string): string {
    return this.translation.translate(key);
  }
}

const values = (points: SeriesPoint[]): number[] => points.map((p) => p.value);

/** Short axis label: day/month in UTC, matching the backend's UTC day buckets. */
function dayLabel(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return `${date.getUTCDate()}/${date.getUTCMonth() + 1}`;
}
