import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { GlobalService } from '../services/global.service';
import { TranslationService } from '../services/translation.service';

/** Attaches the Bearer token and the Accept-Language header. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const global = inject(GlobalService);
  const translation = inject(TranslationService);

  const token = global.token;
  const headers: Record<string, string> = { 'Accept-Language': translation.lang() };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  return next(req.clone({ setHeaders: headers }));
};
