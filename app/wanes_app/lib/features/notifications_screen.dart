import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../widgets/wanes_ui.dart';

/// Category of a notification — drives its icon and accent colour.
enum NotifKind { booking, enroute, price, rate, promo, system }

/// Feed content is held as l10n keys rather than finished copy, so the list
/// re-reads in whichever language is on screen.
class WanesNotification {
  const WanesNotification({
    required this.kind,
    required this.titleKey,
    required this.bodyKey,
    required this.timeKey,
    this.timeValue,
    this.unread = false,
  });

  final NotifKind kind;
  final String titleKey;
  final String bodyKey;

  /// Relative age, e.g. `notifTime.minutes` with [timeValue] `2` → "2m".
  final String timeKey;
  final int? timeValue;
  final bool unread;

  bool get isNow => timeKey == 'notifTime.now';

  String time(BuildContext context) =>
      context.tr(timeKey, timeValue == null ? null : {'value': timeValue});

  WanesNotification copyWith({bool? unread}) => WanesNotification(
        kind: kind,
        titleKey: titleKey,
        bodyKey: bodyKey,
        timeKey: timeKey,
        timeValue: timeValue,
        unread: unread ?? this.unread,
      );
}

/// Rider notifications feed. Mirrors prototype screen 13 (Notifications):
/// a Today / Earlier grouped list with unread dots and "Mark all read".
///
/// Content is representative until the backend exposes a notifications feed;
/// read/unread state is handled locally.
class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  List<WanesNotification> _today = const [
    WanesNotification(
        kind: NotifKind.booking,
        titleKey: 'notif.bookingConfirmed',
        bodyKey: 'notif.bookingConfirmedBody',
        timeKey: 'notifTime.minutes',
        timeValue: 2,
        unread: true),
    WanesNotification(
        kind: NotifKind.enroute,
        titleKey: 'notif.driverOnWay',
        bodyKey: 'notif.driverOnWayBody',
        timeKey: 'notifTime.now',
        unread: true),
    WanesNotification(
        kind: NotifKind.price,
        titleKey: 'notif.priceDrop',
        bodyKey: 'notif.priceDropBody',
        timeKey: 'notifTime.hours',
        timeValue: 1),
  ];

  List<WanesNotification> _earlier = const [
    WanesNotification(
        kind: NotifKind.rate,
        titleKey: 'notif.rateTrip',
        bodyKey: 'notif.rateTripBody',
        timeKey: 'notifTime.days',
        timeValue: 1),
    WanesNotification(
        kind: NotifKind.promo,
        titleKey: 'notif.promo',
        bodyKey: 'notif.promoBody',
        timeKey: 'notifTime.days',
        timeValue: 2),
  ];

  bool get _hasUnread => _today.any((n) => n.unread) || _earlier.any((n) => n.unread);

  void _markAllRead() {
    setState(() {
      _today = _today.map((n) => n.copyWith(unread: false)).toList();
      _earlier = _earlier.map((n) => n.copyWith(unread: false)).toList();
    });
  }

  /// icon + icon-tile colours, and (for the two live kinds) a card tint.
  ({IconData icon, Color fg, Color tile, Color? card}) _style(WanesTokens t, NotifKind kind) =>
      switch (kind) {
        NotifKind.booking => (icon: Icons.check_rounded, fg: t.onTeal, tile: t.teal, card: t.tealTint),
        NotifKind.enroute => (
            icon: Icons.directions_car_rounded,
            fg: const Color(0xFF2A1000),
            tile: t.amber,
            card: t.amberTint
          ),
        NotifKind.price => (icon: Icons.sell_outlined, fg: t.info, tile: t.info.withValues(alpha: .16), card: null),
        NotifKind.rate => (icon: Icons.star_outline_rounded, fg: t.amberInk, tile: t.surface2, card: null),
        NotifKind.promo => (
            icon: Icons.card_giftcard_rounded,
            fg: const Color(0xFFE05F9A),
            tile: const Color(0x29E05F9A),
            card: null
          ),
        NotifKind.system => (icon: Icons.info_outline_rounded, fg: t.ink2, tile: t.surface2, card: null),
      };

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 14, 20, 24),
          children: [
            Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
              Text(context.tr('notif.title'),
                  style: TextStyle(
                      fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: -0.44, color: t.ink)),
              if (_hasUnread)
                GestureDetector(
                  onTap: _markAllRead,
                  child: Text(context.tr('notif.markAllRead'),
                      style: WanesTheme.mono(
                          size: 12, weight: FontWeight.w600, color: t.tealInk, spacing: 0)),
                ),
            ]),
            if (_today.isNotEmpty) ...[
              _groupLabel(t, context.tr('common.today')),
              ..._cards(t, _today),
            ],
            if (_earlier.isNotEmpty) ...[
              _groupLabel(t, context.tr('notif.earlier')),
              ..._cards(t, _earlier),
            ],
            if (_today.isEmpty && _earlier.isEmpty) _empty(t),
          ],
        ),
      ),
    );
  }

  Widget _groupLabel(WanesTokens t, String label) => Padding(
        padding: const EdgeInsets.only(top: 18, bottom: 10),
        child: MonoLabel(label, size: 10.5, spacing: 1.05),
      );

  List<Widget> _cards(WanesTokens t, List<WanesNotification> items) => [
        for (var i = 0; i < items.length; i++)
          Padding(
            padding: EdgeInsets.only(top: i == 0 ? 0 : 10),
            child: _card(t, items[i]),
          ),
      ];

  Widget _card(WanesTokens t, WanesNotification n) {
    final s = _style(t, n.kind);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
      decoration: BoxDecoration(
        color: s.card ?? t.surface,
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
            Text(context.tr(n.titleKey),
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13.5, color: t.ink)),
            const SizedBox(height: 2),
            Text(context.tr(n.bodyKey),
                style: TextStyle(fontSize: 12, color: t.ink2, height: 1.4)),
          ]),
        ),
        const SizedBox(width: 10),
        if (n.unread && n.isNow)
          Text(n.time(context),
              style: WanesTheme.mono(size: 10, weight: FontWeight.w600, color: t.amberInk, spacing: 0))
        else if (n.unread)
          Container(
            margin: const EdgeInsets.only(top: 14),
            width: 8,
            height: 8,
            decoration: BoxDecoration(color: t.info, shape: BoxShape.circle),
          )
        else
          Text(n.time(context),
              style: WanesTheme.mono(size: 10, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
      ]),
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
