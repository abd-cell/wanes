import {
  ActiveRole, BookingStatus, CancelReason, DeviceType, DriverStatus, FaqCategory, FeedbackKind,
  FeedbackStatus, Gender, GenderPolicy, Language, NotificationAudience, NotificationType,
  RatingDirection, Recurrence, ReliabilityEventKind, RideRequestStatus, Roles, SafetyIncidentKind,
  SafetyIncidentStatus, SavedPlaceLabel, SeriesSide, SeriesStatus, TripStatus,
} from '../../core/api/models';

export type FieldType =
  | 'text' | 'number' | 'checkbox' | 'select' | 'datetime' | 'textarea' | 'reference'
  /**
   * Shown in the form but never sent back. For records the admin has to *read*
   * to act on — a complaint's own words — where an editable box would invite
   * rewriting someone else's text, and a table cell is too small to read it in.
   */
  | 'readonly';
export type ColumnType = 'text' | 'datetime' | 'enum' | 'bool' | 'currency';

export interface EnumOption { value: number; labelKey: string; }

export interface ResourceColumn {
  field: string;
  labelKey: string;
  type?: ColumnType;
  enum?: EnumOption[];
}

export interface ResourceField {
  name: string;
  labelKey: string;
  type: FieldType;
  enum?: EnumOption[];
  required?: boolean;
  /** step for number inputs, e.g. 'any' for coordinates / decimals. */
  step?: string;
  /**
   * For `reference` fields: the backend lookup type the picker searches
   * (users, drivers, riders, vehicles, trips, bookings, requests). The form still
   * stores the foreign key — the admin just never has to know it.
   */
  lookup?: string;
}

export interface ResourceFilter {
  name: string;
  labelKey: string;
  enum: EnumOption[];
}

export interface ResourceConfig {
  key: string;      // nav + route param in the CMS
  route: string;    // api path segment under /admin/
  titleKey: string;
  columns: ResourceColumn[];
  fields: ResourceField[];
  filters?: ResourceFilter[];
  searchable?: boolean;
  canCreate: boolean;
  canEdit: boolean;
  canDelete: boolean;
  /** enables the role-management panel in the edit form. */
  manageRoles?: boolean;
}

// ── enum option sets (labelKey → i18n) ──
const opt = (e: Record<string, string | number>, prefix: string): EnumOption[] =>
  Object.keys(e)
    .filter((k) => typeof e[k] === 'number')
    .map((k) => ({ value: e[k] as number, labelKey: `${prefix}_${(e[k] as number)}` }));

export const GENDER = opt(Gender, 'gender');
export const ACTIVE_ROLE = opt(ActiveRole, 'activerole');
export const DRIVER_STATUS = opt(DriverStatus, 'driverstatus');
export const LANGUAGE = opt(Language, 'language');
export const TRIP_STATUS = opt(TripStatus, 'tripstatus');
export const BOOKING_STATUS = opt(BookingStatus, 'bookingstatus');
export const GENDER_POLICY = opt(GenderPolicy, 'genderpolicy');
export const RECURRENCE = opt(Recurrence, 'recurrence');
export const RATING_DIR = opt(RatingDirection, 'ratingdir');
export const NOTIF_TYPE = opt(NotificationType, 'notiftype');
export const NOTIF_AUDIENCE = opt(NotificationAudience, 'audience');
export const PLACE_LABEL = opt(SavedPlaceLabel, 'placelabel');
export const DEVICE_TYPE = opt(DeviceType, 'devicetype');
export const ROLE_OPTIONS = opt(Roles, 'role');
export const FAQ_CATEGORY = opt(FaqCategory, 'faqcategory');
export const FEEDBACK_KIND = opt(FeedbackKind, 'feedbackkind');
export const FEEDBACK_STATUS = opt(FeedbackStatus, 'feedbackstatus');
export const REQUEST_STATUS = opt(RideRequestStatus, 'requeststatus');
export const RELIABILITY_KIND = opt(ReliabilityEventKind, 'reliabilitykind');
export const CANCEL_REASON = opt(CancelReason, 'cancelreason');
export const SAFETY_KIND = opt(SafetyIncidentKind, 'safetykind');
export const SAFETY_STATUS = opt(SafetyIncidentStatus, 'safetystatus');
export const SERIES_SIDE = opt(SeriesSide, 'seriesside');
export const SERIES_STATUS = opt(SeriesStatus, 'seriesstatus');
/** The reliability grid's review filter — the API takes 1 for "waiting on review". */
export const NEEDS_REVIEW: EnumOption[] = [{ value: 1, labelKey: 'filter_needs_review' }];

export const RESOURCES: ResourceConfig[] = [
  {
    key: 'users', route: 'users', titleKey: 'res_users',
    searchable: true, canCreate: true, canEdit: true, canDelete: true, manageRoles: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'phone', labelKey: 'col_phone' },
      { field: 'firstName', labelKey: 'col_first_name' },
      { field: 'lastName', labelKey: 'col_last_name' },
      { field: 'driverStatus', labelKey: 'col_driver_status', type: 'enum', enum: DRIVER_STATUS },
      { field: 'isDisabled', labelKey: 'col_disabled', type: 'bool' },
      { field: 'creationDate', labelKey: 'col_created', type: 'datetime' },
    ],
    filters: [
      { name: 'driverStatus', labelKey: 'col_driver_status', enum: DRIVER_STATUS },
    ],
    fields: [
      { name: 'phone', labelKey: 'col_phone', type: 'text', required: true },
      { name: 'email', labelKey: 'col_email', type: 'text' },
      { name: 'firstName', labelKey: 'col_first_name', type: 'text' },
      { name: 'lastName', labelKey: 'col_last_name', type: 'text' },
      { name: 'displayName', labelKey: 'col_display_name', type: 'text' },
      { name: 'gender', labelKey: 'col_gender', type: 'select', enum: GENDER },
      { name: 'isRider', labelKey: 'col_is_rider', type: 'checkbox' },
      { name: 'isDriver', labelKey: 'col_is_driver', type: 'checkbox' },
      { name: 'driverStatus', labelKey: 'col_driver_status', type: 'select', enum: DRIVER_STATUS },
      { name: 'licenseNumber', labelKey: 'col_license', type: 'text' },
      { name: 'language', labelKey: 'col_language', type: 'select', enum: LANGUAGE },
      { name: 'isDisabled', labelKey: 'col_disabled', type: 'checkbox' },
    ],
  },
  {
    key: 'vehicles', route: 'vehicles', titleKey: 'res_vehicles',
    searchable: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'ownerName', labelKey: 'col_owner' },
      { field: 'make', labelKey: 'col_make' },
      { field: 'model', labelKey: 'col_model' },
      { field: 'plate', labelKey: 'col_plate' },
      { field: 'seatCapacity', labelKey: 'col_seats' },
    ],
    fields: [
      { name: 'userId', labelKey: 'col_owner', type: 'reference', lookup: 'drivers', required: true },
      { name: 'make', labelKey: 'col_make', type: 'text', required: true },
      { name: 'model', labelKey: 'col_model', type: 'text', required: true },
      { name: 'plate', labelKey: 'col_plate', type: 'text', required: true },
      { name: 'color', labelKey: 'col_color', type: 'text' },
      { name: 'year', labelKey: 'col_year', type: 'number' },
      { name: 'seatCapacity', labelKey: 'col_seats', type: 'number', required: true },
      { name: 'photoUrl', labelKey: 'col_photo', type: 'text' },
      { name: 'isDefault', labelKey: 'col_default', type: 'checkbox' },
    ],
  },
  {
    // Every trip, however it began: one a driver published, and one formed
    // from a matched ride request. After formation the two are the same thing
    // — see BUSINESS_LOGIC §21.1 rule 20 — so there is one grid, and nothing
    // here can ask which way a trip came about. Demand itself is not in this
    // grid at all: it is a different object now, with its own lifecycle.
    key: 'trips', route: 'trips', titleKey: 'res_trips',
    searchable: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'driverName', labelKey: 'col_driver' },
      { field: 'vehicleLabel', labelKey: 'col_vehicle' },
      { field: 'originAddress', labelKey: 'col_origin' },
      { field: 'destinationAddress', labelKey: 'col_destination' },
      { field: 'departAt', labelKey: 'col_depart', type: 'datetime' },
      { field: 'seatsLeft', labelKey: 'col_seats_left' },
      { field: 'pricePerSeat', labelKey: 'col_price', type: 'currency' },
      { field: 'minSeatsToConfirm', labelKey: 'col_min_seats' },
      { field: 'genderPolicy', labelKey: 'col_gender_policy', type: 'enum', enum: GENDER_POLICY },
      { field: 'status', labelKey: 'col_status', type: 'enum', enum: TRIP_STATUS },
    ],
    filters: [{ name: 'status', labelKey: 'col_status', enum: TRIP_STATUS }],
    fields: [
      // Not required: a trip still waiting for a driver has neither.
      { name: 'driverId', labelKey: 'col_driver', type: 'reference', lookup: 'drivers' },
      { name: 'vehicleId', labelKey: 'col_vehicle', type: 'reference', lookup: 'vehicles' },
      { name: 'originAddress', labelKey: 'col_origin', type: 'text' },
      { name: 'originLat', labelKey: 'col_origin_lat', type: 'number', step: 'any' },
      { name: 'originLng', labelKey: 'col_origin_lng', type: 'number', step: 'any' },
      { name: 'destinationAddress', labelKey: 'col_destination', type: 'text' },
      { name: 'destLat', labelKey: 'col_dest_lat', type: 'number', step: 'any' },
      { name: 'destLng', labelKey: 'col_dest_lng', type: 'number', step: 'any' },
      { name: 'departAt', labelKey: 'col_depart', type: 'datetime' },
      { name: 'seatsTotal', labelKey: 'col_seats_total', type: 'number' },
      { name: 'seatsLeft', labelKey: 'col_seats_left', type: 'number' },
      { name: 'pricePerSeat', labelKey: 'col_price', type: 'number', step: 'any' },
      { name: 'minSeatsToConfirm', labelKey: 'col_min_seats', type: 'number' },
      { name: 'genderPolicy', labelKey: 'col_gender_policy', type: 'select', enum: GENDER_POLICY },
      { name: 'minAge', labelKey: 'col_min_age', type: 'number' },
      { name: 'maxAge', labelKey: 'col_max_age', type: 'number' },
      { name: 'status', labelKey: 'col_status', type: 'select', enum: TRIP_STATUS },
    ],
  },
  {
    // Demand: journeys riders asked for that nobody is driving yet, and what
    // became of them. Read-only but for closing one — "delete" here closes the
    // request, and the row stays as the record.
    key: 'ride-requests', route: 'ride-requests', titleKey: 'res_ride_requests',
    searchable: true, canCreate: false, canEdit: false, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'authorName', labelKey: 'col_rider' },
      { field: 'originAddress', labelKey: 'col_origin' },
      { field: 'destinationAddress', labelKey: 'col_destination' },
      { field: 'departAt', labelKey: 'col_depart', type: 'datetime' },
      { field: 'seatsRequested', labelKey: 'col_seats' },
      { field: 'riderCount', labelKey: 'col_riders' },
      { field: 'interestCount', labelKey: 'col_offers' },
      { field: 'decideAt', labelKey: 'col_decide_at', type: 'datetime' },
      { field: 'status', labelKey: 'col_status', type: 'enum', enum: REQUEST_STATUS },
      { field: 'matchedTripId', labelKey: 'col_matched_trip' },
      { field: 'reopenedFromRequestId', labelKey: 'col_reopened_from' },
    ],
    filters: [{ name: 'status', labelKey: 'col_status', enum: REQUEST_STATUS }],
    fields: [],
  },
  {
    key: 'bookings', route: 'bookings', titleKey: 'res_bookings',
    canCreate: true, canEdit: true, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'tripSummary', labelKey: 'col_trip' },
      { field: 'riderName', labelKey: 'col_rider' },
      { field: 'seats', labelKey: 'col_seats' },
      { field: 'status', labelKey: 'col_status', type: 'enum', enum: BOOKING_STATUS },
      { field: 'creationDate', labelKey: 'col_created', type: 'datetime' },
    ],
    filters: [{ name: 'status', labelKey: 'col_status', enum: BOOKING_STATUS }],
    fields: [
      { name: 'tripId', labelKey: 'col_trip', type: 'reference', lookup: 'trips', required: true },
      { name: 'riderId', labelKey: 'col_rider', type: 'reference', lookup: 'riders', required: true },
      { name: 'seats', labelKey: 'col_seats', type: 'number', required: true },
      { name: 'status', labelKey: 'col_status', type: 'select', enum: BOOKING_STATUS },
    ],
  },
  {
    // Recurring postings. Read mostly: what a desk needs is to see the pattern
    // behind fourteen near-identical rows and to stop a runaway series, so the
    // only writable fields are the pause, the hour and the end date. There is
    // no create — a schedule is somebody's own commute.
    key: 'schedules', route: 'schedules', titleKey: 'res_schedules',
    searchable: true, canCreate: false, canEdit: true, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'ownerName', labelKey: 'col_owner' },
      { field: 'ownerRole', labelKey: 'col_role', type: 'enum', enum: ACTIVE_ROLE },
      { field: 'originAddress', labelKey: 'col_origin' },
      { field: 'destinationAddress', labelKey: 'col_destination' },
      { field: 'recurrence', labelKey: 'col_recurrence', type: 'enum', enum: RECURRENCE },
      { field: 'timeOfDay', labelKey: 'col_time' },
      { field: 'seats', labelKey: 'col_seats' },
      { field: 'isPaused', labelKey: 'col_paused', type: 'bool' },
      { field: 'materialisedThrough', labelKey: 'col_generated_through', type: 'datetime' },
    ],
    filters: [{ name: 'ownerRole', labelKey: 'col_role', enum: ACTIVE_ROLE }],
    fields: [
      { name: 'ownerName', labelKey: 'col_owner', type: 'readonly' },
      { name: 'originAddress', labelKey: 'col_origin', type: 'readonly' },
      { name: 'destinationAddress', labelKey: 'col_destination', type: 'readonly' },
      { name: 'isPaused', labelKey: 'col_paused', type: 'checkbox' },
      { name: 'timeOfDay', labelKey: 'col_time', type: 'text' },
      { name: 'endDate', labelKey: 'col_end_date', type: 'datetime' },
    ],
  },
  {
    key: 'ratings', route: 'ratings', titleKey: 'res_ratings',
    canCreate: true, canEdit: true, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'bookingSummary', labelKey: 'col_booking' },
      { field: 'fromName', labelKey: 'col_from' },
      { field: 'toName', labelKey: 'col_to' },
      { field: 'direction', labelKey: 'col_direction', type: 'enum', enum: RATING_DIR },
      { field: 'stars', labelKey: 'col_stars' },
    ],
    filters: [{ name: 'direction', labelKey: 'col_direction', enum: RATING_DIR }],
    fields: [
      { name: 'bookingId', labelKey: 'col_booking', type: 'reference', lookup: 'bookings', required: true },
      { name: 'fromUserId', labelKey: 'col_from', type: 'reference', lookup: 'users', required: true },
      { name: 'toUserId', labelKey: 'col_to', type: 'reference', lookup: 'users', required: true },
      { name: 'direction', labelKey: 'col_direction', type: 'select', enum: RATING_DIR },
      { name: 'stars', labelKey: 'col_stars', type: 'number', required: true },
      { name: 'comment', labelKey: 'col_comment', type: 'textarea' },
    ],
  },
  {
    key: 'places', route: 'places', titleKey: 'res_places',
    searchable: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'ownerName', labelKey: 'col_owner' },
      { field: 'label', labelKey: 'col_label', type: 'enum', enum: PLACE_LABEL },
      { field: 'name', labelKey: 'col_name' },
      { field: 'address', labelKey: 'col_address' },
    ],
    filters: [{ name: 'label', labelKey: 'col_label', enum: PLACE_LABEL }],
    fields: [
      { name: 'userId', labelKey: 'col_owner', type: 'reference', lookup: 'users', required: true },
      { name: 'label', labelKey: 'col_label', type: 'select', enum: PLACE_LABEL },
      { name: 'name', labelKey: 'col_name', type: 'text', required: true },
      { name: 'address', labelKey: 'col_address', type: 'text' },
      { name: 'lat', labelKey: 'col_lat', type: 'number', step: 'any' },
      { name: 'lng', labelKey: 'col_lng', type: 'number', step: 'any' },
    ],
  },
  {
    key: 'sessions', route: 'sessions', titleKey: 'res_sessions',
    canCreate: false, canEdit: false, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'ownerName', labelKey: 'col_owner' },
      { field: 'deviceType', labelKey: 'col_device', type: 'enum', enum: DEVICE_TYPE },
      { field: 'lastActivityAt', labelKey: 'col_last_activity', type: 'datetime' },
      { field: 'creationDate', labelKey: 'col_created', type: 'datetime' },
    ],
    filters: [{ name: 'deviceType', labelKey: 'col_device', enum: DEVICE_TYPE }],
    fields: [],
  },
  {
    // Help-centre content. Both languages are edited side by side so a
    // half-translated entry is obvious before it is published.
    key: 'faqs', route: 'faqs', titleKey: 'res_faqs',
    searchable: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'category', labelKey: 'col_category', type: 'enum', enum: FAQ_CATEGORY },
      { field: 'questionEn', labelKey: 'col_question_en' },
      { field: 'questionAr', labelKey: 'col_question_ar' },
      { field: 'sortOrder', labelKey: 'col_sort_order' },
      { field: 'isPublished', labelKey: 'col_published', type: 'bool' },
    ],
    filters: [{ name: 'category', labelKey: 'col_category', enum: FAQ_CATEGORY }],
    fields: [
      { name: 'category', labelKey: 'col_category', type: 'select', enum: FAQ_CATEGORY, required: true },
      { name: 'questionEn', labelKey: 'col_question_en', type: 'text', required: true },
      { name: 'questionAr', labelKey: 'col_question_ar', type: 'text', required: true },
      { name: 'answerEn', labelKey: 'col_answer_en', type: 'textarea', required: true },
      { name: 'answerAr', labelKey: 'col_answer_ar', type: 'textarea', required: true },
      { name: 'sortOrder', labelKey: 'col_sort_order', type: 'number' },
      { name: 'isPublished', labelKey: 'col_published', type: 'checkbox' },
    ],
  },
  {
    // The support desk's inbox. Read-mostly: the form exposes the submission as
    // context and only the two columns the desk owns — status and the reply —
    // are editable. No create; a complaint belongs to whoever wrote it.
    key: 'feedback', route: 'feedback', titleKey: 'res_feedback',
    searchable: true, canCreate: false, canEdit: true, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'kind', labelKey: 'col_feedback_kind', type: 'enum', enum: FEEDBACK_KIND },
      { field: 'status', labelKey: 'col_status', type: 'enum', enum: FEEDBACK_STATUS },
      { field: 'userName', labelKey: 'col_user' },
      { field: 'subject', labelKey: 'col_subject' },
      { field: 'language', labelKey: 'col_language', type: 'enum', enum: LANGUAGE },
      { field: 'repliedByName', labelKey: 'col_replied_by' },
      { field: 'creationDate', labelKey: 'col_created', type: 'datetime' },
    ],
    filters: [
      { name: 'status', labelKey: 'col_status', enum: FEEDBACK_STATUS },
      { name: 'kind', labelKey: 'col_feedback_kind', enum: FEEDBACK_KIND },
    ],
    fields: [
      { name: 'subject', labelKey: 'col_subject', type: 'readonly' },
      { name: 'message', labelKey: 'col_message', type: 'readonly' },
      { name: 'status', labelKey: 'col_status', type: 'select', enum: FEEDBACK_STATUS, required: true },
      { name: 'reply', labelKey: 'col_reply', type: 'textarea' },
    ],
  },
  {
    // The safety queue. Open SOS calls come first. The team acknowledges and
    // resolves them here, with a note; the report itself is read-only.
    key: 'safety-incidents', route: 'safety-incidents', titleKey: 'res_safety',
    searchable: true, canCreate: false, canEdit: true, canDelete: false,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'kind', labelKey: 'col_kind', type: 'enum', enum: SAFETY_KIND },
      { field: 'status', labelKey: 'col_status', type: 'enum', enum: SAFETY_STATUS },
      { field: 'reporterName', labelKey: 'col_user' },
      { field: 'reporterPhone', labelKey: 'col_phone' },
      { field: 'tripId', labelKey: 'col_trip' },
      { field: 'emergencyContactNotified', labelKey: 'col_contact_notified', type: 'bool' },
      { field: 'createdAt', labelKey: 'col_created', type: 'datetime' },
    ],
    filters: [{ name: 'status', labelKey: 'col_status', enum: SAFETY_STATUS }],
    fields: [
      { name: 'reporterName', labelKey: 'col_user', type: 'readonly' },
      { name: 'reporterPhone', labelKey: 'col_phone', type: 'readonly' },
      { name: 'note', labelKey: 'col_message', type: 'readonly' },
      { name: 'lat', labelKey: 'col_lat', type: 'readonly' },
      { name: 'lng', labelKey: 'col_lng', type: 'readonly' },
      { name: 'status', labelKey: 'col_status', type: 'select', enum: SAFETY_STATUS, required: true },
      { name: 'adminNote', labelKey: 'col_admin_note', type: 'textarea' },
    ],
  },
  {
    // Cancellations and no-shows. Entries flagged for review — a breakdown, a
    // safety call — are waived by saving the entry with a note; the record of
    // what happened is never deleted.
    key: 'reliability', route: 'reliability', titleKey: 'res_reliability',
    searchable: true, canCreate: false, canEdit: true, canDelete: false,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'userName', labelKey: 'col_user' },
      { field: 'role', labelKey: 'col_role', type: 'enum', enum: ACTIVE_ROLE },
      { field: 'kind', labelKey: 'col_kind', type: 'enum', enum: RELIABILITY_KIND },
      { field: 'points', labelKey: 'col_points' },
      { field: 'reason', labelKey: 'col_reason', type: 'enum', enum: CANCEL_REASON },
      { field: 'ridersAffected', labelKey: 'col_riders' },
      { field: 'minutesBeforeDeparture', labelKey: 'col_minutes_before' },
      { field: 'needsReview', labelKey: 'col_needs_review', type: 'bool' },
      { field: 'isWaived', labelKey: 'col_waived', type: 'bool' },
      { field: 'createdAt', labelKey: 'col_created', type: 'datetime' },
    ],
    filters: [{ name: 'needsReview', labelKey: 'col_needs_review', enum: NEEDS_REVIEW }],
    fields: [
      { name: 'userName', labelKey: 'col_user', type: 'readonly' },
      { name: 'note', labelKey: 'col_message', type: 'readonly' },
      { name: 'waiveNote', labelKey: 'col_waive_note', type: 'textarea', required: true },
    ],
  },
  {
    // Whole-series commitments: a driver driving a rider's commute, a rider
    // holding a seat every day. Support reads them, and can end one — which
    // drops its upcoming days at nobody's cost.
    key: 'series', route: 'series', titleKey: 'res_series',
    searchable: true, canCreate: false, canEdit: false, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'side', labelKey: 'col_side', type: 'enum', enum: SERIES_SIDE },
      { field: 'status', labelKey: 'col_status', type: 'enum', enum: SERIES_STATUS },
      { field: 'driverName', labelKey: 'col_driver' },
      { field: 'riderName', labelKey: 'col_rider' },
      { field: 'originAddress', labelKey: 'col_origin' },
      { field: 'destinationAddress', labelKey: 'col_destination' },
      { field: 'recurrence', labelKey: 'col_recurrence', type: 'enum', enum: RECURRENCE },
      { field: 'timeOfDay', labelKey: 'col_time' },
      { field: 'pricePerSeat', labelKey: 'col_price' },
      { field: 'seats', labelKey: 'col_seats' },
      { field: 'upcomingCount', labelKey: 'col_upcoming_days' },
      { field: 'nextDeparture', labelKey: 'col_next_departure', type: 'datetime' },
      { field: 'until', labelKey: 'col_until', type: 'datetime' },
      { field: 'createdAt', labelKey: 'col_created', type: 'datetime' },
    ],
    filters: [
      { name: 'status', labelKey: 'col_status', enum: SERIES_STATUS },
      { name: 'side', labelKey: 'col_side', enum: SERIES_SIDE },
    ],
    fields: [],
  },
  {
    // Drivers' route alerts and request watches — read-only, for support.
    key: 'demand-alerts', route: 'demand-alerts', titleKey: 'res_alerts',
    searchable: true, canCreate: false, canEdit: false, canDelete: false,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'driverName', labelKey: 'col_driver' },
      { field: 'originAddress', labelKey: 'col_origin' },
      { field: 'destinationAddress', labelKey: 'col_destination' },
      { field: 'minSeats', labelKey: 'col_min_seats' },
      { field: 'radiusMeters', labelKey: 'col_radius' },
      { field: 'rideRequestId', labelKey: 'col_watched_request' },
      { field: 'isActive', labelKey: 'col_active', type: 'bool' },
      { field: 'notifiedCount', labelKey: 'col_notified' },
      { field: 'lastNotifiedAt', labelKey: 'col_last_notified', type: 'datetime' },
    ],
    fields: [],
  },
  {
    key: 'api-logs', route: 'api-logs', titleKey: 'res_api_logs',
    searchable: true, canCreate: false, canEdit: false, canDelete: false,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'method', labelKey: 'col_method' },
      { field: 'path', labelKey: 'col_path' },
      { field: 'statusCode', labelKey: 'col_status_code' },
      { field: 'durationMs', labelKey: 'col_duration' },
      { field: 'actorName', labelKey: 'col_actor' },
      { field: 'creationDate', labelKey: 'col_created', type: 'datetime' },
    ],
    fields: [],
  },
];

export const resourceByKey = (key: string): ResourceConfig | undefined =>
  RESOURCES.find((r) => r.key === key);
