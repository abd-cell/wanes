import 'dart:async';

import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/notification_router.dart';
import '../core/push_service.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import '../widgets/wanes_motion.dart';

/// Rider notifications feed. Mirrors prototype screen 13 (Notifications):
/// a Today / Earlier grouped list with unread dots and "Mark all read".
///
/// Backed by `GET /Notifications/mine`. A push or SSE event that lands while
/// this screen is open refreshes it in place, so the inbox matches the tray.
class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  final _service = NotificationsService();

  List<AppNotification> _items = const [];
  bool _loading = true;
  String? _error;
  StreamSubscription<AppNotification>? _incoming;

  @override
  void initState() {
    super.initState();
    _load();
    // A notification arriving while the inbox is on screen should show up
    // without the user pulling to refresh.
    _incoming = PushService.instance.received.listen((_) => _load(silent: true));
  }

  @override
  void dispose() {
    _incoming?.cancel();
    super.dispose();
  }

  /// [silent] keeps the list on screen while re-fetching (push-triggered
  /// refresh), instead of flashing the spinner.
  Future<void> _load({bool silent = false}) async {
    if (!silent) setState(() => _loading = true);

    final response = await _service.feed();
    if (!mounted) return;

    setState(() {
      _loading = false;
      if (response.success && response.data != null) {
        _items = response.data!.items;
        _error = null;
        PushService.instance.unreadCount.value = response.data!.unreadCount;
      } else if (!silent) {
        _error = response.errorMessage ?? context.tr('notif.loadFailed');
      }
    });
  }

  bool get _hasUnread => _items.any((n) => !n.isRead);

  Future<void> _markAllRead() async {
    // Optimistic: the list is already correct locally, and a failed call is
    // reconciled by the next load.
    setState(() => _items = _items.map((n) => n.copyWith(isRead: true)).toList());
    PushService.instance.unreadCount.value = 0;

    final response = await _service.markAllRead();
    if (!mounted || response.success) return;
    WanesAlerts.error(context, response.errorMessage ?? context.tr('errors.generic'));
    await _load(silent: true);
  }

  /// A row is a link to whatever it is about, not just a read receipt — the
  /// same destination a tap on the tray notification opens.
  Future<void> _open(AppNotification notification) async {
    unawaited(NotificationRouter.open(notification, fromInbox: true));

    if (notification.isRead) return;

    setState(() {
      _items = _items
          .map((n) => n.id == notification.id ? n.copyWith(isRead: true) : n)
          .toList();
    });
    final unread = PushService.instance.unreadCount;
    if (unread.value > 0) unread.value -= 1;

    final response = await _service.markRead(notification.id);
    if (!mounted || response.success) return;
    await _load(silent: true);
  }

  /// Today = same calendar day as now; everything else falls under "Earlier".
  bool _isToday(AppNotification n) {
    final created = n.createdAt?.toLocal();
    if (created == null) return false;
    final now = DateTime.now();
    return created.year == now.year && created.month == now.month && created.day == now.day;
  }

  /// Prefers the wording the API sent for the reader's language — it is more
  /// precise than the type alone (a confirmed booking says different things to
  /// the rider and the driver).
  ///
  /// The per-type heading below is the fallback, for rows that carry only one
  /// language: anything an admin typed in English only, and anything stored
  /// before the API started sending both.
  String _title(BuildContext context, AppNotification n) {
    final lang = context.l10n.locale.languageCode;
    if (n.hasArabic(lang)) return n.titleFor(lang);

    final key = switch (n.kind) {
      NotificationKind.rideRequestNearby => 'notifType.rideRequestNearby',
      NotificationKind.bookingConfirmed => 'notifType.bookingConfirmed',
      NotificationKind.tripCancelled => 'notifType.tripCancelled',
      NotificationKind.driverAccepted => 'notifType.driverAccepted',
      NotificationKind.tripCompleted => 'notifType.tripCompleted',
      NotificationKind.bookingCancelled => 'notifType.bookingCancelled',
      NotificationKind.tripStarted => 'notifType.tripStarted',
      NotificationKind.driverArrived => 'notifType.driverArrived',
      NotificationKind.tripMatched => 'notifType.tripMatched',
      NotificationKind.driverVerified => 'notifType.driverVerified',
      NotificationKind.driverRejected => 'notifType.driverRejected',
      NotificationKind.ratingReceived => 'notifType.ratingReceived',
      NotificationKind.feedbackReplied => 'notifType.feedbackReplied',
      // Admin-composed — there is no key for it, so show what was sent.
      NotificationKind.general => '',
    };
    if (key.isEmpty) return n.title;
    return context.tr(key);
  }

  /// Relative age in the prototype's compact form (now / 2m / 1h / 3d).
  String _time(BuildContext context, AppNotification n) {
    final created = n.createdAt;
    if (created == null) return '';
    final age = DateTime.now().toUtc().difference(created.toUtc());
    if (age.inMinutes < 1) return context.tr('notifTime.now');
    if (age.inHours < 1) return context.tr('notifTime.minutes', {'value': age.inMinutes});
    if (age.inDays < 1) return context.tr('notifTime.hours', {'value': age.inHours});
    return context.tr('notifTime.days', {'value': age.inDays});
  }

  bool _isNow(AppNotification n) {
    final created = n.createdAt;
    if (created == null) return false;
    return DateTime.now().toUtc().difference(created.toUtc()).inMinutes < 1;
  }

  /// icon + icon-tile colours, and (for the two live kinds) a card tint.
  ({IconData icon, Color fg, Color tile, Color? card}) _style(WanesTokens t, NotificationKind kind) =>
      switch (kind) {
        NotificationKind.bookingConfirmed =>
          (icon: Icons.check_rounded, fg: t.onTeal, tile: t.teal, card: t.tealTint),
        NotificationKind.tripMatched => (
            icon: Icons.alt_route_rounded,
            fg: t.onTeal,
            tile: t.teal,
            card: t.tealTint
          ),
        NotificationKind.driverAccepted ||
        NotificationKind.tripStarted ||
        NotificationKind.driverArrived => (
            icon: Icons.directions_car_rounded,
            fg: const Color(0xFF2A1000),
            tile: t.amber,
            card: t.amberTint
          ),
        NotificationKind.rideRequestNearby => (
            icon: Icons.my_location_rounded,
            fg: t.info,
            tile: t.info.withValues(alpha: .16),
            card: null
          ),
        NotificationKind.tripCompleted =>
          (icon: Icons.star_outline_rounded, fg: t.amberInk, tile: t.surface2, card: null),
        NotificationKind.ratingReceived =>
          (icon: Icons.star_rounded, fg: t.amberInk, tile: t.surface2, card: null),
        // Being cleared to drive is the best news the app sends a driver, so it
        // gets the same teal treatment as a confirmed booking.
        NotificationKind.driverVerified => (
            icon: Icons.verified_rounded,
            fg: t.onTeal,
            tile: t.teal,
            card: t.tealTint
          ),
        NotificationKind.tripCancelled ||
        NotificationKind.bookingCancelled ||
        NotificationKind.driverRejected => (
            icon: Icons.close_rounded,
            fg: const Color(0xFFE05F9A),
            tile: const Color(0x29E05F9A),
            card: null
          ),
        // An answer from a person, so it reads as a reply rather than as an
        // event the platform generated.
        NotificationKind.feedbackReplied =>
          (icon: Icons.forum_rounded, fg: t.tealInk, tile: t.tealTint, card: null),
        NotificationKind.general =>
          (icon: Icons.info_outline_rounded, fg: t.ink2, tile: t.surface2, card: null),
      };

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final today = _items.where(_isToday).toList();
    final earlier = _items.where((n) => !_isToday(n)).toList();

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          color: t.tealInk,
          onRefresh: () => _load(silent: true),
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(20, 14, 20, 24),
            children: [
              ScreenHeader(
                title: context.tr('notif.title'),
                trailing: _hasUnread
                    ? GestureDetector(
                        onTap: _markAllRead,
                        child: Text(context.tr('notif.markAllRead'),
                            style: WanesTheme.mono(
                                size: 12, weight: FontWeight.w600, color: t.tealInk, spacing: 0)),
                      )
                    : null,
              ),
              if (_loading)
                _busy(t)
              else if (_error != null)
                _failed(t)
              else ...[
                if (today.isNotEmpty) ...[
                  _groupLabel(t, context.tr('common.today')),
                  ..._cards(t, today),
                ],
                if (earlier.isNotEmpty) ...[
                  _groupLabel(t, context.tr('notif.earlier')),
                  ..._cards(t, earlier),
                ],
                if (_items.isEmpty) _empty(t),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _busy(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 90),
        child: Center(child: WanesSpinner(color: t.tealInk)),
      );

  Widget _failed(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 80),
        child: Column(children: [
          Icon(Icons.cloud_off_rounded, size: 34, color: t.ink2),
          const SizedBox(height: 14),
          Text(_error!, textAlign: TextAlign.center, style: TextStyle(color: t.ink2)),
          const SizedBox(height: 16),
          GestureDetector(
            onTap: _load,
            child: Text(context.tr('common.retry'),
                style: WanesTheme.mono(
                    size: 12, weight: FontWeight.w600, color: t.tealInk, spacing: 0)),
          ),
        ]),
      );

  Widget _groupLabel(WanesTokens t, String label) => Padding(
        padding: const EdgeInsets.only(top: 18, bottom: 10),
        child: MonoLabel(label, size: 10.5, spacing: 1.05),
      );

  List<Widget> _cards(WanesTokens t, List<AppNotification> items) => [
        for (var i = 0; i < items.length; i++)
          Padding(
            padding: EdgeInsets.only(top: i == 0 ? 0 : 10),
            child: _card(t, items[i]),
          ),
      ];

  Widget _card(WanesTokens t, AppNotification n) {
    final s = _style(t, n.kind);
    final unread = !n.isRead;
    return GestureDetector(
      onTap: () => _open(n),
      behavior: HitTestBehavior.opaque,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
        decoration: BoxDecoration(
          color: (unread ? s.card : null) ?? t.surface,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: t.border),
        ),
        child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Container(
            width: 38,
            height: 38,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: s.tile, borderRadius: BorderRadius.circular(11)),
            child: Icon(s.icon, size: 20, color: s.fg),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(_title(context, n),
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, color: t.ink)),
              const SizedBox(height: 2),
              Text(n.bodyFor(context.l10n.locale.languageCode),
                  style: TextStyle(fontSize: 12, color: t.ink2, height: 1.4)),
            ]),
          ),
          const SizedBox(width: 10),
          if (unread && _isNow(n))
            Text(_time(context, n),
                style: WanesTheme.mono(size: 10, weight: FontWeight.w600, color: t.amberInk, spacing: 0))
          else if (unread)
            Container(
              margin: const EdgeInsets.only(top: 14),
              width: 8,
              height: 8,
              decoration: BoxDecoration(color: t.info, shape: BoxShape.circle),
            )
          else
            Text(_time(context, n),
                style: WanesTheme.mono(size: 10, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
        ]),
      ),
    );
  }

  Widget _empty(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 80),
        child: Column(children: [
          Container(
            width: 64, height: 64,
            decoration: BoxDecoration(color: t.tealTint, borderRadius: BorderRadius.circular(20)),
            child: Icon(Icons.notifications_none_rounded, size: 28, color: t.tealInk),
          ),
          const SizedBox(height: 16),
          Text(context.tr('notif.allCaughtUp'),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('notif.allCaughtUpBody'), style: TextStyle(color: t.ink2)),
        ]),
      );
}
