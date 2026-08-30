import { Component, computed, input, signal } from '@angular/core';
import { ChartBase, Slice, exact, niceMax, slotColor } from './chart.types';

interface Band { x: number; y: number; w: number; h: number; path: string; fill: string; slice: Slice; index: number; }

/**
 * Column chart for one measure across ordered categories (hours, weekdays, star
 * ratings, status classes). One colour for the whole series — bar length already
 * encodes magnitude, so hue stays free. Bars are capped at 24px with a 2px
 * surface gap, and only the tallest bar is labelled; the rest are on the axis,
 * the tooltip, and the table twin.
 */
@Component({
  selector: 'app-column-chart',
  standalone: true,
  template: `
    <div class="viz-plot">
      <svg
        [attr.width]="width()"
        [attr.height]="height()"
        [attr.viewBox]="'0 0 ' + width() + ' ' + height()"
        role="img"
        [attr.aria-label]="title()">

        @for (t of yTicks(); track t.y) {
          <line class="grid" [attr.x1]="padL" [attr.x2]="width() - padR" [attr.y1]="t.y" [attr.y2]="t.y" />
          <text [attr.x]="padL - 8" [attr.y]="t.y + 4" text-anchor="end">{{ t.label }}</text>
        }

        <line class="axis"
          [attr.x1]="padL" [attr.x2]="width() - padR"
          [attr.y1]="baseline()" [attr.y2]="baseline()" />

        @for (b of bands(); track b.index) {
          @if (b.h > 0) {
            <path [attr.d]="b.path" [attr.fill]="b.fill"
              [attr.opacity]="hovered() === null || hovered() === b.index ? 1 : 0.55" />
          }
          @if (showEvery() === 1 || b.index % showEvery() === 0) {
            <text [attr.x]="b.x + b.w / 2" [attr.y]="height() - 8" text-anchor="middle">{{ b.slice.label }}</text>
          }
          <rect
            [attr.x]="b.x - gap" [attr.y]="padT" [attr.width]="b.w + gap * 2" [attr.height]="innerH()"
            fill="transparent"
            tabindex="0"
            [attr.aria-label]="b.slice.label + ': ' + b.slice.value"
            (pointerenter)="hovered.set(b.index)"
            (focus)="hovered.set(b.index)"
            (pointerleave)="hovered.set(null)"
            (blur)="hovered.set(null)" />
        }

        @if (peak(); as p) {
          <text [attr.x]="p.x + p.w / 2" [attr.y]="p.y - 7" text-anchor="middle" class="label-strong">
            {{ exact(p.slice.value) }}
          </text>
        }
      </svg>

      @if (active(); as b) {
        <div class="viz-tip" [style.left.px]="clampX(b.x + b.w / 2)" [style.top.px]="4">
          <div class="tip-head">{{ b.slice.sublabel || b.slice.label }}</div>
          <div class="tip-row">
            <span class="tip-key" [style.background]="b.fill"></span>
            <span class="tip-val">{{ exact(b.slice.value) }}</span>
            <span class="tip-name">{{ unitLabel() }}</span>
          </div>
        </div>
      }
    </div>
  `,
  styles: [':host { display: block; }'],
})
export class ColumnChartComponent extends ChartBase {
  readonly slices = input.required<Slice[]>();
  readonly height = input(220);
  readonly title = input('');
  readonly unitLabel = input('');
  /** Label every nth category on the x-axis; 1 labels them all. */
  readonly labelEvery = input(0);

  protected readonly padL = 48;
  protected readonly padR = 12;
  protected readonly padT = 22;
  protected readonly padB = 26;
  protected readonly gap = 2;
  protected readonly maxBar = 24;

  protected readonly exact = exact;
  protected readonly hovered = signal<number | null>(null);

  protected readonly baseline = computed(() => this.height() - this.padB);
  protected readonly innerH = computed(() => Math.max(1, this.height() - this.padT - this.padB));
  private readonly innerW = computed(() => Math.max(1, this.width() - this.padL - this.padR));
  private readonly max = computed(() => niceMax(Math.max(1, ...this.slices().map((s) => s.value))));

  protected readonly showEvery = computed(() => {
    if (this.labelEvery() > 0) return this.labelEvery();
    const perLabel = 34;
    return Math.max(1, Math.ceil((this.slices().length * perLabel) / this.innerW()));
  });

  /** Ticks are de-duplicated: on a 0–1 scale, 0 / 0.5 / 1 would print "1" twice. */
  protected readonly yTicks = computed(() => {
    const max = this.max();
    const steps = [...new Set([0, 0.5, 1].map((f) => Math.round(max * f)))];
    return steps.map((value) => ({
      y: this.baseline() - (value / max) * this.innerH(),
      label: exact(value),
    }));
  });

  protected readonly bands = computed<Band[]>(() => {
    const slices = this.slices();
    if (!slices.length) return [];
    const band = this.innerW() / slices.length;
    const barW = Math.max(3, Math.min(this.maxBar, band - this.gap * 2));

    return slices.map((slice, index) => {
      const h = Math.round((slice.value / this.max()) * this.innerH());
      const x = this.padL + index * band + (band - barW) / 2;
      const y = this.baseline() - h;
      return {
        index, x, y, w: barW, h, slice,
        fill: slice.color ?? slotColor(slice.slot ?? 0),
        path: topRounded(x, y, barW, h),
      };
    });
  });

  protected readonly active = computed(() => {
    const index = this.hovered();
    return index === null ? null : this.bands()[index] ?? null;
  });

  /** Exactly one direct label — the tallest bar. */
  protected readonly peak = computed(() => {
    const bands = this.bands();
    if (bands.length < 2) return null;
    return bands.reduce((best, b) => (b.h > best.h ? b : best), bands[0]);
  });
}

/** 4px rounded data-end, square where the bar meets the baseline. */
function topRounded(x: number, y: number, w: number, h: number): string {
  const r = Math.min(4, w / 2, h);
  return [
    `M${x},${y + h}`,
    `L${x},${y + r}`,
    `Q${x},${y} ${x + r},${y}`,
    `L${x + w - r},${y}`,
    `Q${x + w},${y} ${x + w},${y + r}`,
    `L${x + w},${y + h}`,
    'Z',
  ].join(' ');
}
