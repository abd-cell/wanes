import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { GlobalService } from './core/services/global.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <router-outlet />
    <div class="toast-host">
      @for (t of global.toasts(); track t.id) {
        <div class="toast" [class.success]="t.kind === 'success'" [class.error]="t.kind === 'error'">
          {{ t.text }}
        </div>
      }
    </div>
  `,
})
export class App {
  protected readonly global = inject(GlobalService);
}
