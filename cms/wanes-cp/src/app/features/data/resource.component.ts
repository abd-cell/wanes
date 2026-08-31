import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { AppConfigService } from '../../core/services/app-config.service';
import { GlobalService } from '../../core/services/global.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { ResourceRecord, Roles } from '../../core/api/models';
import { LookupPickerComponent } from './lookup-picker.component';
import {
  EnumOption, ResourceColumn, ResourceConfig, ResourceField, ROLE_OPTIONS, resourceByKey,
} from './resource-config';

const PAGE_SIZE = 25;

@Component({
  selector: 'app-resource',
  standalone: true,
  imports: [FormsModule, TranslatePipe, DatePipe, LookupPickerComponent],
  templateUrl: './resource.component.html',
  // Toolbar, form grid, section label and pager now live in admin.css — they
  // were identical to the notification manager's copies.
  styles: [`
    .roles-row { display: flex; gap: 16px; flex-wrap: wrap; margin: 6px 0 2px; }
    .roles-row label { display: flex; align-items: center; gap: 6px; font-weight: 600; }
  `],
})
export class ResourceComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly global = inject(GlobalService);
  private readonly appConfig = inject(AppConfigService);
  private readonly route = inject(ActivatedRoute);
  readonly translation = inject(TranslationService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly config = signal<ResourceConfig | undefined>(undefined);
  readonly rows = signal<ResourceRecord[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  readonly pageNumber = signal(1);
  readonly search = signal('');
  readonly filters = signal<Record<string, string>>({});

  readonly formOpen = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly model = signal<ResourceRecord>({});
  readonly editingRoles = signal<number[]>([]);

  readonly roleOptions = ROLE_OPTIONS;
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((p) => {
      const key = p.get('resource') ?? '';
      this.config.set(resourceByKey(key));
      this.resetState();
      if (this.isBrowser && this.config()) this.load();
    });
  }

  ngOnInit(): void {
    if (this.isBrowser && this.config()) this.load();
  }

  private resetState(): void {
    this.pageNumber.set(1);
    this.search.set('');
    this.filters.set({});
    this.rows.set([]);
    this.total.set(0);
    this.closeForm();
  }

  async load(): Promise<void> {
    const cfg = this.config();
    if (!cfg) return;
    this.loading.set(true);
    try {
      const res = await firstValueFrom(this.api.listResource(
        cfg.route,
        { pageNumber: this.pageNumber(), pageSize: PAGE_SIZE, search: this.search() || undefined },
        this.filters(),
      ));
      if (res.success && res.data) {
        this.rows.set(res.data.data);
        this.total.set(res.data.totalRows);
      }
    } finally {
      this.loading.set(false);
    }
  }

  applySearch(): void { this.pageNumber.set(1); this.load(); }

  setFilter(name: string, value: string): void {
    this.filters.update((f) => ({ ...f, [name]: value }));
    this.pageNumber.set(1);
    this.load();
  }

  go(delta: number): void {
    const next = this.pageNumber() + delta;
    if (next < 1 || next > this.pageCount()) return;
    this.pageNumber.set(next);
    this.load();
  }

  // ── formatting helpers ──
  cell(col: ResourceColumn, row: ResourceRecord): string {
    const v = row[col.field];
    if (v === null || v === undefined || v === '') return '—';
    if (col.type === 'bool') return this.translation.translate(v ? 'bool_true' : 'bool_false');
    if (col.type === 'enum' && col.enum) return this.enumLabel(col.enum, v as number);
    // Money is written with whatever currency the admin configured, so the
    // tables agree with what riders and drivers see in the app.
    if (col.type === 'currency') return this.appConfig.format(Number(v), this.translation.lang());
    return String(v);
  }

  isDate(col: ResourceColumn): boolean { return col.type === 'datetime'; }

  enumLabel(options: EnumOption[], value: number): string {
    const o = options.find((x) => x.value === value);
    return o ? this.translation.translate(o.labelKey) : String(value);
  }

  // ── form ──
  openCreate(): void {
    const cfg = this.config();
    if (!cfg) return;
    const m: ResourceRecord = {};
    for (const f of cfg.fields) m[f.name] = this.defaultFor(f);
    this.model.set(m);
    this.editingId.set(null);
    this.editingRoles.set([]);
    this.formOpen.set(true);
  }

  async openEdit(row: ResourceRecord): Promise<void> {
    const cfg = this.config();
    if (!cfg) return;
    const m: ResourceRecord = {};
    for (const f of cfg.fields) {
      let v = row[f.name];
      if (f.type === 'datetime' && typeof v === 'string' && v.length >= 16) v = v.slice(0, 16);
      m[f.name] = v ?? this.defaultFor(f);
    }
    this.model.set(m);
    this.editingId.set(Number(row['id']));
    this.editingRoles.set(Array.isArray(row['roles']) ? (row['roles'] as number[]) : []);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
    this.editingId.set(null);
    this.model.set({});
    this.editingRoles.set([]);
  }

  /** Reference fields hold a foreign key; the picker hands back the id it selected. */
  refValue(name: string): number | null {
    const v = this.model()[name];
    return v === null || v === undefined || v === '' ? null : Number(v);
  }

  setField(name: string, value: unknown): void {
    this.model.update((m) => ({ ...m, [name]: value }));
  }

  async save(): Promise<void> {
    const cfg = this.config();
    if (!cfg) return;
    const id = this.editingId();
    const body = this.model();
    const res = id === null
      ? await firstValueFrom(this.api.createResource(cfg.route, body))
      : await firstValueFrom(this.api.updateResource(cfg.route, id, body));
    if (res.success) {
      this.global.successMsg(this.translation.translate('saved'));
      this.closeForm();
      this.load();
    } else {
      this.global.errorMsg(res.message || this.translation.translate('error_generic'));
    }
  }

  async remove(row: ResourceRecord): Promise<void> {
    const cfg = this.config();
    if (!cfg) return;
    if (this.isBrowser && !confirm(this.translation.translate('confirm_delete'))) return;
    const res = await firstValueFrom(this.api.deleteResource(cfg.route, Number(row['id'])));
    if (res.success) {
      this.global.successMsg(this.translation.translate('deleted'));
      this.load();
    } else {
      this.global.errorMsg(res.message || this.translation.translate('error_generic'));
    }
  }

  hasRole(value: number): boolean { return this.editingRoles().includes(value); }

  async toggleRole(value: number): Promise<void> {
    const id = this.editingId();
    if (id === null) return;
    const grant = !this.hasRole(value);
    const res = await firstValueFrom(this.api.setUserRole(id, value as Roles, grant));
    if (res.success) {
      this.editingRoles.update((r) => grant ? [...r, value] : r.filter((x) => x !== value));
      this.global.successMsg(this.translation.translate('saved'));
      this.load();
    } else {
      this.global.errorMsg(res.message || this.translation.translate('error_generic'));
    }
  }

  private defaultFor(f: ResourceField): unknown {
    switch (f.type) {
      case 'checkbox': return false;
      case 'number': return null;
      case 'reference': return null;
      case 'select': return f.enum?.[0]?.value ?? null;
      default: return '';
    }
  }
}
