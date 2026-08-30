import { Component, computed, input, signal } from '@angular/core';
import { ChartBase, Slice, exact, percent, slotColor } from './chart.types';

interface Segment { path: string; fill: string; slice: Slice; share: number; index: number; }

/**
 * Part-to-whole at a glance, capped at six segments. Segments are separated by a
 * 2px gap in the surface colour rather than a stroke, and the legend carries the
 * value beside each name so identity never rests on hue alone.
 */
@Component({
  selector: 'app-donut-chart',
  standalone: true,
  template: `
    <div class="viz-plot donut">
      @if (total() > 0) {
        <svg
          [attr.width]="size()"
          [attr.height]="size()"
          [attr.viewBox]="'0 0 ' + size() + ' ' + size()"
          role="img"
          [attr.aria-label]="title()">
          @for (s of segments(); track s.index) {
            <path [attr.d]="s.path" [attr.fill]="s.fill"
              [attr.opacity]="hovered() === null || hovered() === s.index ? 1 : 0.5"
              tabindex="0"
              [attr.aria-label]="s.slice.label + ': ' + s.slice.value"
              (pointerenter)="hovered.set(s.index)"
              (focus)="hovered.set(s.index)"
              (pointerleave)="hovered.set(null)"
              (blur)="hovered.set(null)" />
          }
          <text [attr.x]="size() / 2" [attr.y]="size() / 2 - 2" text-anchor="middle"
            class="label-strong" style="font-size:20px">{{ exact(total()) }}</text>
          <text [attr.x]="size() / 2" [attr.y]="size() / 2 + 16" text-anchor="middle">
            {{ centerLabel() }}
          </text>
        </svg>

        @if (active(); as s) {
          <div class="viz-tip" [style.left.px]="clampX(width() / 2)" [style.top.px]="4">
            <div class="tip-head">{{ s.slice.label }}</div>
            <div class="tip-row">
              <span class="tip-key" [style.background]="s.fill"></span>
              <span class="tip-val">{{ exact(s.slice.value) }}</span>
              <span class="tip-name">{{ percent(s.share) }}</span>
            </div>
          </div>
        }
      } @else {
        <p class="empty-note">{{ emptyLabel() }}</p>
      }
    </div>

    <div class="viz-legend">
      @for (s of segments(); track s.index) {
        <span class="item">
          <span class="swatch" [style.background]="s.fill"></span>{{ s.slice.label }}
          <strong>{{ exact(s.slice.value) }}</strong>
        </span>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .donut { display: flex; justify-content: center; }
    .empty-note { color: var(--app-ink-soft); font-size: 13px; margin: 40px 0; }
  `],
})
export class DonutChartComponent extends ChartBase {
  readonly slices = input.required<Slice[]>();
  readonly title = input('');
  readonly centerLabel = input('');
  readonly emptyLabel = input('');

  protected readonly exact = exact;
  protected readonly percent = percent;
  protected readonly hovered = signal<number | null>(null);

  protected readonly size = computed(() => Math.min(200, Math.max(150, this.width() - 40)));
  protected readonly total = computed(() => this.slices().reduce((sum, s) => sum + s.value, 0));

  protected readonly segments = computed<Segment[]>(() => {
    const total = this.total();
    if (total <= 0) return [];

    const size = this.size();
    const cx = size / 2;
    const outer = size / 2 - 2;
    const inner = outer * 0.62;
    const pad = 2 / outer;   // 2px of surface between neighbouring segments

    let angle = -Math.PI / 2;
    return this.slices().map((slice, index) => {
      const sweep = (slice.value / total) * Math.PI * 2;
      const start = angle + (sweep > pad * 2 ? pad / 2 : 0);
      const end = angle + sweep - (sweep > pad * 2 ? pad / 2 : 0);
      angle += sweep;
      return {
        index,
        slice,
        share: slice.value / total,
        fill: slice.color ?? slotColor(slice.slot ?? index),
        path: slice.value > 0 ? ring(cx, cx, outer, inner, start, end) : '',
      };
    });
  });

  protected readonly active = computed(() => {
    const index = this.hovered();
    return index === null ? null : this.segments()[index] ?? null;
  });
}

function ring(cx: number, cy: number, outer: number, inner: number, start: number, end: number): string {
  const large = end - start > Math.PI ? 1 : 0;
  const o1 = polar(cx, cy, outer, start);
  const o2 = polar(cx, cy, outer, end);
  const i2 = polar(cx, cy, inner, end);
  const i1 = polar(cx, cy, inner, start);
  return [
    `M${o1.x},${o1.y}`,
    `A${outer},${outer} 0 ${large} 1 ${o2.x},${o2.y}`,
    `L${i2.x},${i2.y}`,
    `A${inner},${inner} 0 ${large} 0 ${i1.x},${i1.y}`,
    'Z',
  ].join(' ');
}

const polar = (cx: number, cy: number, r: number, angle: number) => ({
  x: cx + r * Math.cos(angle),
  y: cy + r * Math.sin(angle),
});
