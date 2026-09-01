import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'notifications_screen.dart';
import 'booking_details_screen.dart';
import '../widgets/wanes_motion.dart';

/// The rider "Bookings" tab — every seat the rider has taken, split into the
/// ones still ahead of them and everything already behind (completed, cancelled
/// or simply departed). Tapping a row opens [BookingDetailsScreen].
class BookingsScreen extends StatefulWidget {
  const BookingsScreen({super.key});

  @override
  State<BookingsScreen> createState() => BookingsScreenState();
}

class BookingsScreenState extends State<BookingsScreen> {
  final _bookings = BookingService();

  int _tab = 0; // 0 upcoming · 1 past
  List<Booking> _list = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    load();
  }

  /// Public so the shell can refresh the tab when the rider returns to it.
  Future<void> load() async {
    if (mounted) setState(() => _loading = _list.isEmpty);
    final res = await _bookings.myBookings();
    if (!mounted) return;
    setState(() {
      _loading = false;
      if (res.success) {
        _list = res.data ?? [];
        _error = null;
      } else {
        _error = res.errorMessage ?? context.tr('bookings.loadFailed');
      }
    });
  }

  List<Booking> get _upcoming => _list.where((b) => b.isUpcoming).toList();

  /// Everything the rider can no longer travel on — completed, cancelled, or a
  /// confirmed seat whose departure has already passed.
  List<Booking> get _past => _list.where((b) => !b.isUpcoming).toList();

  Future<void> _open(Booking booking) async {
    final changed = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => BookingDetailsScreen(booking: booking)),
    );
    if (changed == true) load();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final rows = _tab == 0 ? _upcoming : _past;

    return SafeArea(
      bottom: false,
      child: RefreshIndicator(
        onRefresh: load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
          children: [
            Row(children: [
              Expanded(
                child: Text(context.tr('bookings.title'),
                    style: TextStyle(
                        fontSize: 23, fontWeight: FontWeight.w800, letterSpacing: -0.4, color: t.ink)),
              ),
              NotificationBellButton(
                onTap: () => Navigator.push(context,
                    MaterialPageRoute(builder: (_) => const NotificationsScreen())),
              ),
            ]),
            const SizedBox(height: 16),
            SegmentedToggle(
              labels: [
                '${context.tr('trips.upcoming')} · ${_upcoming.length}',
                context.tr('trips.past'),
              ],
              index: _tab,
              onSelect: (i) => setState(() => _tab = i),
            ),
            const SizedBox(height: 18),
            if (_loading)
              const Padding(
                padding: EdgeInsets.only(top: 60),
                child: Center(child: WanesSpinner()),
              )
            else if (_error != null)
              WanesInlineAlert(_error!, onTap: load)
            else if (rows.isEmpty)
              _empty(t)
            else
              ...rows.map((b) => Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: _bookingCard(t, b),
                  )),
          ],
        ),
      ),
    );
  }

  Widget _bookingCard(WanesTokens t, Booking b) {
    final depart = b.departAt?.toLocal();
    return WanesCard(
      radius: 16,
      padding: const EdgeInsets.all(14),
      onTap: () => _open(b),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Expanded(
            child: Text(b.reference,
                textDirection: TextDirection.ltr,
                style: WanesTheme.mono(size: 11.5, weight: FontWeight.w600, color: t.ink2, spacing: 1.0)),
          ),
          StatusPill(
            label: context.tr(b.statusKey),
            color: t.bookingStatus(b.status),
            dot: b.isLive,
          ),
        ]),
        const SizedBox(height: 12),
        Row(children: [
          const RouteDot(),
          const SizedBox(width: 8),
          Expanded(
            child: Text(b.originAddress.isEmpty ? '—' : b.originAddress,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
          ),
        ]),
        Padding(
          padding: const EdgeInsetsDirectional.only(start: 5, top: 4, bottom: 4),
          child: Container(width: 2, height: 14, color: t.border),
        ),
        Row(children: [
          const RouteDot(destination: true),
          const SizedBox(width: 8),
          Expanded(
            child: Text(b.destinationAddress.isEmpty ? '—' : b.destinationAddress,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
          ),
        ]),
        const SizedBox(height: 12),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12)),
          child: Row(children: [
            Icon(Icons.schedule, size: 15, color: t.ink2),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                  depart == null
                      ? '—'
                      : DateFormat('EEE, MMM d · HH:mm', context.l10n.localeName).format(depart),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(color: t.ink, fontSize: 12.5, fontWeight: FontWeight.w600)),
            ),
            const SizedBox(width: 8),
            Icon(Icons.event_seat_outlined, size: 15, color: t.ink2),
            const SizedBox(width: 6),
            Text('${b.seats}',
                textDirection: TextDirection.ltr,
                style: WanesTheme.mono(size: 12, weight: FontWeight.w700, color: t.ink2)),
          ]),
        ),
      ]),
    );
  }

  Widget _empty(WanesTokens t) {
    return Padding(
      padding: const EdgeInsets.only(top: 48),
      child: Column(children: [
        Container(
          width: 64,
          height: 64,
          decoration: BoxDecoration(color: t.tealTint, borderRadius: BorderRadius.circular(20)),
          child: Icon(_tab == 0 ? Icons.confirmation_number_outlined : Icons.history_rounded,
              color: t.tealInk, size: 28),
        ),
        const SizedBox(height: 16),
        Text(context.tr(_tab == 0 ? 'bookings.noUpcoming' : 'bookings.noPast'),
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
        const SizedBox(height: 6),
        Text(context.tr(_tab == 0 ? 'bookings.noUpcomingBody' : 'bookings.noPastBody'),
            textAlign: TextAlign.center, style: TextStyle(color: t.ink2, fontSize: 13.5)),
      ]),
    );
  }
}
