import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { GlobalService } from '../../core/services/global.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import {
  BulkNotificationInput, LookupOption, NotificationAudience, NotificationBulkAction,
  NotificationRow, NotificationStats, NotificationType,
} from '../../core/api/models';
import { LookupPickerComponent } from '../data/lookup-picker.component';
import { EnumOption, NOTIF_AUDIENCE, NOTIF_TYPE } from '../data/resource-config';

const PAGE_SIZE = 25;

/**
 * Who a composed notification is aimed at. The three modes hit three different
 * endpoints, so the target is part of the draft rather than something inferred
 * from which fields happen to be filled in.
 */
export type TargetMode = 'user' | 'picked' | 'audience';

interface Draft {
  mode: TargetMode;
  /** `user` mode: the single recipient. */
  userId: number | null;
  /** `picked` mode: recipients as chips, so the admin can see who they chose. */
  recipients: LookupOption[];
  /** `audience` mode. */
  audience: NotificationAudience;
  type: NotificationType;
  title: string;
  body: string;
  titleAr: string;
  bodyAr: string;
  dataJson: string;
}

const emptyDraft = (): Draft => ({
  mode: 'user',
  userId: null,
  recipients: [],
  audience: NotificationAudience.All,
  type: NotificationType.General,
  title: '',
  body: '',
  titleAr: '',
  bodyAr: '',
  dataJson: '',
});

/**
 * The notification manager: one screen for everything an admin does with the
 * inbox. It replaces the generic data grid's notification entry, which could
 * only edit one row at a time and had the broadcast panel bolted onto it.
 *
 * Three things the grid could not do live here — a composer that targets one
 * user, a hand-picked set, or a whole audience; bulk read/unread/delete over
 * ticked rows; and the headline counts, which describe the whole table rather
 * than the page on screen and so come from their own endpoint.
 */
@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [FormsModule, TranslatePipe, DatePipe, LookupPickerComponent],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.css',
})
export class NotificationsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly global = inject(GlobalService);
  readonly translation = inject(TranslationService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly typeOptions = NOTIF_TYPE;
  readonly audienceOptions = NOTIF_AUDIENCE;
  readonly bulkAction = NotificationBulkAction;

  readonly modes: { mode: TargetMode; labelKey: string }[] = [
    { mode: 'user', labelKey: 'notif_mode_user' },
    { mode: 'picked', labelKey: 'notif_mode_picked' },
    { mode: 'audience', labelKey: 'notif_mode_audience' },
  ];

  // ── list ──
  readonly rows = signal<NotificationRow[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly pageNumber = signal(1);
  readonly search = signal('');
  readonly filterType = signal<string>('');
  readonly filterRead = signal<string>('');
  /** '' both, 'false' live only, 'true' cleared only. */
  readonly filterDeleted = signal<string>('');
  readonly filterUserId = signal<number | null>(null);

  readonly stats = signal<NotificationStats | null>(null);

  // ── selection (drives the bulk bar) ──
  readonly selected = signal<number[]>([]);

  // ── composer ──
  readonly composerOpen = signal(false);
  readonly sending = signal(false);
  readonly draft = signal<Draft>(emptyDraft());

  // ── single-row edit ──
  readonly editingId = signal<number | null>(null);
  readonly editModel = signal<NotificationRow | null>(null);
  readonly saving = signal(false);

  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  /** Unread as a share of the whole table, for the tile's meter and caption. */
  readonly unreadPct = computed(() => {
    const s = this.stats();
    return s && s.total > 0 ? Math.round((s.unread / s.total) * 100) : 0;
  });

  readonly readPct = computed(() => {
    const s = this.stats();
    // Derived from unread so the two shares always total 100 and never disagree
    // by a rounding step.
    return s && s.total > 0 ? 100 - this.unreadPct() : 0;
  });

  readonly allSelected = computed(() => {
    const rows = this.rows();
    return rows.length > 0 && rows.every((r) => this.selected().includes(r.id));
  });

  readonly anyFilter = computed(() =>
    !!this.search() || this.filterType() !== '' || this.filterRead() !== ''
    || this.filterDeleted() !== '' || this.filterUserId() !== null);

  /** Bad JSON is caught before sending, so the admin sees it next to the field. */
  readonly draftDataError = computed(() => this.jsonError(this.draft().dataJson));
  readonly editDataError = computed(() => this.jsonError(this.editModel()?.dataJson ?? ''));

  /** How many recipients the draft names; -1 when only the server can know. */
  readonly recipientCount = computed(() => {
    const d = this.draft();
    if (d.mode === 'user') return d.userId === null ? 0 : 1;
    if (d.mode === 'picked') return d.recipients.length;
    return -1;
  });

  readonly canSend = computed(() => {
    const d = this.draft();
    return d.title.trim().length > 0
      && d.body.trim().length > 0
      && this.draftDataError() === null
      && this.recipientCount() !== 0;
  });

  /** The message as English readers will see it. */
  readonly previewEn = computed(() => {
    const d = this.draft();
    return { title: d.title.trim(), body: d.body.trim() };
  });

  /**
   * The Arabic half. Blank Arabic falls back to the default wording — the same
   * rule the app applies — so the preview shows the fallback rather than an
   * empty card, and flags that it is doing so.
   */
  readonly previewAr = computed(() => {
    const d = this.draft();
    return {
      title: d.titleAr.trim() || d.title.trim(),
      body: d.bodyAr.trim() || d.body.trim(),
      fallback: !d.titleAr.trim() || !d.bodyAr.trim(),
    };
  });

  ngOnInit(): void {
    if (!this.isBrowser) return;
    this.load();
    this.loadStats();
  }

  // ── loading ──
  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const res = await firstValueFrom(this.api.listResource(
        'notifications',
        { pageNumber: this.pageNumber(), pageSize: PAGE_SIZE, search: this.search() || undefined },
        {
          type: this.filterType() || undefined,
          isRead: this.filterRead() || undefined,
          isDeleted: this.filterDeleted() || undefined,
          userId: this.filterUserId() ?? undefined,
        },
      ));
      if (res.success && res.data) {
        this.rows.set(res.data.data as unknown as NotificationRow[]);
        this.total.set(res.data.totalRows);
        // Drop ticks for rows that are no longer on screen, so the bulk bar
        // never acts on something the admin cannot see.
        const visible = new Set(this.rows().map((r) => r.id));
        this.selected.update((ids) => ids.filter((id) => visible.has(id)));
      }
    } finally {
      this.loading.set(false);
    }
  }

  async loadStats(): Promise<void> {
    const res = await firstValueFrom(this.api.notificationStats());
    if (res.success && res.data) this.stats.set(res.data);
  }

  /** Reloads the table and the headline counts together, so they never disagree. */
  async refreshAll(): Promise<void> {
    await Promise.all([this.load(), this.loadStats()]);
  }

  // ── filters ──
  applySearch(): void { this.pageNumber.set(1); this.load(); }

  setType(value: string): void { this.filterType.set(value); this.pageNumber.set(1); this.load(); }

  setRead(value: string): void { this.filterRead.set(value); this.pageNumber.set(1); this.load(); }

  setDeleted(value: string): void { this.filterDeleted.set(value); this.pageNumber.set(1); this.load(); }

  setUser(value: number | null): void { this.filterUserId.set(value); this.pageNumber.set(1); this.load(); }

  clearFilters(): void {
    this.search.set('');
    this.filterType.set('');
    this.filterRead.set('');
    this.filterDeleted.set('');
    this.filterUserId.set(null);
    this.pageNumber.set(1);
    this.load();
  }

  go(delta: number): void {
    const next = this.pageNumber() + delta;
    if (next < 1 || next > this.pageCount()) return;
    this.pageNumber.set(next);
    this.load();
  }

  // ── selection ──
  isSelected(id: number): boolean { return this.selected().includes(id); }

  toggleSelect(id: number): void {
    this.selected.update((ids) => ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]);
  }

  toggleAll(): void {
    this.selected.set(this.allSelected() ? [] : this.rows().map((r) => r.id));
  }

  clearSelection(): void { this.selected.set([]); }

  async runBulk(action: NotificationBulkAction): Promise<void> {
    const ids = this.selected();
    if (ids.length === 0) return;

    // Deleting is the only irreversible one, so it is the only one that asks.
    if (action === NotificationBulkAction.Delete && this.isBrowser
      && !confirm(this.t('bulk_delete_confirm').replace('{count}', String(ids.length)))) return;

    const body: BulkNotificationInput = { ids, action };
    const res = await firstValueFrom(this.api.bulkNotifications(body));
    if (res.success && res.data) {
      this.global.successMsg(this.t('bulk_done').replace('{count}', String(res.data.affected)));
      this.clearSelection();
      await this.refreshAll();
    } else {
      this.global.errorMsg(res.message || this.t('error_generic'));
    }
  }

  // ── composer ──
  openComposer(): void {
    this.closeEdit();
    this.draft.set(emptyDraft());
    this.composerOpen.set(true);
  }

  closeComposer(): void {
    this.composerOpen.set(false);
    this.sending.set(false);
    this.draft.set(emptyDraft());
  }

  setDraft<K extends keyof Draft>(name: K, value: Draft[K]): void {
    this.draft.update((d) => ({ ...d, [name]: value }));
  }

  setMode(mode: TargetMode): void { this.setDraft('mode', mode); }

  /** Adds a chip; the picker resets itself so it is ready for the next pick. */
  addRecipient(option: LookupOption, picker: LookupPickerComponent): void {
    this.draft.update((d) => d.recipients.some((r) => r.id === option.id)
      ? d
      : { ...d, recipients: [...d.recipients, option] });
    picker.reset();
  }

  removeRecipient(id: number): void {
    this.draft.update((d) => ({ ...d, recipients: d.recipients.filter((r) => r.id !== id) }));
  }

  async send(): Promise<void> {
    const d = this.draft();
    if (!this.canSend()) {
      this.global.errorMsg(this.t(this.recipientCount() === 0 ? 'send_needs_target' : 'broadcast_needs_text'));
      return;
    }

    // Every mode writes inbox rows and pushes to devices with no way back, so
    // each one confirms first — naming what it is about to reach.
    const target = d.mode === 'audience'
      ? this.enumLabel(this.audienceOptions, d.audience)
      : this.t('send_target_count').replace('{count}', String(this.recipientCount()));
    if (this.isBrowser && !confirm(this.t('broadcast_confirm').replace('{audience}', target))) return;

    const text = {
      type: d.type,
      title: d.title.trim(),
      body: d.body.trim(),
      titleAr: d.titleAr.trim() || null,
      bodyAr: d.bodyAr.trim() || null,
      dataJson: d.dataJson.trim() || null,
    };

    this.sending.set(true);
    try {
      let ok = false;
      let recipients = 0;
      let message: string | undefined;

      if (d.mode === 'audience') {
        const res = await firstValueFrom(this.api.broadcastNotification({ audience: d.audience, ...text }));
        ok = res.success && !!res.data;
        recipients = res.data?.recipients ?? 0;
        message = res.message;
      } else {
        const userIds = d.mode === 'user' ? [d.userId!] : d.recipients.map((r) => r.id);
        const res = await firstValueFrom(this.api.sendTargetedNotification({ userIds, ...text }));
        ok = res.success && !!res.data;
        recipients = res.data?.recipients ?? 0;
        message = res.message;
      }

      if (ok) {
        this.global.successMsg(this.t('broadcast_sent').replace('{count}', String(recipients)));
        this.closeComposer();
        await this.refreshAll();
      } else {
        this.global.errorMsg(message || this.t('error_generic'));
      }
    } finally {
      this.sending.set(false);
    }
  }

  // ── single-row edit ──
  openEdit(row: NotificationRow): void {
    this.closeComposer();
    this.editingId.set(row.id);
    this.editModel.set({ ...row });
  }

  closeEdit(): void {
    this.editingId.set(null);
    this.editModel.set(null);
    this.saving.set(false);
  }

  setEdit<K extends keyof NotificationRow>(name: K, value: NotificationRow[K]): void {
    this.editModel.update((m) => m === null ? m : { ...m, [name]: value });
  }

  async saveEdit(): Promise<void> {
    const m = this.editModel();
    const id = this.editingId();
    if (m === null || id === null) return;
    if (this.editDataError() !== null) {
      this.global.errorMsg(this.t('data_json_invalid'));
      return;
    }

    this.saving.set(true);
    try {
      const res = await firstValueFrom(this.api.updateResource('notifications', id, {
        userId: m.userId,
        type: m.type,
        title: m.title,
        body: m.body,
        titleAr: m.titleAr?.trim() || null,
        bodyAr: m.bodyAr?.trim() || null,
        dataJson: m.dataJson?.trim() || null,
        isRead: m.isRead,
      }));
      if (res.success) {
        this.global.successMsg(this.t('saved'));
        this.closeEdit();
        await this.refreshAll();
      } else {
        this.global.errorMsg(res.message || this.t('error_generic'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  async remove(row: NotificationRow): Promise<void> {
    if (this.isBrowser && !confirm(this.t('confirm_delete'))) return;
    const res = await firstValueFrom(this.api.deleteResource('notifications', row.id));
    if (res.success) {
      this.global.successMsg(this.t('deleted'));
      await this.refreshAll();
    } else {
      this.global.errorMsg(res.message || this.t('error_generic'));
    }
  }

  /** Flips one row's read state without opening the editor. */
  async toggleRead(row: NotificationRow): Promise<void> {
    const res = await firstValueFrom(this.api.bulkNotifications({
      ids: [row.id],
      action: row.isRead ? NotificationBulkAction.MarkUnread : NotificationBulkAction.MarkRead,
    }));
    if (res.success) await this.refreshAll();
    else this.global.errorMsg(res.message || this.t('error_generic'));
  }

  // ── helpers ──
  t(key: string): string { return this.translation.translate(key); }

  enumLabel(options: EnumOption[], value: number): string {
    const o = options.find((x) => x.value === value);
    return o ? this.t(o.labelKey) : String(value);
  }

  typeLabel(value: NotificationType): string { return this.enumLabel(this.typeOptions, value); }

  private jsonError(value: string | null | undefined): string | null {
    if (!value || !value.trim()) return null;
    try {
      JSON.parse(value);
      return null;
    } catch {
      return this.t('data_json_invalid');
    }
  }
}
