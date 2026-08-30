import { Component, computed, input } from '@angular/core';
import { Slice, exact, slotColor } from './chart.types';

/**
 * Ranked horizontal bars — the right form for named categories with long labels
 * (endpoints, routes, audit actions, top drivers). One colour for the whole list:
 * bar length carries the magnitude, so hue is not spent restating it. Every row
 * is directly labelled, which is why this form needs no tooltip.
 */
@Component({
  selector: 'app-bar-list',
  standalone: true,
  template: `
    @if (rows().length) {
      <ol class="bars">
        @for (r of scaled(); track $index) {
          <li>
            <span class="name" [title]="r.slice.label">{{ r.slice.label }}</span>
            <span class="track">
              <span class="fill" [style.width.%]="r.pct" [style.background]="r.fill"></span>
            </span>
            <span class="value">{{ exact(r.slice.value) }}{{ unit() }}</span>
            @if (r.slice.sublabel) { <span class="sub">{{ r.slice.sublabel }}</span> }
          </li>
        }
      </ol>
    } @else {
      <p class="empty-note">{{ emptyLabel() }}</p>
    }
  `,
  styles: [`
    :host { display: block; }
    .bars { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 9px; }
    .bars li {
      display: grid; grid-template-columns: minmax(90px, 1.3fr) minmax(60px, 2fr) auto;
      align-items: center; gap: 10px; font-size: 13px;
    }
    .name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--app-ink); }
    .track { height: 10px; border-radius: 999px; background: var(--viz-track); overflow: hidden; }
    .fill { display: block; height: 100%; border-radius: 999px; }
    .value { font-weight: 700; font-variant-numeric: tabular-nums; }
    .sub {
      grid-column: 1 / -1; margin-top: -6px; font-size: 11.5px;
      color: var(--app-ink-soft); overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }
    .empty-note { color: var(--app-ink-soft); font-size: 13px; margin: 24px 0; }
  `],
})
export class BarListComponent {
  readonly rows = input.required<Slice[]>();
  readonly unit = input('');
  readonly emptyLabel = input('');

  protected readonly exact = exact;

  protected readonly scaled = computed(() => {
    const max = Math.max(1, ...this.rows().map((r) => r.value));
    return this.rows().map((slice) => ({
      slice,
      pct: Math.max(slice.value > 0 ? 2 : 0, (slice.value / max) * 100),
      fill: slice.color ?? slotColor(slice.slot ?? 0),
    }));
  });
}
