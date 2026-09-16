import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environment';
import { SKIP_AUTH_HANDLING } from '../interceptors/auth-context';
import {
  AdminPage, AnalyticsBreakdowns, AnalyticsLeaderboards, AnalyticsOperations,
  AnalyticsOverview, AnalyticsTimeSeries, AppConfiguration, AppResponse, AuditRow,
  AuthResult, BroadcastInput, BroadcastResult, BulkNotificationInput, BulkNotificationResult,
  DeviceType, DriverDocument, DriverRow, DriverStatus, LookupOption, NotificationStats, PageQuery,
  ResourceRecord, Roles,
  TargetedSendInput, TargetedSendResult,
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

  /**
   * Trades the refresh token for a new pair. Skips the auth interceptors: the
   * access token is expired by definition here, and a 401 on this call is the
   * end of the session rather than something to retry.
   */
  refreshSession = (refreshToken: string): Observable<AppResponse<AuthResult>> =>
    this.http.post<AppResponse<AuthResult>>(`${this.base}Accounts/refresh`,
      { refreshToken },
      { context: new HttpContext().set(SKIP_AUTH_HANDLING, true) });

  // ── platform configuration ──
  /**
   * Public read, used at bootstrap before anyone has signed in. Opted out of the
   * auth interceptors: a settings fetch that fails is a degraded brand, not the
   * end of a session, and it must not toast or redirect to login.
   */
  configuration = (): Observable<AppResponse<AppConfiguration>> =>
    this.http.get<AppResponse<AppConfiguration>>(`${this.base}configuration`,
      { context: new HttpContext().set(SKIP_AUTH_HANDLING, true) });

  updateConfiguration = (body: AppConfiguration): Observable<AppResponse<AppConfiguration>> =>
    this.http.put<AppResponse<AppConfiguration>>(`${this.base}admin/configuration`, body);

  // ── admin: drivers (verification queue) ──
  /** Defaults to applications awaiting a decision; pass a status to reopen the decided ones. */
  pendingDrivers = (q: PageQuery, status?: DriverStatus): Observable<AppResponse<AdminPage<DriverRow>>> =>
    this.http.get<AppResponse<AdminPage<DriverRow>>>(`${this.base}admin/drivers/pending`,
      { params: this.toParams(q, { status }) });

  driverDocuments = (userId: number): Observable<AppResponse<DriverDocument[]>> =>
    this.http.get<AppResponse<DriverDocument[]>>(`${this.base}admin/drivers/${userId}/documents`);

  /**
   * The bytes of one document.
   *
   * Fetched as a blob and shown from an object URL rather than handed to an
   * `<img src>`: the endpoint is admin-only, and an image tag cannot carry the
   * bearer token the interceptor puts on this call. It is also the only
   * response in the app that is not the JSON envelope — the body is a JPEG.
   */
  driverDocumentContent = (documentId: number): Observable<Blob> =>
    this.http.get(`${this.base}admin/drivers/documents/${documentId}/content`,
      { responseType: 'blob' });

  verifyDriver = (userId: number, approve: boolean, note?: string): Observable<AppResponse> =>
    this.http.post<AppResponse>(`${this.base}admin/drivers/${userId}/verify`, { approve, note });

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

  /** One notification to a whole audience; resolves with the recipient count. */
  broadcastNotification = (body: BroadcastInput): Observable<AppResponse<BroadcastResult>> =>
    this.http.post<AppResponse<BroadcastResult>>(`${this.base}admin/notifications/broadcast`, body);

  /** One notification to a hand-picked set of users; resolves with the recipient count. */
  sendTargetedNotification = (body: TargetedSendInput): Observable<AppResponse<TargetedSendResult>> =>
    this.http.post<AppResponse<TargetedSendResult>>(`${this.base}admin/notifications/send`, body);

  /** Marks read/unread or deletes every row the admin ticked. */
  bulkNotifications = (body: BulkNotificationInput): Observable<AppResponse<BulkNotificationResult>> =>
    this.http.post<AppResponse<BulkNotificationResult>>(`${this.base}admin/notifications/bulk`, body);

  /**
   * Headline counts for the notification manager. Separate from the list call
   * because the totals describe the whole table, not the page on screen.
   */
  notificationStats = (): Observable<AppResponse<NotificationStats>> =>
    this.http.get<AppResponse<NotificationStats>>(`${this.base}admin/notifications/stats`);

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
