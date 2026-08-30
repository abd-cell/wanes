import { Component, computed, input, signal } from '@angular/core';
import { ChartBase, LineSeries, exact, niceMax, slotColor } from './chart.types';

interface Tick { y: number; value: string; }
interface XTick { x: number; label: string; anchor: 'start' | 'middle' | 'end'; }
interface EndLabel { x: number; y: number; text: string; color: string; }
interface Hover { index: number; x: number; label: string; rows: { name: string; value: string; color: string; y: number }[]; }

/**
 * Daily line chart. One y-axis only — a second measure gets its own chart rather
 * than a second scale. The crosshair snaps to the nearest day and the readout
 * lists every series at that day, so the pointer never has to land on a line.
 */
@Component({
  selector: 'app-line-chart',
  standalone: true,
  template: `
    <div class="viz-plot">
      <svg
        [attr.width]="width()"
        [attr.height]="height()"
        [attr.viewBox]="'0 0 ' + width() + ' ' + height()"
        role="img"
        tabindex="0"
        [attr.aria-label]="ariaLabel()"
        (pointermove)="track($event)"
        (pointerleave)="hover.set(null)"
        (keydown)="step($event)"
        (blur)="hover.set(null)">

        @for (t of yTicks(); track t.y) {
          <line class="grid" [attr.x1]="padL" [attr.x2]="width() - padR" [attr.y1]="t.y" [attr.y2]="t.y" />
          <text [attr.x]="padL - 8" [attr.y]="t.y + 4" text-anchor="end">{{ t.value }}</text>
        }

        <line class="axis"
          [attr.x1]="padL" [attr.x2]="width() - padR"
          [attr.y1]="baseline()" [attr.y2]="baseline()" />

        @for (t of xTicks(); track t.x) {
          <text [attr.x]="t.x" [attr.y]="height() - 7" [attr.text-anchor]="t.anchor">{{ t.label }}</text>
        }

        @if (series().length === 1) {
          <path [attr.d]="areaPath(series()[0])" [attr.fill]="slotColor(series()[0].slot)" opacity="0.1" />
        }

        @for (s of series(); track s.name) {
          <path
            [attr.d]="linePath(s)"
            fill="none"
            [attr.stroke]="slotColor(s.slot)"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round" />
        }

        @for (l of endLabels(); track l.text) {
          <text [attr.x]="l.x + 8" [attr.y]="l.y + 4" class="label-strong">{{ l.text }}</text>
        }

        @if (hover(); as h) {
          <line class="axis" [attr.x1]="h.x" [attr.x2]="h.x" [attr.y1]="padT" [attr.y2]="baseline()" />
          @for (r of h.rows; track r.name) {
            <circle [attr.cx]="h.x" [attr.cy]="r.y" r="5"
              [attr.fill]="r.color" stroke="var(--viz-surface)" stroke-width="2" />
          }
        }
      </svg>

      @if (hover(); as h) {
        <div class="viz-tip" [style.left.px]="clampX(h.x)" [style.top.px]="4">
          <div class="tip-head">{{ h.label }}</div>
          @for (r of h.rows; track r.name) {
            <div class="tip-row">
              <span class="tip-key" [style.background]="r.color"></span>
              <span class="tip-val">{{ r.value }}</span>
              <span class="tip-name">{{ r.name }}</span>
            </div>
          }
        </div>
      }
    </div>

    @if (series().length > 1) {
      <div class="viz-legend">
        @for (s of series(); track s.name) {
          <span class="item">
            <span class="key-line" [style.background]="slotColor(s.slot)"></span>{{ s.name }}
          </span>
        }
      </div>
    }
  `,
  styles: [':host { display: block; }'],
})
export class LineChartComponent extends ChartBase {
  readonly labels = input.required<string[]>();
  readonly series = input.required<LineSeries[]>();
  readonly height = input(230);
  readonly title = input('');

  protected readonly padL = 48;
  protected readonly padR = 16;
  protected readonly padT = 16;
  protected readonly padB = 28;

  protected readonly hover = signal<Hover | null>(null);
  protected readonly slotColor = slotColor;

  protected readonly baseline = computed(() => this.height() - this.padB);
  private readonly innerW = computed(() => Math.max(1, this.width() - this.padL - this.padR));
  private readonly innerH = computed(() => Math.max(1, this.height() - this.padT - this.padB));

  private readonly max = computed(() =>
    niceMax(Math.max(1, ...this.series().flatMap((s) => s.values))));

  protected readonly ariaLabel = computed(() =>
    `${this.title()} — ${this.series().map((s) => s.name).join(', ')}`);

  /** Ticks are de-duplicated: on a 0–1 scale, 0 / 0.5 / 1 would print "1" twice. */
  protected readonly yTicks = computed<Tick[]>(() => {
    const max = this.max();
    const steps = [...new Set([0, 0.5, 1].map((f) => Math.round(max * f)))];
    return steps.map((value) => ({ y: this.y(value), value: exact(value) }));
  });

  /**
   * The first and last ticks are edge-anchored — centring them would push the
   * first label under the y-axis numbers and the last one past the plot edge.
   */
  protected readonly xTicks = computed<XTick[]>(() => {
    const labels = this.labels();
    if (!labels.length) return [];
    const last = labels.length - 1;
    const every = Math.max(1, Math.ceil(labels.length / 6));
    return labels
      .map((label, i) => ({ x: this.x(i), label, i }))
      .filter((t) => t.i % every === 0 || t.i === last)
      .map((t) => ({
        x: t.x,
        label: t.label,
        anchor: (t.i === 0 ? 'start' : t.i === last ? 'end' : 'middle') as XTick['anchor'],
      }));
  });

  /** End labels only when they cannot collide; otherwise the legend carries identity. */
  protected readonly endLabels = computed<EndLabel[]>(() => {
    const series = this.series();
    const last = this.labels().length - 1;
    if (last < 0 || series.length > 4 || this.padR < 40) return [];
    const placed = series.map((s) => ({
      x: this.x(last),
      y: this.y(s.values[last] ?? 0),
      text: exact(s.values[last] ?? 0),
      color: slotColor(s.slot),
    }));
    const ys = placed.map((p) => p.y).sort((a, b) => a - b);
    for (let i = 1; i < ys.length; i++) if (ys[i] - ys[i - 1] < 14) return [];
    return placed;
  });

  protected linePath(s: LineSeries): string {
    return s.values.map((v, i) => `${i ? 'L' : 'M'}${this.x(i)},${this.y(v)}`).join(' ');
  }

  protected areaPath(s: LineSeries): string {
    if (!s.values.length) return '';
    const last = s.values.length - 1;
    return `${this.linePath(s)} L${this.x(last)},${this.baseline()} L${this.x(0)},${this.baseline()} Z`;
  }

  protected track(event: PointerEvent): void {
    const rect = (event.currentTarget as SVGElement).getBoundingClientRect();
    this.select(this.nearest(event.clientX - rect.left));
  }

  protected step(event: KeyboardEvent): void {
    const current = this.hover()?.index ?? 0;
    if (event.key === 'ArrowRight') this.select(Math.min(this.labels().length - 1, current + 1));
    else if (event.key === 'ArrowLeft') this.select(Math.max(0, current - 1));
    else if (event.key === 'Escape') this.hover.set(null);
    else return;
    event.preventDefault();
  }

  private select(index: number): void {
    const labels = this.labels();
    if (index < 0 || index >= labels.length) return;
    this.hover.set({
      index,
      x: this.x(index),
      label: labels[index],
      rows: this.series().map((s) => ({
        name: s.name,
        value: exact(s.values[index] ?? 0),
        color: slotColor(s.slot),
        y: this.y(s.values[index] ?? 0),
      })),
    });
  }

  private nearest(px: number): number {
    const n = this.labels().length;
    if (n < 2) return 0;
    const stepWidth = this.innerW() / (n - 1);
    return Math.min(n - 1, Math.max(0, Math.round((px - this.padL) / stepWidth)));
  }

  private x(index: number): number {
    const n = this.labels().length;
    if (n < 2) return this.padL + this.innerW() / 2;
    return this.padL + (index * this.innerW()) / (n - 1);
  }

  private y(value: number): number {
    return this.padT + this.innerH() - (value / this.max()) * this.innerH();
  }
}
