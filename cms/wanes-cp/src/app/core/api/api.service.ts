import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import {
  AdminPage, AnalyticsBreakdowns, AnalyticsLeaderboards, AnalyticsOperations,
  AnalyticsOverview, AnalyticsTimeSeries, AppResponse, AuditRow, AuthResult,
  DeviceType, DriverRow, LookupOption, PageQuery, ResourceRecord, Roles,
} from './models';

type FilterValue = string | number | boolean | undefined | null;

/**
 * Single typed HTTP gateway. Named endpoints for the fixed screens, plus a
 * generic resource CRUD surface that the config-driven admin screens use so
 * every entity is reachable without a bespoke method each.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  // ── auth ──
  requestOtp = (phone: string): Observable<AppResponse> =>
    this.http.post<AppResponse>(`${this.base}Accounts/request-otp`, { phone });

  verifyOtp = (phone: string, code: string): Observable<AppResponse<AuthResult>> =>
    this.http.post<AppResponse<AuthResult>>(`${this.base}Accounts/verify-otp`, {
      phone, code, deviceType: DeviceType.Web,
    });

  logout = (): Observable<AppResponse> =>
    this.http.post<AppResponse>(`${this.base}Accounts/logout`, {});

  // ── admin: drivers (verification queue) ──
  pendingDrivers = (q: PageQuery): Observable<AppResponse<AdminPage<DriverRow>>> =>
    this.http.get<AppResponse<AdminPage<DriverRow>>>(`${this.base}admin/drivers/pending`,
      { params: this.toParams(q) });

  verifyDriver = (userId: number, approve: boolean): Observable<AppResponse> =>
    this.http.post<AppResponse>(`${this.base}admin/drivers/${userId}/verify`, { approve });

  // ── admin: audit ──
  audit = (q: PageQuery, action?: string): Observable<AppResponse<AdminPage<AuditRow>>> =>
    this.http.get<AppResponse<AdminPage<AuditRow>>>(`${this.base}admin/audit`,
      { params: this.toParams(q, { action }) });

  // ── admin: dashboard analytics ──
  // Five endpoints rather than one payload so each dashboard section loads on its
  // own; they share the `days` window so every number on the page agrees.
  analyticsOverview = (days: number): Observable<AppResponse<AnalyticsOverview>> =>
    this.http.get<AppResponse<AnalyticsOverview>>(`${this.base}admin/analytics/overview`,
      { params: new HttpParams().set('days', days) });

  analyticsTimeSeries = (days: number): Observable<AppResponse<AnalyticsTimeSeries>> =>
    this.http.get<AppResponse<AnalyticsTimeSeries>>(`${this.base}admin/analytics/timeseries`,
      { params: new HttpParams().set('days', days) });

  analyticsBreakdowns = (days: number): Observable<AppResponse<AnalyticsBreakdowns>> =>
    this.http.get<AppResponse<AnalyticsBreakdowns>>(`${this.base}admin/analytics/breakdowns`,
      { params: new HttpParams().set('days', days) });

  analyticsOperations = (days: number): Observable<AppResponse<AnalyticsOperations>> =>
    this.http.get<AppResponse<AnalyticsOperations>>(`${this.base}admin/analytics/operations`,
      { params: new HttpParams().set('days', days) });

  analyticsLeaderboards = (days: number): Observable<AppResponse<AnalyticsLeaderboards>> =>
    this.http.get<AppResponse<AnalyticsLeaderboards>>(`${this.base}admin/analytics/leaderboards`,
      { params: new HttpParams().set('days', days) });

  // ── admin: generic resource CRUD (config-driven screens) ──
  listResource = (route: string, q: PageQuery, filters?: Record<string, FilterValue>):
    Observable<AppResponse<AdminPage<ResourceRecord>>> =>
    this.http.get<AppResponse<AdminPage<ResourceRecord>>>(`${this.base}admin/${route}`,
      { params: this.toParams(q, filters) });

  getResource = (route: string, id: number): Observable<AppResponse<ResourceRecord>> =>
    this.http.get<AppResponse<ResourceRecord>>(`${this.base}admin/${route}/${id}`);

  createResource = (route: string, body: ResourceRecord): Observable<AppResponse<ResourceRecord>> =>
    this.http.post<AppResponse<ResourceRecord>>(`${this.base}admin/${route}`, body);

  updateResource = (route: string, id: number, body: ResourceRecord): Observable<AppResponse<ResourceRecord>> =>
    this.http.put<AppResponse<ResourceRecord>>(`${this.base}admin/${route}/${id}`, body);

  deleteResource = (route: string, id: number): Observable<AppResponse> =>
    this.http.delete<AppResponse>(`${this.base}admin/${route}/${id}`);

  // ── admin: reference lookups (option lists behind the form pickers) ──
  lookup = (type: string, search?: string, pageSize = 20):
    Observable<AppResponse<AdminPage<LookupOption>>> =>
    this.http.get<AppResponse<AdminPage<LookupOption>>>(`${this.base}admin/lookups/${type}`,
      { params: this.toParams({ pageNumber: 1, pageSize, search }) });

  /** Resolves a stored foreign key back to its label, for an edit form. */
  lookupOne = (type: string, id: number): Observable<AppResponse<LookupOption>> =>
    this.http.get<AppResponse<LookupOption>>(`${this.base}admin/lookups/${type}/${id}`);

  // ── admin: user role management ──
  setUserRole = (userId: number, role: Roles, grant: boolean): Observable<AppResponse<ResourceRecord>> =>
    this.http.post<AppResponse<ResourceRecord>>(`${this.base}admin/users/${userId}/roles`, { role, grant });

  private toParams(q: PageQuery, extra?: Record<string, FilterValue>): HttpParams {
    let params = new HttpParams()
      .set('pageNumber', q.pageNumber)
      .set('pageSize', q.pageSize);
    if (q.search) params = params.set('search', q.search);
    for (const [k, v] of Object.entries(extra ?? {})) {
      if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
    }
    return params;
  }
}
