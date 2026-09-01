// ── Response envelope (mirrors backend BaseResponse<T>) ──
export interface AppResponse<T = unknown> {
  success: boolean;
  data?: T;
  errorCode: number;
  message?: string;
  errors?: string[];
}

/** Paged admin list payload (matches backend PageOutput<T>). */
export interface AdminPage<T> {
  data: T[];
  totalRows: number;
}

export interface PageQuery {
  pageNumber: number;
  pageSize: number;
  search?: string;
}

/** A generic admin row / form model — the admin screens are config-driven. */
export type ResourceRecord = Record<string, unknown>;

/**
 * One option in a reference picker (matches backend LookupRow). `label` is what
 * the admin reads — a name, a route — and `description` is the secondary line
 * that tells two similar records apart. `id` is the foreign key we actually send.
 */
export interface LookupOption {
  id: number;
  label: string;
  description?: string;
}

/**
 * Platform settings the admin owns (matches backend AppConfigurationOutput).
 * Read anonymously by every client, written only from the CMS settings screen.
 */
export interface AppConfiguration {
  currencyCode: string;
  currencySymbol: string;
  currencyPosition: CurrencyPosition;
  /** Fraction digits, 0-3. */
  currencyDecimals: number;
  /** Brand primary as `#RRGGBB`; the other brand shades are derived from it. */
  primaryColor: string;
  /**
   * The display/body typeface, as a pairing of a Latin and an Arabic face —
   * see `core/services/app-font.ts` for what each value resolves to here.
   */
  fontFamily: AppFont;
  /**
   * Minutes an unanswered hail stays open before it expires and stops being
   * offered to drivers. 1-240; the backend clamps anything outside that.
   */
  hailRequestTtlMinutes: number;
  /**
   * Support channels the app's "Contact us" screen offers. Each one is optional
   * and empty until an admin fills it in; a client hides the channel it has no
   * value for rather than showing a row that goes nowhere.
   */
  supportPhone?: string;
  supportWhatsApp?: string;
  supportEmail?: string;
  supportWebsite?: string;
  /** Free text, e.g. "Sun-Thu, 9:00-17:00". */
  supportHours?: string;
  updatedAt?: string;
}

export enum CurrencyPosition {
  Before = 1,
  After = 2,
}

/**
 * The typeface the admin picks. A closed set rather than a family name because
 * every option names *two* faces — a Latin display face carries no Arabic
 * glyphs, and Wanes runs in both scripts.
 */
export enum AppFont {
  /** Plus Jakarta Sans + Cairo — the shipped design. */
  Jakarta = 1,
  /** Inter + IBM Plex Sans Arabic. */
  Inter = 2,
  /** Rubik, both scripts from one family. */
  Rubik = 3,
  /** Noto Sans + Noto Sans Arabic. */
  Noto = 4,
  /** Tajawal, both scripts — Arabic-first. */
  Tajawal = 5,
  /** The browser's own UI face; no webfont is downloaded. */
  System = 6,
}

// ── Enums (kept in sync with the backend) ──
export enum Roles {
  User = 1,
  Admin = 2,
}

export enum DriverStatus {
  None = 0,
  Pending = 1,
  Verified = 2,
  Rejected = 3,
  Suspended = 4,
}

export enum DeviceType {
  Web = 1,
  Ios = 2,
  Android = 3,
}

export enum Gender {
  Unspecified = 0,
  Male = 1,
  Female = 2,
}

export enum ActiveRole {
  Rider = 1,
  Driver = 2,
}

export enum Language {
  En = 1,
  Ar = 2,
}

export enum TripStatus {
  Posted = 1,
  Full = 2,
  Active = 3,
  Completed = 4,
  Cancelled = 5,
  /** Driver is at the pickup point, waiting for the rider to board. */
  Arrived = 6,
}

export enum BookingStatus {
  Pending = 1,
  Confirmed = 2,
  InProgress = 3,
  Completed = 4,
  Cancelled = 5,
  /** Driver is at this rider's pickup, waiting for them to board. */
  Arrived = 6,
  /** Driver waited and the rider never boarded. Terminal, not a cancellation. */
  NoShow = 7,
}

export enum RideRequestStatus {
  Open = 1,
  Matched = 2,
  Expired = 3,
  Cancelled = 4,
}

export enum RatingDirection {
  RiderToDriver = 1,
  DriverToRider = 2,
}

export enum NotificationType {
  RideRequestNearby = 1,
  BookingConfirmed = 2,
  TripCancelled = 3,
  DriverAccepted = 4,
  TripCompleted = 5,
  BookingCancelled = 6,
  TripStarted = 7,
  TripMatched = 8,
  DriverVerified = 9,
  DriverRejected = 10,
  RatingReceived = 11,
  DriverArrived = 12,
  General = 100,
}

/** Who a broadcast reaches (`Wanes.Shareds.Enums.NotificationAudience`). */
export enum NotificationAudience {
  All = 1,
  Riders = 2,
  Drivers = 3,
  VerifiedDrivers = 4,
}

export interface BroadcastInput {
  audience: NotificationAudience;
  type: NotificationType;
  title: string;
  body: string;
  /** Optional Arabic wording; blank means Arabic readers see `title`/`body`. */
  titleAr?: string | null;
  bodyAr?: string | null;
  dataJson?: string | null;
}

export interface BroadcastResult {
  audience: NotificationAudience;
  recipients: number;
}

/** An inbox row as the admin console sees it (`Management.Models.NotificationRow`). */
export interface NotificationRow {
  id: number;
  userId: number;
  userName?: string | null;
  type: NotificationType;
  title: string;
  body: string;
  titleAr?: string | null;
  bodyAr?: string | null;
  dataJson?: string | null;
  isRead: boolean;
  creationDate: string;
}

/** Create/update payload for one user's notification. */
export interface NotificationInput {
  userId: number | null;
  type: NotificationType;
  title: string;
  body: string;
  titleAr?: string | null;
  bodyAr?: string | null;
  dataJson?: string | null;
  isRead: boolean;
}

/** One notification aimed at a hand-picked set of users. */
export interface TargetedSendInput {
  userIds: number[];
  type: NotificationType;
  title: string;
  body: string;
  titleAr?: string | null;
  bodyAr?: string | null;
  dataJson?: string | null;
}

export interface TargetedSendResult {
  recipients: number;
}

/** What a bulk action does to the ticked rows (`NotificationBulkAction`). */
export enum NotificationBulkAction {
  MarkRead = 1,
  MarkUnread = 2,
  Delete = 3,
}

export interface BulkNotificationInput {
  ids: number[];
  action: NotificationBulkAction;
}

export interface BulkNotificationResult {
  action: NotificationBulkAction;
  affected: number;
}

export interface NotificationTypeCount {
  type: NotificationType;
  count: number;
}

/** Headline counts describing the whole notification table, not one page of it. */
export interface NotificationStats {
  total: number;
  unread: number;
  read: number;
  last24Hours: number;
  recipients: number;
  byType: NotificationTypeCount[];
}

export enum SavedPlaceLabel {
  Home = 1,
  Work = 2,
  Custom = 3,
}

export enum FaqCategory {
  General = 1,
  Riding = 2,
  Driving = 3,
  Account = 4,
  Safety = 5,
}

// ── DTOs ──
export interface AuthResult {
  /** Short-lived bearer token; `refreshToken` is what outlives it. */
  token: string;
  expiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  isNewUser: boolean;
  profile: Profile;
}

export interface Profile {
  id: number;
  phone: string;
  firstName: string;
  lastName: string;
  displayName?: string;
  avatarUrl?: string;
  driverStatus: DriverStatus;
  ratingAvg: number;
  /** Roles held by the account — the CMS requires Roles.Admin. */
  roles?: Roles[];
}

export interface DriverRow {
  id: number;
  phone: string;
  name: string;
  driverStatus: DriverStatus;
  licenseNumber?: string;
}

export interface AuditRow {
  id: number;
  actorUserId?: number;
  actorName?: string;
  action: string;
  entityType?: string;
  entityId?: number;
  ip?: string;
  creationDate: string;
}

// ── Analytics (mirrors Areas/Services/Management/Models/Analytics) ──

/** One day of a daily series. The backend zero-fills gaps. */
export interface SeriesPoint {
  date: string;
  value: number;
}

/** One slice of a categorical breakdown. `key` is the enum value. */
export interface MetricPoint {
  key: number;
  label: string;
  value: number;
}

export interface LeaderRow {
  id?: number;
  label: string;
  sublabel?: string;
  value: number;
  secondary?: number;
}

export interface EndpointStat {
  method: string;
  path: string;
  calls: number;
  avgDurationMs: number;
  maxDurationMs: number;
  errors: number;
}

export interface AnalyticsOverview {
  rangeDays: number;
  from: string;
  to: string;

  users: number;
  newUsers: number;
  activeUsers: number;
  riders: number;
  drivers: number;
  verifiedDrivers: number;
  pendingDrivers: number;
  onlineDrivers: number;
  disabledUsers: number;

  vehicles: number;
  vehicleSeats: number;

  trips: number;
  newTrips: number;
  activeTrips: number;
  completedTrips: number;
  cancelledTrips: number;
  seatsOffered: number;
  seatsTaken: number;
  seatFillRate: number;

  bookings: number;
  newBookings: number;
  completedBookings: number;
  cancelledBookings: number;
  bookingCancelRate: number;
  requests: number;
  newRequests: number;
  openRequests: number;
  matchedRequests: number;
  expiredRequests: number;
  matchRate: number;
  avgSeatsPerBooking: number;

  ratings: number;
  avgRating: number;
  lowRatings: number;

  notifications: number;
  unreadNotifications: number;
  savedPlaces: number;
  activeSessions: number;
  newLogins: number;

  apiCalls: number;
  apiErrors: number;
  apiErrorRate: number;
  avgResponseMs: number;
  auditEvents: number;

  usersTrend: number | null;
  tripsTrend: number | null;
  bookingsTrend: number | null;
  requestsTrend: number | null;
}

export interface AnalyticsTimeSeries {
  from: string;
  to: string;
  newUsers: SeriesPoint[];
  newTrips: SeriesPoint[];
  newBookings: SeriesPoint[];
  newRequests: SeriesPoint[];
  completedTrips: SeriesPoint[];
  cancelledTrips: SeriesPoint[];
  logins: SeriesPoint[];
  seatsBooked: SeriesPoint[];
}

export interface AnalyticsBreakdowns {
  tripsByStatus: MetricPoint[];
  bookingsByStatus: MetricPoint[];
  requestsByStatus: MetricPoint[];
  usersByDriverStatus: MetricPoint[];
  usersByLanguage: MetricPoint[];
  usersByGender: MetricPoint[];
  ratingsByStars: MetricPoint[];
  notificationsByType: MetricPoint[];
  sessionsByDevice: MetricPoint[];
  placesByLabel: MetricPoint[];
  demandByHour: MetricPoint[];
  tripsByWeekday: MetricPoint[];
}

export interface AnalyticsOperations {
  totalCalls: number;
  ok2xx: number;
  redirect3xx: number;
  clientError4xx: number;
  serverError5xx: number;
  avgDurationMs: number;
  maxDurationMs: number;
  slowCalls: number;
  unauthenticatedCalls: number;
  callsByDay: SeriesPoint[];
  errorsByDay: SeriesPoint[];
  topEndpoints: EndpointStat[];
  slowestEndpoints: EndpointStat[];
  topAuditActions: LeaderRow[];
  auditByDay: SeriesPoint[];
}

export interface AnalyticsLeaderboards {
  topDrivers: LeaderRow[];
  topRiders: LeaderRow[];
  topRoutes: LeaderRow[];
  topOrigins: LeaderRow[];
  topDestinations: LeaderRow[];
  topRatedDrivers: LeaderRow[];
}
