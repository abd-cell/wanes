import 'package:flutter/material.dart';
import '../widgets/wanes_ui.dart';
import 'bookings_screen.dart';
import 'home_screen.dart';
import 'profile_screen.dart';

/// The rider app shell: Home · Bookings · Profile behind the floating bottom
/// nav. Tabs are kept alive via an IndexedStack, so returning to one keeps its
/// scroll position — the bookings tab refreshes itself instead.
class RiderShell extends StatefulWidget {
  const RiderShell({super.key, this.initialIndex = 0});
  final int initialIndex;

  @override
  State<RiderShell> createState() => _RiderShellState();
}

class _RiderShellState extends State<RiderShell> {
  static const _bookingsTab = 1;

  final _bookings = GlobalKey<BookingsScreenState>();

  late int _index = widget.initialIndex;

  /// Coming back to the bookings tab re-reads it: a booking may have been made
  /// (or cancelled) on another screen since it was last built.
  void _select(int next) {
    if (next == _index) return;
    setState(() => _index = next);
    if (next == _bookingsTab) _bookings.currentState?.load();
  }

  @override
  Widget build(BuildContext context) {
    final tabs = [
      const HomeScreen(),
      BookingsScreen(key: _bookings),
      const ProfileScreen(),
    ];
    return Scaffold(
      body: IndexedStack(index: _index, children: tabs),
      bottomNavigationBar: WanesBottomNav(
        index: _index,
        onSelect: _select,
        items: WanesBottomNav.riderItems(context),
      ),
    );
  }
}
