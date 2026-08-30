import { Component, input, signal } from '@angular/core';
import { TranslatePipe } from '../../../core/pipes/translate.pipe';
import { TableView } from './chart.types';

/**
 * Card shell around one chart: title, optional subtitle, and the chart/table
 * switch. Every chart ships a table twin so no value is reachable by colour or
 * hover alone.
 */
@Component({
  selector: 'app-chart-card',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    <section class="card viz-card">
      <header class="viz-card-head">
        <h3>{{ titleKey() | translate }}</h3>
        @if (subtitle()) { <span class="sub">{{ subtitle() }}</span> }
        <span class="spacer"></span>
        <button
          type="button"
          class="viz-toggle"
          [attr.aria-pressed]="showTable()"
          (click)="showTable.set(!showTable())">
          {{ (showTable() ? 'viz_view_chart' : 'viz_view_table') | translate }}
        </button>
      </header>

      @if (showTable()) {
        <div class="viz-table-wrap">
          <table class="viz-table">
            <thead>
              <tr>
                @for (c of table().columns; track $index) {
                  <th [class.num]="$index > 0">{{ c }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of table().rows; track $index) {
                <tr>
                  @for (cell of row; track $index) {
                    <td [class.num]="$index > 0">{{ cell }}</td>
                  }
                </tr>
              }
              @if (!table().rows.length) {
                <tr><td [attr.colspan]="table().columns.length">{{ 'list_empty' | translate }}</td></tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <ng-content />
      }
    </section>
  `,
})
export class ChartCardComponent {
  readonly titleKey = input.required<string>();
  readonly subtitle = input<string>('');
  readonly table = input.required<TableView>();

  readonly showTable = signal(false);
}
