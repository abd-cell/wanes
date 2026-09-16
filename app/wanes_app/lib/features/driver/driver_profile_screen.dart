import 'package:flutter/material.dart';
import '../../core/l10n.dart';
import '../../core/session.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/language_picker.dart';
import '../../widgets/wanes_alerts.dart';
import '../notifications_screen.dart';
import '../../widgets/wanes_ui.dart';
import '../schedules_screen.dart';
import '../contact_us_screen.dart';
import '../edit_profile_screen.dart';
import '../login_screen.dart';
import 'driver_apply_screen.dart';
import 'vehicles_screen.dart';

/// Driver profile — prototype screen 12. Verified identity, the Trips /
/// Rating / Completed metrics, the vehicle card, a grouped settings list and
/// the switch back to riding.
class DriverProfileScreen extends StatefulWidget {
  const DriverProfileScreen({super.key});

  @override
  State<DriverProfileScreen> createState() => _DriverProfileScreenState();
}

class _DriverProfileScreenState extends State<DriverProfileScreen> {
  final _auth = AuthService();
  final _vehicleService = VehicleService();
  final _tripService = TripService();

  Profile? _profile;
  List<Vehicle> _vehicles = [];
  List<Trip> _trips = [];

  @override
  void initState() {
    super.initState();
    _profile = Session.instance.profile;
    _load();
  }

  Future<void> _load() async {
    final prof = await _auth.getProfile();
    final veh = await _vehicleService.myVehicles();
    final trips = await _tripService.myTrips();
    if (!mounted) return;
    setState(() {
      if (prof.success && prof.data != null) _profile = prof.data;
      _vehicles = veh.data ?? [];
      _trips = trips.data ?? [];
    });
  }

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  /// Share of the driver's posted trips that reached "Completed" (status 4).
  String get _completionRate {
    if (_trips.isEmpty) return '—';
    final done = _trips.where((t) => t.status == 4).length;
    return '${(done / _trips.length * 100).round()}%';
  }

  Future<void> _editProfile() async {
    final saved = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => EditProfileScreen(profile: _profile)),
    );
    if (saved == true) _load();
  }

  /// The driver's way out. There was none: the only Log out in the app lived on
  /// the *rider* profile, so a driver had to switch back to riding first, and a
  /// driver-only account had no way to sign out at all.
  Future<void> _logout() async {
    await _auth.logout();
    if (!mounted) return;
    Navigator.pushAndRemoveUntil(
        context, MaterialPageRoute(builder: (_) => const LoginScreen()), (_) => false);
  }

  Future<void> _switchToRiding() async {
    await _auth.switchRole(1);
    if (!mounted) return;
    Navigator.pop(context);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final dark = Theme.of(context).brightness == Brightness.dark;
    final p = _profile;
    final name = (p?.name.isNotEmpty ?? false) ? p!.name : context.tr('role.driver');
    final verified = p?.driverStatus == 2;
    final vehicle = _vehicles.isNotEmpty ? _vehicles.first : null;

    return SafeArea(
      bottom: false,
      child: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 14, 20, 24),
          children: [
            Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
              Text(context.tr('nav.profile'),
                  style: TextStyle(
                      fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: -0.44, color: t.ink)),
              Row(children: [
                NotificationBellButton(
                onTap: () => Navigator.push(context,
                    MaterialPageRoute(builder: (_) => const NotificationsScreen())),
              ),
                const SizedBox(width: 10),
                CircleIconButton(
                  icon: dark ? Icons.dark_mode_outlined : Icons.light_mode_outlined,
                  color: t.ink2,
                  onTap: () => ThemeController.toggle(context),
                ),
              ]),
            ]),
            const SizedBox(height: 16),
            _identity(t, name, p, verified),
            const SizedBox(height: 18),
            _metrics(t, p),
            const SizedBox(height: 16),
            _vehicleCard(t, vehicle),
            const SizedBox(height: 12),
            _settingsGroup(t, verified),
            const SizedBox(height: 14),
            _switchButton(t),
            const SizedBox(height: 14),
            Center(
              child: GestureDetector(
                onTap: _logout,
                child: Text(context.tr('profile.logOut'),
                    style: TextStyle(
                        fontWeight: FontWeight.w600, fontSize: 14, color: t.amberInk)),
              ),
            ),
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
  }

  /// 64px avatar with the teal verified tick, name + VERIFIED chip, rating line.
  Widget _identity(WanesTokens t, String name, Profile? p, bool verified) {
    return Row(children: [
      SizedBox(
        width: 64,
        height: 64,
        child: Stack(clipBehavior: Clip.none, children: [
          AvatarBadge(_initials(name), size: 64, tint: t.tealTint, fg: t.tealInk),
          if (verified)
            PositionedDirectional(
              end: -2,
              bottom: -2,
              child: Container(
                width: 22,
                height: 22,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: t.teal,
                  shape: BoxShape.circle,
                  border: Border.all(color: t.bg, width: 3),
                ),
                child: Icon(Icons.check_rounded, size: 12, color: t.onTeal),
              ),
            ),
        ]),
      ),
      const SizedBox(width: 15),
      Expanded(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Flexible(
              child: Text(name,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18, color: t.ink)),
            ),
            const SizedBox(width: 8),
            if (verified)
              TintChip(context.tr('driver.verified'),
                  size: 9,
                  radius: 6,
                  padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3))
            else
              TintChip(context.tr('driver.pending'),
                  size: 9,
                  radius: 6,
                  padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
                  tint: t.amberTint,
                  color: t.amberInk),
          ]),
          const SizedBox(height: 4),
          InlineRating(
            '${(p?.ratingAvg ?? 0).toStringAsFixed(1)} · '
            '${context.tr(verified ? 'driver.verifiedDriver' : 'driver.verificationPending')}',
            size: 12,
          ),
        ]),
      ),
    ]);
  }

  Widget _metrics(WanesTokens t, Profile? p) {
    return IntrinsicHeight(
      child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Expanded(
            child: MetricTile(value: '${_trips.length}', label: context.tr('nav.trips'))),
        const SizedBox(width: 10),
        Expanded(
          child: MetricTile(
            value: (p?.ratingAvg ?? 0).toStringAsFixed(1),
            label: context.tr('profile.rating'),
            valueColor: t.amberInk,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
            child: MetricTile(
                value: _completionRate,
                label: context.tr('tripStatus.completed'),
                valueColor: t.tealInk)),
      ]),
    );
  }

  Widget _vehicleCard(WanesTokens t, Vehicle? v) {
    if (v == null) {
      return WanesCard(
        radius: 16,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        onTap: _openVehicles,
        child: Row(children: [
          Container(
            width: 44,
            height: 44,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12)),
            child: Icon(Icons.add_rounded, color: t.tealInk, size: 22),
          ),
          const SizedBox(width: 13),
          Expanded(
            child: Text(context.tr('vehicle.addVehicle'),
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
          ),
          Text(context.tr('common.add'),
              style: WanesTheme.mono(size: 12, weight: FontWeight.w700, color: t.tealInk, spacing: 0)),
        ]),
      );
    }
    return WanesCard(
      radius: 16,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      child: Row(children: [
        Container(
          width: 44,
          height: 44,
          alignment: Alignment.center,
          decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12)),
          child: Icon(Icons.directions_car_filled_rounded, color: t.tealInk, size: 24),
        ),
        const SizedBox(width: 13),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('${v.make} ${v.model}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
            const SizedBox(height: 2),
            Text('${v.plate} · ${context.trPlural('vehicle.seatCount', v.seatCapacity)}',
                style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0.6)),
          ]),
        ),
        const SizedBox(width: 10),
        GestureDetector(
          onTap: _openVehicles,
          child: Text(context.tr('common.edit'),
              style: WanesTheme.mono(size: 12, weight: FontWeight.w600, color: t.tealInk, spacing: 0)),
        ),
      ]),
    );
  }

  Widget _settingsGroup(WanesTokens t, bool verified) {
    return GroupedCard(children: [
      GroupedRow(
        icon: Icons.repeat_rounded,
        title: context.tr('schedule.title'),
        subtitle: context.tr('schedule.subtitleDriver'),
        onTap: () => Navigator.push(context,
            MaterialPageRoute(builder: (_) => const SchedulesScreen(asDriver: true))),
      ),
      GroupedRow(
        icon: Icons.person_outline_rounded,
        title: context.tr('profile.editProfile'),
        subtitle: context.tr('profile.editProfileSubtitle'),
        onTap: _editProfile,
      ),
      const LanguageRow(),
      GroupedRow(
        icon: Icons.description_outlined,
        title: context.tr('driver.documents'),
        iconColor: verified ? t.tealInk : t.warning,
        trailing: Text(context.tr(verified ? 'driver.allValid' : 'driver.pending'),
            style: WanesTheme.mono(
                size: 10,
                weight: FontWeight.w700,
                color: verified ? t.tealInk : t.warning,
                spacing: 0)),
        onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const DriverApplyScreen()))
            .then((_) => _load()),
      ),
      GroupedRow(
        icon: Icons.directions_car_outlined,
        title: context.tr('vehicle.myVehicles'),
        subtitle: _vehicles.isEmpty
            ? context.tr('common.noneYet')
            : _vehicles.map((v) => v.plate).join(' · '),
        onTap: _openVehicles,
      ),
      GroupedRow(
        icon: Icons.star_outline_rounded,
        title: context.tr('driver.ratingsReviews'),
        onTap: () => WanesAlerts.info(context, context.tr('common.notAvailableYet'),
            message: context.tr('driver.reviewsComingSoon')),
      ),
      GroupedRow(
        icon: Icons.support_agent_rounded,
        title: context.tr('contact.title'),
        subtitle: context.tr('contact.subtitle'),
        onTap: () => Navigator.push(
            context, MaterialPageRoute(builder: (_) => const ContactUsScreen())),
      ),
    ]);
  }

  Widget _switchButton(WanesTokens t) {
    return Material(
      color: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(color: t.border),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: _switchToRiding,
        child: Container(
          height: 50,
          alignment: Alignment.center,
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            Icon(Icons.swap_vert_rounded, size: 18, color: t.ink),
            const SizedBox(width: 9),
            Text(context.tr('driver.switchToRiding'),
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14, color: t.ink)),
          ]),
        ),
      ),
    );
  }

  void _openVehicles() =>
      Navigator.push(context, MaterialPageRoute(builder: (_) => const VehiclesScreen())).then((_) => _load());
}
