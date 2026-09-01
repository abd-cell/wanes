import {
  ActiveRole, BookingStatus, DeviceType, DriverStatus, FaqCategory, Gender, Language,
  NotificationAudience, NotificationType, RatingDirection, RideRequestStatus, Roles,
  SavedPlaceLabel, TripStatus,
} from '../../core/api/models';

export type FieldType = 'text' | 'number' | 'checkbox' | 'select' | 'datetime' | 'textarea' | 'reference';
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
export const REQUEST_STATUS = opt(RideRequestStatus, 'requeststatus');
export const RATING_DIR = opt(RatingDirection, 'ratingdir');
export const NOTIF_TYPE = opt(NotificationType, 'notiftype');
export const NOTIF_AUDIENCE = opt(NotificationAudience, 'audience');
export const PLACE_LABEL = opt(SavedPlaceLabel, 'placelabel');
export const DEVICE_TYPE = opt(DeviceType, 'devicetype');
export const ROLE_OPTIONS = opt(Roles, 'role');
export const FAQ_CATEGORY = opt(FaqCategory, 'faqcategory');

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
      { field: 'status', labelKey: 'col_status', type: 'enum', enum: TRIP_STATUS },
    ],
    filters: [{ name: 'status', labelKey: 'col_status', enum: TRIP_STATUS }],
    fields: [
      { name: 'driverId', labelKey: 'col_driver', type: 'reference', lookup: 'drivers', required: true },
      { name: 'vehicleId', labelKey: 'col_vehicle', type: 'reference', lookup: 'vehicles', required: true },
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
      { name: 'status', labelKey: 'col_status', type: 'select', enum: TRIP_STATUS },
    ],
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
      { name: 'rideRequestId', labelKey: 'col_request', type: 'reference', lookup: 'requests' },
    ],
  },
  {
    key: 'requests', route: 'requests', titleKey: 'res_requests',
    searchable: true, canCreate: true, canEdit: true, canDelete: true,
    columns: [
      { field: 'id', labelKey: 'col_id' },
      { field: 'riderName', labelKey: 'col_rider' },
      { field: 'originAddress', labelKey: 'col_origin' },
      { field: 'destinationAddress', labelKey: 'col_destination' },
      { field: 'seats', labelKey: 'col_seats' },
      { field: 'wantedDepartAt', labelKey: 'col_depart', type: 'datetime' },
      { field: 'matchedTripSummary', labelKey: 'col_matched_trip' },
      { field: 'status', labelKey: 'col_status', type: 'enum', enum: REQUEST_STATUS },
    ],
    filters: [{ name: 'status', labelKey: 'col_status', enum: REQUEST_STATUS }],
    fields: [
      { name: 'riderId', labelKey: 'col_rider', type: 'reference', lookup: 'riders', required: true },
      { name: 'originAddress', labelKey: 'col_origin', type: 'text' },
      { name: 'originLat', labelKey: 'col_origin_lat', type: 'number', step: 'any' },
      { name: 'originLng', labelKey: 'col_origin_lng', type: 'number', step: 'any' },
      { name: 'destinationAddress', labelKey: 'col_destination', type: 'text' },
      { name: 'destLat', labelKey: 'col_dest_lat', type: 'number', step: 'any' },
      { name: 'destLng', labelKey: 'col_dest_lng', type: 'number', step: 'any' },
      { name: 'seats', labelKey: 'col_seats', type: 'number' },
      { name: 'wantedDepartAt', labelKey: 'col_depart', type: 'datetime' },
      { name: 'radiusMeters', labelKey: 'col_radius', type: 'number' },
      { name: 'status', labelKey: 'col_status', type: 'select', enum: REQUEST_STATUS },
      { name: 'matchedTripId', labelKey: 'col_matched_trip', type: 'reference', lookup: 'trips' },
      { name: 'expiresAt', labelKey: 'col_expires', type: 'datetime' },
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
