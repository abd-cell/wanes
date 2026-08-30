import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { signedPercent } from './chart.types';

/**
 * Label · value · optional delta · optional 12-point sparkline. The delta pairs
 * its colour with an arrow glyph and a named comparison period, so direction
 * never rests on colour alone. Values use proportional figures — `tabular-nums`
 * would make a large standalone number look loose.
 */
@Component({
  selector: 'app-stat-tile',
  standalone: true,
  imports: [NgTemplateOutlet, RouterLink],
  template: `
    <ng-template #body>
      <span class="tile-label">{{ label() }}</span>
      <span class="tile-value">{{ value() }}</span>
      <span class="tile-foot">
        @if (delta() !== null && delta() !== undefined) {
          <span class="tile-delta" [class]="direction()">{{ arrow() }} {{ deltaText() }}</span>
        }
        @if (note()) { <span class="tile-note">{{ note() }}</span> }
      </span>
      @if (meter() !== null) {
        <span class="viz-meter"><span [style.width.%]="meterPct()"></span></span>
      }
      @if (spark().length > 1) {
        <svg class="spark" width="100%" height="30" [attr.viewBox]="'0 0 ' + sparkW + ' 30'"
          preserveAspectRatio="none" aria-hidden="true">
          <path [attr.d]="sparkPath()" fill="none" stroke="var(--viz-axis)"
            stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
          <path [attr.d]="sparkTail()" fill="none" stroke="var(--viz-1)"
            stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      }
    </ng-template>

    @if (link().length) {
      <a class="viz-tile" [class.hero]="hero()" [routerLink]="link()">
        <ng-container [ngTemplateOutlet]="body" />
      </a>
    } @else {
      <div class="viz-tile" [class.hero]="hero()">
        <ng-container [ngTemplateOutlet]="body" />
      </div>
    }
  `,
  styles: [`
    :host { display: contents; }
    .spark { display: block; margin-top: 10px; overflow: visible; }
  `],
})
export class StatTileComponent {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly note = input('');
  readonly delta = input<number | null>(null);
  /** Flips the delta colour for metrics where a rise is bad (errors, cancellations). */
  readonly goodWhenUp = input(true);
  readonly spark = input<number[]>([]);
  readonly meter = input<number | null>(null);
  readonly hero = input(false);
  readonly link = input<string[]>([]);

  protected readonly sparkW = 160;

  protected readonly deltaText = computed(() => signedPercent(this.delta() ?? 0));

  protected readonly arrow = computed(() => {
    const d = this.delta() ?? 0;
    return d > 0 ? '▲' : d < 0 ? '▼' : '■';
  });

  protected readonly direction = computed(() => {
    const d = this.delta() ?? 0;
    if (d === 0) return 'flat';
    return d > 0 === this.goodWhenUp() ? 'up' : 'down';
  });

  protected readonly meterPct = computed(() =>
    Math.max(0, Math.min(100, (this.meter() ?? 0) * 100)));

  protected readonly sparkPath = computed(() => this.path(0));

  /** The most recent quarter of the window, drawn in the series hue. */
  protected readonly sparkTail = computed(() =>
    this.path(Math.max(0, this.spark().length - Math.ceil(this.spark().length / 4) - 1)));

  private path(from: number): string {
    const values = this.spark();
    if (values.length < 2) return '';
    const max = Math.max(1, ...values);
    const step = this.sparkW / (values.length - 1);
    return values
      .map((v, i) => ({ x: i * step, y: 28 - (v / max) * 26 }))
      .slice(from)
      .map((p, i) => `${i ? 'L' : 'M'}${p.x.toFixed(1)},${p.y.toFixed(1)}`)
      .join(' ');
  }
}
