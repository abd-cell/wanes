import { AfterViewInit, Directive, ElementRef, HostListener, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

/** A named value — one bar, one slice, one leaderboard row. */
export interface Slice {
  label: string;
  value: number;
  /** 0-based categorical slot; omit to paint every mark with slot 1. */
  slot?: number;
  sublabel?: string;
  /** Overrides the categorical slot — only for reserved status colours. */
  color?: string;
}

/** One line on a time chart. Points align 1:1 with the shared x labels. */
export interface LineSeries {
  name: string;
  slot: number;
  values: number[];
}

/** The WCAG-clean twin every chart ships with. */
export interface TableView {
  columns: string[];
  rows: (string | number)[][];
}

/**
 * Fixed categorical order — a series keeps its hue when a filter removes its
 * neighbours, and slots are never cycled. Past six, fold the tail into "Other".
 */
export const SERIES_SLOTS = 6;

export const slotColor = (slot: number): string => `var(--viz-${(slot % SERIES_SLOTS) + 1})`;

/** Compact display form for tile values: 1,284 · 12.9K · 4.2M. */
export function compact(value: number): string {
  const abs = Math.abs(value);
  if (abs >= 1_000_000) return `${trim(value / 1_000_000)}M`;
  if (abs >= 10_000) return `${trim(value / 1_000)}K`;
  return Math.round(value).toLocaleString('en-US');
}

/** Axis ticks and table cells stay exact — only tiles get compacted. */
export const exact = (value: number): string =>
  Number.isInteger(value) ? value.toLocaleString('en-US') : value.toLocaleString('en-US', { maximumFractionDigits: 2 });

export const percent = (fraction: number, digits = 1): string => `${(fraction * 100).toFixed(digits)}%`;

export const signedPercent = (fraction: number): string =>
  `${fraction >= 0 ? '+' : ''}${(fraction * 100).toFixed(1)}%`;

const trim = (n: number): string => (Math.abs(n) >= 100 ? n.toFixed(0) : n.toFixed(1).replace(/\.0$/, ''));

/** Rounded "nice" upper bound so y-ticks land on 0 / 50 / 100 rather than 0 / 47 / 94. */
export function niceMax(max: number): number {
  if (max <= 0) return 1;
  const magnitude = 10 ** Math.floor(Math.log10(max));
  for (const step of [1, 2, 2.5, 5, 10]) {
    const candidate = step * magnitude;
    if (candidate >= max) return candidate;
  }
  return 10 * magnitude;
}

/**
 * Charts render at their real pixel width so strokes and type stay at their
 * specified size instead of being scaled by a viewBox. Width is re-measured on
 * window resize; the SSR pass uses a sane default and re-measures on hydration.
 */
@Directive()
export abstract class ChartBase implements AfterViewInit {
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly width = signal(560);

  ngAfterViewInit(): void {
    this.measure();
  }

  /** Keeps a centre-anchored tooltip from hanging off either edge of the card. */
  protected clampX(x: number, half = 72): number {
    return Math.min(Math.max(x, half), Math.max(half, this.width() - half));
  }

  @HostListener('window:resize')
  protected measure(): void {
    if (!this.isBrowser) return;
    const w = this.host.nativeElement.clientWidth;
    if (w > 0) this.width.set(Math.max(260, w));
  }
}
