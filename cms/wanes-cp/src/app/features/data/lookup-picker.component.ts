import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/api/api.service';
import { TranslatePipe } from '../../core/pipes/translate.pipe';
import { LookupOption } from '../../core/api/models';

const SEARCH_DEBOUNCE_MS = 250;

/**
 * Reference picker: the admin searches records by their human details — a name,
 * a route, a plate — and the form keeps the foreign key. `type` is a backend
 * lookup type (users, drivers, riders, vehicles, trips, bookings, requests).
 */
@Component({
  selector: 'app-lookup-picker',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  template: `
    <div class="picker">
      <input class="field" type="text" autocomplete="off"
             [ngModel]="open() ? term() : (selectedLabel() ?? '')"
             (ngModelChange)="onTerm($event)"
             (focus)="onFocus()" (blur)="onBlur()"
             (keydown.escape)="open.set(false)"
             [placeholder]="selectedLabel() ?? (placeholderKey() | translate)" />

      @if (value() !== null && value() !== undefined) {
        <button type="button" class="picker-clear" (mousedown)="clear()"
                [title]="'action_clear' | translate">×</button>
      }

      @if (open()) {
        <div class="picker-menu">
          @if (loading()) {
            <div class="picker-empty">{{ 'common_loading' | translate }}</div>
          } @else if (options().length === 0) {
            <div class="picker-empty">{{ 'list_empty' | translate }}</div>
          } @else {
            @for (o of options(); track o.id) {
              <button type="button" class="picker-option" [class.selected]="o.id === value()"
                      (mousedown)="select(o)">
                <span class="picker-label">{{ o.label }}</span>
                @if (o.description) { <span class="picker-desc">{{ o.description }}</span> }
              </button>
            }
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .picker { position: relative; }
    .picker .field { width: 100%; padding-inline-end: 30px; }
    .picker-clear {
      position: absolute; inset-inline-end: 6px; top: 50%; transform: translateY(-50%);
      width: 22px; height: 22px; line-height: 1; border: 0; border-radius: 6px;
      background: transparent; color: var(--app-ink-soft); font-size: 17px; cursor: pointer;
    }
    .picker-clear:hover { background: var(--app-surface-2); color: var(--app-ink); }
    .picker-menu {
      position: absolute; z-index: 20; inset-inline: 0; top: calc(100% + 4px);
      max-height: 260px; overflow-y: auto;
      background: var(--app-surface); border: 1px solid var(--app-line);
      border-radius: 10px; box-shadow: var(--app-shadow); padding: 4px;
    }
    .picker-empty { padding: 12px; text-align: center; color: var(--app-ink-soft); font-size: 13px; }
    .picker-option {
      display: flex; flex-direction: column; gap: 2px; width: 100%;
      padding: 8px 10px; border: 0; border-radius: 8px; background: transparent;
      text-align: start; cursor: pointer; color: var(--app-ink);
    }
    .picker-option:hover { background: var(--app-surface-2); }
    .picker-option.selected { background: color-mix(in srgb, var(--app-primary) 14%, transparent); }
    .picker-label { font-weight: 600; font-size: 13.5px; }
    .picker-desc { font-size: 12px; color: var(--app-ink-soft); }
  `],
})
export class LookupPickerComponent {
  private readonly api = inject(ApiService);

  readonly type = input.required<string>();
  readonly value = input<number | null>(null);
  readonly placeholderKey = input('lookup_search');
  readonly valueChange = output<number | null>();

  readonly term = signal('');
  readonly options = signal<LookupOption[]>([]);
  readonly open = signal(false);
  readonly loading = signal(false);
  readonly selectedLabel = signal<string | null>(null);

  /** The id `selectedLabel` was resolved for — keeps our own picks from re-fetching. */
  private labelledId: number | null = null;
  /** Guards against a slow request landing after a newer one and overwriting it. */
  private queryToken = 0;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private blurTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    // A value arriving from outside (an edit form opening) needs its label resolved.
    effect(() => {
      const id = this.value() ?? null;
      if (id === this.labelledId) return;
      this.labelledId = id;
      if (id === null) this.selectedLabel.set(null);
      else this.resolveLabel(id);
    });
  }

  onFocus(): void {
    clearTimeout(this.blurTimer);
    this.open.set(true);
    this.term.set('');
    if (this.options().length === 0) this.query('');
  }

  onBlur(): void {
    // Let a click on an option land before the menu closes.
    this.blurTimer = setTimeout(() => this.open.set(false), 150);
  }

  onTerm(text: string): void {
    this.term.set(text);
    this.open.set(true);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.query(text), SEARCH_DEBOUNCE_MS);
  }

  select(option: LookupOption): void {
    this.labelledId = option.id;
    this.selectedLabel.set(option.label);
    this.term.set('');
    this.open.set(false);
    this.valueChange.emit(option.id);
  }

  clear(): void {
    this.labelledId = null;
    this.selectedLabel.set(null);
    this.term.set('');
    this.valueChange.emit(null);
  }

  private async query(search: string): Promise<void> {
    const token = ++this.queryToken;
    this.loading.set(true);
    try {
      const res = await firstValueFrom(this.api.lookup(this.type(), search || undefined));
      if (token !== this.queryToken) return;
      this.options.set(res.success && res.data ? res.data.data : []);
    } finally {
      if (token === this.queryToken) this.loading.set(false);
    }
  }

  private async resolveLabel(id: number): Promise<void> {
    const res = await firstValueFrom(this.api.lookupOne(this.type(), id));
    // Ignore a late response for an id we have since moved off.
    if (this.labelledId !== id) return;
    this.selectedLabel.set(res.success && res.data ? res.data.label : `#${id}`);
  }
}
