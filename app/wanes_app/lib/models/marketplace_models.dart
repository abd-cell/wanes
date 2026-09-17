import 'models.dart';

/// One driver's offer on a request, as its riders compare them
/// (`GET ride-requests/{id}/offers`).
class RideOffer {
  const RideOffer({
    required this.interestId,
    required this.driverId,
    required this.driverName,
    required this.pricePerSeat,
    this.driverRating = 0,
    this.driverRatingCount = 0,
    this.driverTrips = 0,
    this.driverCompletionRate,
    this.driverVerified = false,
    this.vehicleLabel = '',
    this.vehicleColor = '',
    this.vehicleSeats = 0,
    this.seatsOffered,
    this.minPassengers,
    this.message,
    this.offeredAt,
  });

  final int interestId;
  final int driverId;
  final String driverName;
  final double driverRating;
  final int driverRatingCount;
  final int driverTrips;
  final double? driverCompletionRate;
  final bool driverVerified;
  final String vehicleLabel;
  final String vehicleColor;
  final int vehicleSeats;
  final double pricePerSeat;
  final int? seatsOffered;

  /// A conditional offer: the trip runs once this many seats are held.
  final int? minPassengers;
  final String? message;
  final DateTime? offeredAt;

  factory RideOffer.fromJson(Map<String, dynamic> j) => RideOffer(
        interestId: (j['interestId'] as num).toInt(),
        driverId: (j['driverId'] as num?)?.toInt() ?? 0,
        driverName: j['driverName'] as String? ?? '',
        driverRating: (j['driverRating'] as num?)?.toDouble() ?? 0,
        driverRatingCount: (j['driverRatingCount'] as num?)?.toInt() ?? 0,
        driverTrips: (j['driverTrips'] as num?)?.toInt() ?? 0,
        driverCompletionRate: (j['driverCompletionRate'] as num?)?.toDouble(),
        driverVerified: j['driverVerified'] as bool? ?? false,
        vehicleLabel: j['vehicleLabel'] as String? ?? '',
        vehicleColor: j['vehicleColor'] as String? ?? '',
        vehicleSeats: (j['vehicleSeats'] as num?)?.toInt() ?? 0,
        pricePerSeat: (j['pricePerSeat'] as num?)?.toDouble() ?? 0,
        seatsOffered: (j['seatsOffered'] as num?)?.toInt(),
        minPassengers: (j['minPassengers'] as num?)?.toInt(),
        message: j['message'] as String?,
        offeredAt: parseServerDate(j['offeredAt'] as String?),
      );
}

/// A driver's route alert, or a watch on one request (`me/demand-alerts`).
class DemandAlert {
  const DemandAlert({
    required this.id,
    required this.originAddress,
    required this.destinationAddress,
    required this.minSeats,
    this.radiusMeters = 3000,
    this.rideRequestId,
    this.recurringOnly = false,
    this.isActive = true,
    this.lastNotifiedAt,
    this.notifiedCount = 0,
  });

  final int id;
  final String originAddress;
  final String destinationAddress;
  final int radiusMeters;
  final int minSeats;
  final int? rideRequestId;

  /// A route alert that fires for recurring requests only.
  final bool recurringOnly;
  final bool isActive;
  final DateTime? lastNotifiedAt;
  final int notifiedCount;

  bool get isWatch => rideRequestId != null;

  factory DemandAlert.fromJson(Map<String, dynamic> j) => DemandAlert(
        id: (j['id'] as num).toInt(),
        originAddress: j['originAddress'] as String? ?? '',
        destinationAddress: j['destinationAddress'] as String? ?? '',
        radiusMeters: (j['radiusMeters'] as num?)?.toInt() ?? 3000,
        minSeats: (j['minSeats'] as num?)?.toInt() ?? 1,
        rideRequestId: (j['rideRequestId'] as num?)?.toInt(),
        recurringOnly: j['recurringOnly'] as bool? ?? false,
        isActive: j['isActive'] as bool? ?? true,
        lastNotifiedAt: parseServerDate(j['lastNotifiedAt'] as String?),
        notifiedCount: (j['notifiedCount'] as num?)?.toInt() ?? 0,
      );
}

/// Why a driver cancelled (`Shareds.Enums.CancelReason`).
enum CancelReason {
  personal(1, 'cancel.reasonPersonal'),
  vehicleProblem(2, 'cancel.reasonVehicle'),
  safetyConcern(3, 'cancel.reasonSafety'),
  emergency(4, 'cancel.reasonEmergency'),
  routeOrTimeChanged(5, 'cancel.reasonChanged'),
  other(9, 'cancel.reasonOther');

  const CancelReason(this.value, this.labelKey);

  final int value;
  final String labelKey;
}

/// How a cancellation is recorded (`Shareds.Enums.ReliabilityEventKind`).
enum ReliabilityKind {
  freeCancel(1, 'reliability.kindFree'),
  cancel(2, 'reliability.kindCancel'),
  lateCancel(3, 'reliability.kindLate'),
  riderLateCancel(4, 'reliability.kindRiderLate'),
  riderNoShow(5, 'reliability.kindNoShow'),
  seriesSkip(6, 'reliability.kindSeriesSkip'),
  seriesEnd(7, 'reliability.kindSeriesEnd');

  const ReliabilityKind(this.value, this.labelKey);

  final int value;
  final String labelKey;

  static ReliabilityKind fromValue(int? v) =>
      ReliabilityKind.values.firstWhere((k) => k.value == v, orElse: () => ReliabilityKind.freeCancel);
}

/// What cancelling a trip would cost the driver (`GET trips/{id}/cancel-preview`).
class CancelPreview {
  const CancelPreview({
    required this.kind,
    required this.points,
    required this.ridersAffected,
    required this.reasonRequired,
    this.pointsAfter = 0,
    this.warnPoints = 3,
    this.suspendPoints = 5,
    this.windowDays = 30,
    this.wouldSuspend = false,
    this.ridersRequeued = false,
  });

  final ReliabilityKind kind;
  final int points;
  final int ridersAffected;
  final bool reasonRequired;
  final int pointsAfter;
  final int warnPoints;
  final int suspendPoints;
  final int windowDays;
  final bool wouldSuspend;
  final bool ridersRequeued;

  factory CancelPreview.fromJson(Map<String, dynamic> j) => CancelPreview(
        kind: ReliabilityKind.fromValue((j['kind'] as num?)?.toInt()),
        points: (j['points'] as num?)?.toInt() ?? 0,
        ridersAffected: (j['ridersAffected'] as num?)?.toInt() ?? 0,
        reasonRequired: j['reasonRequired'] as bool? ?? false,
        pointsAfter: (j['pointsAfter'] as num?)?.toInt() ?? 0,
        warnPoints: (j['warnPoints'] as num?)?.toInt() ?? 3,
        suspendPoints: (j['suspendPoints'] as num?)?.toInt() ?? 5,
        windowDays: (j['windowDays'] as num?)?.toInt() ?? 30,
        wouldSuspend: j['wouldSuspend'] as bool? ?? false,
        ridersRequeued: j['ridersRequeued'] as bool? ?? false,
      );
}

/// A user's own reliability record (`GET me/reliability`).
class ReliabilityRecord {
  const ReliabilityRecord({
    this.pointsInWindow = 0,
    this.windowDays = 30,
    this.warnPoints = 3,
    this.suspendPoints = 5,
    this.suspendedUntil,
    this.tripsAsDriver = 0,
    this.driverCancellations = 0,
    this.completionRate,
    this.riderLateCancels = 0,
    this.riderNoShows = 0,
    this.recent = const [],
  });

  final int pointsInWindow;
  final int windowDays;
  final int warnPoints;
  final int suspendPoints;
  final DateTime? suspendedUntil;
  final int tripsAsDriver;
  final int driverCancellations;
  final double? completionRate;
  final int riderLateCancels;
  final int riderNoShows;
  final List<ReliabilityEntry> recent;

  bool get isSuspended => suspendedUntil != null && suspendedUntil!.isAfter(DateTime.now());

  factory ReliabilityRecord.fromJson(Map<String, dynamic> j) => ReliabilityRecord(
        pointsInWindow: (j['pointsInWindow'] as num?)?.toInt() ?? 0,
        windowDays: (j['windowDays'] as num?)?.toInt() ?? 30,
        warnPoints: (j['warnPoints'] as num?)?.toInt() ?? 3,
        suspendPoints: (j['suspendPoints'] as num?)?.toInt() ?? 5,
        suspendedUntil: parseServerDate(j['suspendedUntil'] as String?),
        tripsAsDriver: (j['tripsAsDriver'] as num?)?.toInt() ?? 0,
        driverCancellations: (j['driverCancellations'] as num?)?.toInt() ?? 0,
        completionRate: (j['completionRate'] as num?)?.toDouble(),
        riderLateCancels: (j['riderLateCancels'] as num?)?.toInt() ?? 0,
        riderNoShows: (j['riderNoShows'] as num?)?.toInt() ?? 0,
        recent: ((j['recent'] as List?) ?? const [])
            .map((e) => ReliabilityEntry.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class ReliabilityEntry {
  const ReliabilityEntry({
    required this.kind,
    required this.points,
    this.tripId,
    this.isWaived = false,
    this.needsReview = false,
    this.createdAt,
  });

  final ReliabilityKind kind;
  final int points;
  final int? tripId;
  final bool isWaived;
  final bool needsReview;
  final DateTime? createdAt;

  factory ReliabilityEntry.fromJson(Map<String, dynamic> j) => ReliabilityEntry(
        kind: ReliabilityKind.fromValue((j['kind'] as num?)?.toInt()),
        points: (j['points'] as num?)?.toInt() ?? 0,
        tripId: (j['tripId'] as num?)?.toInt(),
        isWaived: j['isWaived'] as bool? ?? false,
        needsReview: j['needsReview'] as bool? ?? false,
        createdAt: parseServerDate(j['createdAt'] as String?),
      );
}

/// The server's answer to an SOS or report.
class SafetyIncidentResult {
  const SafetyIncidentResult({
    required this.id,
    this.emergencyNumber = '911',
    this.emergencyContactNotified = false,
  });

  final int id;
  final String emergencyNumber;
  final bool emergencyContactNotified;

  factory SafetyIncidentResult.fromJson(Map<String, dynamic> j) => SafetyIncidentResult(
        id: (j['id'] as num).toInt(),
        emergencyNumber: (j['emergencyNumber'] as String?)?.trim().isNotEmpty == true
            ? (j['emergencyNumber'] as String).trim()
            : '911',
        emergencyContactNotified: j['emergencyContactNotified'] as bool? ?? false,
      );
}

/// A "follow my trip" link.
class ShareLink {
  const ShareLink({required this.token, this.url});

  final String token;

  /// The full link when the admin set a public address; null otherwise.
  final String? url;

  factory ShareLink.fromJson(Map<String, dynamic> j) =>
      ShareLink(token: j['token'] as String? ?? '', url: j['url'] as String?);
}

/// Versioned terms the user agrees to (`Shareds.Enums.AcknowledgementKind`).
enum AcknowledgementKindWire {
  riderSafety(1),
  driverSafety(2),
  riderSharedRide(3),
  driverSharedTrip(4);

  const AcknowledgementKindWire(this.value);
  final int value;

  static AcknowledgementKindWire? fromValue(int? v) {
    for (final k in values) {
      if (k.value == v) return k;
    }
    return null;
  }
}
