import { Component, OnDestroy, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { DatePipe, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { GlobalService } from '../../core/services/global.service';
import { TranslationService } from '../../core/services/translation.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { DriverDocument, DriverRow, DriverStatus } from '../../core/api/models';

/**
 * The manual half of driver verification: an admin looks at what the driver
 * uploaded and decides.
 *
 * The screen used to be a table with Approve and Reject on every row and
 * nothing to look at in between, which made "verification" a coin toss. Opening
 * a row now fetches the driver's documents and shows them; the buttons live
 * inside that panel, so a decision is taken with the evidence on screen.
 *
 * Documents are fetched as blobs and rendered from object URLs. The content
 * endpoint is admin-only and audits every read, and an `<img src>` cannot carry
 * the bearer token — pointing one at the API would only ever render a broken
 * image.
 */
@Component({
  selector: 'app-drivers',
  standalone: true,
  imports: [TranslatePipe, FormsModule, DatePipe],
  templateUrl: './drivers.component.html',
  styleUrl: './drivers.component.css',
})
export class DriversComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly global = inject(GlobalService);
  private readonly translation = inject(TranslationService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly DriverStatus = DriverStatus;

  /** The queue and the two decided piles, so a mis-click is recoverable. */
  readonly tabs: DriverStatus[] = [DriverStatus.Pending, DriverStatus.Verified, DriverStatus.Rejected];

  readonly rows = signal<DriverRow[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly tab = signal<DriverStatus>(DriverStatus.Pending);

  /** The expanded review panel, by driver id. Only one is open at a time. */
  readonly openId = signal<number | null>(null);
  readonly documents = signal<DriverDocument[]>([]);
  readonly documentsLoading = signal(false);
  readonly note = signal('');
  readonly deciding = signal(false);

  /**
   * documentId → object URL, for the panel that is open.
   *
   * Released when the panel closes rather than kept: an admin works through a
   * queue, and holding every driver's scans in memory for the life of the tab
   * is both wasteful and more of someone's identity documents than the page
   * needs to have at hand.
   */
  private readonly previews = new Map<number, string>();
  readonly previewVersion = signal(0);

  /** The document shown full-size, if any. */
  readonly lightbox = signal<DriverDocument | null>(null);

  ngOnInit(): void { if (this.isBrowser) this.load(); }

  ngOnDestroy(): void { this.releasePreviews(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.closePanel();
    try {
      const res = await firstValueFrom(this.api.pendingDrivers({ pageNumber: 1, pageSize: 50 }, this.tab()));
      if (res.success && res.data) {
        this.rows.set(res.data.data);
        this.total.set(res.data.totalRows);
      }
    } finally {
      this.loading.set(false);
    }
  }

  selectTab(status: DriverStatus): void {
    if (this.tab() === status) return;
    this.tab.set(status);
    void this.load();
  }

  /** Opens (or closes) one driver's review panel, fetching their documents. */
  async toggle(row: DriverRow): Promise<void> {
    if (this.openId() === row.id) { this.closePanel(); return; }

    this.releasePreviews();
    this.openId.set(row.id);
    this.note.set('');
    this.documents.set([]);
    this.documentsLoading.set(true);
    try {
      const res = await firstValueFrom(this.api.driverDocuments(row.id));
      if (res.success && res.data) {
        this.documents.set(res.data);
        await Promise.all(res.data.map((d) => this.loadPreview(d)));
      }
    } finally {
      this.documentsLoading.set(false);
    }
  }

  closePanel(): void {
    this.openId.set(null);
    this.lightbox.set(null);
    this.documents.set([]);
    this.note.set('');
    this.releasePreviews();
  }

  previewUrl(document: DriverDocument): string | undefined {
    // Read the version signal so the template re-renders when a preview lands;
    // the map itself is not reactive.
    this.previewVersion();
    return this.previews.get(document.id);
  }

  openInNewTab(document: DriverDocument): void {
    const url = this.previews.get(document.id);
    if (url) window.open(url, '_blank', 'noopener');
  }

  async decide(row: DriverRow, approve: boolean): Promise<void> {
    const note = this.note().trim();

    // A rejection with no reason is an instruction to guess, and the driver
    // re-sends the same photo. Ask for one word before ending someone's
    // application.
    if (!approve && !note) {
      this.global.errorMsg(this.translation.translate('driver_reason_required'));
      return;
    }

    this.deciding.set(true);
    try {
      const res = await firstValueFrom(this.api.verifyDriver(row.id, approve, note || undefined));
      if (!res.success) return;

      this.global.successMsg(this.translation.translate(approve ? 'driver_approved' : 'driver_rejected'));
      this.closePanel();

      // The row belongs to another pile now, so it leaves this one.
      this.rows.update((list) => list.filter((r) => r.id !== row.id));
      this.total.update((n) => Math.max(0, n - 1));
    } finally {
      this.deciding.set(false);
    }
  }

  documentLabel(type: number): string {
    return this.translation.translate(`doctype_${type}`);
  }

  fileSize(bytes: number): string {
    const kb = bytes / 1024;
    return kb < 1024 ? `${Math.round(kb)} KB` : `${(kb / 1024).toFixed(1)} MB`;
  }

  statusBadge(status: DriverStatus): string {
    switch (status) {
      case DriverStatus.Verified: return 'badge-verified';
      case DriverStatus.Rejected: return 'badge-rejected';
      default: return 'badge-pending';
    }
  }

  private async loadPreview(document: DriverDocument): Promise<void> {
    if (this.previews.has(document.id)) return;
    try {
      const blob = await firstValueFrom(this.api.driverDocumentContent(document.id));

      // The endpoint answers with the usual JSON envelope when it cannot serve
      // the file. As a blob that is not an error to catch — it is a JSON
      // document that would render as a broken image.
      if (blob.type.includes('json')) return;

      this.previews.set(document.id, URL.createObjectURL(blob));
      this.previewVersion.update((n) => n + 1);
    } catch {
      // A document whose bytes have gone missing shows as a placeholder tile
      // rather than taking the whole panel down with it.
    }
  }

  private releasePreviews(): void {
    for (const url of this.previews.values()) URL.revokeObjectURL(url);
    this.previews.clear();
    this.previewVersion.update((n) => n + 1);
  }
}
