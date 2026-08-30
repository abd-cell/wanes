import 'package:flutter/material.dart';
import '../widgets/wanes_ui.dart';
import 'home_screen.dart';
import 'profile_screen.dart';
import 'trips_screen.dart';

/// The rider app shell: Home · Trips · Profile behind the floating bottom nav
/// (prototype's three-tab bar). Tabs are kept alive via an IndexedStack.
class RiderShell extends StatefulWidget {
  const RiderShell({super.key, this.initialIndex = 0});
  final int initialIndex;

  @override
  State<RiderShell> createState() => _RiderShellState();
}

class _RiderShellState extends State<RiderShell> {
  late int _index = widget.initialIndex;

  @override
  Widget build(BuildContext context) {
    final tabs = [
      HomeScreen(onOpenProfile: () => setState(() => _index = 2)),
      const TripsScreen(),
      const ProfileScreen(),
    ];
    return Scaffold(
      body: IndexedStack(index: _index, children: tabs),
      bottomNavigationBar: WanesBottomNav(index: _index, onSelect: (i) => setState(() => _index = i)),
    );
  }
}
