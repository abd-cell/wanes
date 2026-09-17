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
   * Minutes before departure a driver has to have answered the run-or-cancel
   * question on a trip short of its seat threshold, and minutes before that
   * cutoff they are asked. Riders stood down have to learn in time to find
   * another ride, and how much time that takes is a local question.
   */
  confirmCutoffMinutes: number;
  confirmDecisionLeadMinutes: number;

  /**
   * How many passengers a new trip asks for before it confirms, unless its
   * driver says otherwise.
   *
   * Marketplace policy rather than a per-driver invention: what makes a run
   * worth driving is a fact about the city, and a driver filling in a posting
   * form is the wrong person to decide it from scratch. It only seeds the
   * trip's own threshold — raising it never re-decides a trip that exists.
   */
  minimumPassengersDefault: number;

  /**
   * How long a ride request collects driver offers before one is selected.
   * Zero — the shipped value — means the first interested driver gets it,
   * which is first-come-first-served. Above zero the same data is ranked and
   * the best offer wins, with no other change anywhere.
   */
  driverSelectionWindowMinutes: number;

  /**
   * Average speed behind every duration estimate, in km/h. Drives the earliest
   * departure a rider may post for — one leg-time per seat, because a driver has
   * to gather everybody — as well as the arrival estimate they are shown.
   */
  averageSpeedKmh: number;

  /** Flag-fall and per-km rate behind a derived per-seat price (see cfg_fare). */
  fareBaseAmount: number;
  farePerKm: number;
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

  // ── Shared marketplace ──
  /** How long a request leaving later than the hour collects offers. */
  scheduledSelectionWindowMinutes: number;
  /** Riders may pick one of the collected offers themselves. */
  riderOfferChoice: boolean;
  /** A driver's offer is refused unless they agree the trip is shared. */
  requireSharedTermsAcceptance: boolean;

  // ── Reliability ──
  freeCancelGraceMinutes: number;
  lateCancelLeadMinutes: number;
  reliabilityWarnPoints: number;
  reliabilitySuspendPoints: number;
  reliabilityWindowDays: number;
  suspensionDays: number;

  // ── Safety ──
  /** The driver must type the rider's code to board them. */
  boardingCodeRequired: boolean;
  /** What the SOS button dials. */
  emergencyNumber: string;
  /** Public address trip links are built on — this console's own host. */
  shareBaseUrl?: string;

  // ── Series (whole recurring schedules) ──
  /** Drivers may take, and riders may book, a whole recurring series at once. */
  seriesCommitmentsEnabled: boolean;
  /** Hours a rider has to answer a series offer before the best one is taken. */
  seriesDecisionHours: number;
  /** Notice needed to skip one day of a series for free. */
  seriesSkipNoticeHours: number;
  /** Free skips a driver gets inside the reliability window. */
  seriesFreeSkipsPerWindow: number;
  /** Days of notice for ending a series for free. */
  seriesEndNoticeDays: number;
  /** Day of the week (0 = Sunday) the week-ahead summary goes out. */
  seriesSummaryDay: number;

  updatedAt?: string;
}

/** How a reliability entry was recorded (`ReliabilityEventKind`). */
export enum ReliabilityEventKind {
  FreeCancel = 1,
  Cancel = 2,
  LateCancel = 3,
  RiderLateCancel = 4,
  RiderNoShow = 5,
  /** One day of a series skipped with enough notice — recorded, costs nothing. */
  SeriesSkip = 6,
  /** A series ended without notice — one per day dropped inside it. */
  SeriesEndShortNotice = 7,
}

/** Why a driver cancelled (`CancelReason`). */
export enum CancelReason {
  Personal = 1,
  VehicleProblem = 2,
  SafetyConcern = 3,
  Emergency = 4,
  RouteOrTimeChanged = 5,
  Other = 9,
}

export enum SafetyIncidentKind {
  Sos = 1,
  Report = 2,
}

export enum SafetyIncidentStatus {
  Open = 1,
  Acknowledged = 2,
  Resolved = 3,
}

/** The public page behind a shared trip link (`GET share/{token}`). */
export interface SharedTrip {
  riderFirstName: string;
  originAddress: string;
  originLat: number;
  originLng: number;
  destinationAddress: string;
  destinationLat: number;
  destinationLng: number;
  departAt: string;
  bookingStatus: BookingStatus;
  tripStatus: TripStatus;
  driverFirstName?: string;
  driverRating: number;
  vehicleLabel?: string;
  vehicleColor?: string;
  vehiclePlate?: string;
  driverLat?: number;
  driverLng?: number;
  driverLocationAt?: string;
  updatedAt: string;
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
  /** Almarai, both scripts — Arabic-first geometric. */
  Almarai = 7,
  /** Readex Pro, both scripts. */
  ReadexPro = 8,
  /** Alexandria, both scripts. */
  Alexandria = 9,
  /** Poppins + Almarai. */
  Poppins = 10,
  /** Montserrat + El Messiri. */
  Montserrat = 11,
  /** Amiri, both scripts — the only serif. */
  Amiri = 12,
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

export enum DriverDocumentType {
  LicenseFront = 1,
  LicenseBack = 2,
  IdDocument = 3,
  VehicleRegistration = 4,
  Insurance = 5,
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

/**
 * A trip's LIFECYCLE. Whether it is full and whether it is confirmed are
 * separate dimensions in v2, answered by `isFull` and `isConfirmed` on the
 * trip itself — capacity moves both ways and confirmation only moves one way,
 * so neither ever belonged on this ladder.
 */
export enum TripStatus {
  Posted = 1,
  /** @deprecated Capacity, not a status. Only rows written before v2 carry it. */
  Full = 2,
  Active = 3,
  Completed = 4,
  Cancelled = 5,
  /** Driver is at the pickup point, waiting for the rider to board. */
  Arrived = 6,
  /**
   * Driver has set off for the first pickup — where a trip stops being
   * searchable. Appended rather than slotted into the running order, because
   * the numbers cross all three stacks.
   */
  EnRoute = 7,
  /**
   * @deprecated Retired in v2. A rider's unmet demand is a RideRequest now,
   * with a lifecycle of its own — see RideRequestStatus. Kept only so
   * historical status-log rows still resolve to a label.
   */
  AwaitingDriver = 8,
}

/**
 * Where a rider's demand stands. **Its own lifecycle, not a trip's** — demand
 * that nobody is driving yet is a different kind of thing from transportation
 * that exists, and "open" and "expired" have no trip status that means them.
 */
export enum RideRequestStatus {
  /** Looking for a driver. Joinable by riders, discoverable by drivers. */
  Open = 1,
  /** A driver was selected and a trip was formed — see `matchedTripId`. */
  Matched = 2,
  /** The last participant left, or an admin closed it. */
  Cancelled = 3,
  /** Its departure came and went with nobody driving it. */
  Expired = 4,
}

/**
 * A driver's answer to demand. Interest is a signal, not a trip: it carries a
 * car and a price so the marketplace can rank competing offers without any
 * change to the domain.
 */
export enum DriverInterestStatus {
  Interested = 1,
  Withdrawn = 2,
  Selected = 3,
  Rejected = 4,
  Expired = 5,
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

/**
 * Who a ride is open to, as a condition one side sets on the other
 * (`Wanes.Shareds.Enums.GenderPolicy`).
 *
 * The same three values express both directions — a driver's condition on their
 * riders, and a rider's on their driver and co-riders. Distinct from `Gender`,
 * which is what a person *is*.
 */
export enum GenderPolicy {
  Any = 0,
  MaleOnly = 1,
  FemaleOnly = 2,
}

/** How often a schedule produces a trip (`Wanes.Shareds.Enums.Recurrence`). */
export enum Recurrence {
  Daily = 1,
  Weekly = 2,
  /** On its day of the month, falling back to the last day of shorter months. */
  Monthly = 3,
}

/** Bit flags, ordered to match `DayOfWeek`. Weekly schedules only. */
export enum WeekDays {
  None = 0,
  Sunday = 1,
  Monday = 2,
  Tuesday = 4,
  Wednesday = 8,
  Thursday = 16,
  Friday = 32,
  Saturday = 64,
}

/** Which side of a schedule a commitment is on (`SeriesSide`). */
export enum SeriesSide {
  /** A driver drives a rider's recurring request. */
  DriverServes = 1,
  /** A rider books every day of a driver's recurring trip. */
  RiderJoins = 2,
}

export enum SeriesStatus {
  Proposed = 1,
  Active = 2,
  Declined = 3,
  Withdrawn = 4,
  Ended = 5,
}

export enum RatingDirection {
  RiderToDriver = 1,
  DriverToRider = 2,
}

export enum NotificationType {
  RiderTripNearby = 1,
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
  FeedbackReplied = 13,
  /** A trip reached the seats its driver asked for; every held seat is committed. */
  TripConfirmed = 14,
  /** A trip was called off for want of riders. */
  TripNotEnoughRiders = 15,
  /** The driver has to say whether a trip short of its threshold still runs. */
  ConfirmDecision = 16,
  /** Something moved on a ride request — a rider joined, a driver offered. */
  RideRequest = 17,
  /** A driver's route alert reached their seat count. */
  DemandAlert = 18,
  /** A reliability warning or pause. */
  Reliability = 19,
  /** A safety report for the admin team. */
  SafetyIncident = 20,
  /** A recurring commitment moved — an offer, an acceptance, a skipped day. */
  Series = 21,
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

  /**
   * Cleared — by the owner off their own inbox, or by an admin off this table.
   * Either way the console is the only place the row is still visible, so it is
   * shown here rather than filtered out.
   */
  isDeleted: boolean;
  deletionDate?: string | null;
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
  /** Cleared rows; still counted in `total`. */
  deleted: number;
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

/** Complaint or suggestion — mirrors Shareds/Enums/FeedbackKind.cs. */
export enum FeedbackKind {
  Complaint = 1,
  Suggestion = 2,
}

/** Where a submission stands with the desk — mirrors Shareds/Enums/FeedbackStatus.cs. */
export enum FeedbackStatus {
  New = 1,
  InReview = 2,
  Resolved = 3,
  Dismissed = 4,
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
  /** When they submitted — the queue is worked oldest-first. */
  appliedAt?: string;
  /** Documents on file. A row showing 0 needs no opening. */
  documentCount: number;
  /** The note left on the last decision, when there was one. */
  reviewNote?: string;
  reviewedAt?: string;
}

/**
 * A document a driver uploaded. Carries no URL: the bytes come from an
 * admin-only endpoint that audits every read, so the panel fetches them as a
 * blob rather than pointing an `<img src>` at a public address.
 */
export interface DriverDocument {
  id: number;
  type: DriverDocumentType;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
  isPdf: boolean;
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
  riderTrips: number;
  newRiderTrips: number;
  openRiderTrips: number;
  claimedRiderTrips: number;
  expiredRiderTrips: number;
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
  riderTripsTrend: number | null;
}

export interface AnalyticsTimeSeries {
  from: string;
  to: string;
  newUsers: SeriesPoint[];
  newTrips: SeriesPoint[];
  newBookings: SeriesPoint[];
  newRiderTrips: SeriesPoint[];
  completedTrips: SeriesPoint[];
  cancelledTrips: SeriesPoint[];
  logins: SeriesPoint[];
  seatsBooked: SeriesPoint[];
}

export interface AnalyticsBreakdowns {
  tripsByStatus: MetricPoint[];
  bookingsByStatus: MetricPoint[];
  riderTripsByStatus: MetricPoint[];
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
