import { RenderMode, ServerRoute } from '@angular/ssr';

// All routes are server-rendered (not prerendered) because they carry the
// :languageCode parameter and depend on the authenticated session.
export const serverRoutes: ServerRoute[] = [
  {
    path: '**',
    renderMode: RenderMode.Server,
  },
];
