import 'package:flutter/material.dart';
import '../core/api_log.dart';
import '../core/l10n.dart';
import '../core/saved_places.dart';
import '../core/push_service.dart';
import '../core/session.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/language_picker.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'driver/driver_home_screen.dart';
import 'edit_profile_screen.dart';
import 'login_screen.dart';
import 'notifications_screen.dart';
import 'api_log_screen.dart';
import 'contact_us_screen.dart';
import 'faq_screen.dart';
import 'saved_places_screen.dart';
import '../widgets/wanes_motion.dart';

/// Rider profile tab. Mirrors prototype screen 11 (Rider profile): identity
/// header, stat card, quick links, the "Become a driver" affordance and sign-out.
class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  final _auth = AuthService();
  final _bookingService = BookingService();
  Profile? _profile;
  List<Booking> _rides = const [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _profile = Session.instance.profile;
    _load();
  }

  Future<void> _load() async {
    final res = await _auth.getProfile();
    final rides = await _bookingService.myBookings();
    if (!mounted) return;
    setState(() {
      _loading = false;
      if (res.success && res.data != null) _profile = res.data;
      _rides = rides.data ?? const [];
    });
    // Fills the "Saved places" summary line; the row works either way.
    await SavedPlaces.instance.load();
    if (mounted) setState(() {});
  }

  /// The metric tiles on design 11, from the rider's own booking history.
  int get _tripsTaken => _rides.where((b) => !b.isCancelled).length;
  int get _upcoming => _rides.where((b) => b.isUpcoming).length;

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first[0] + parts.last[0]).toUpperCase();
  }

  Future<void> _edit() async {
    final saved = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => EditProfileScreen(profile: _profile)),
    );
    if (saved == true && mounted) {
      setState(() => _profile = Session.instance.profile);
      _load();
    }
  }

  Future<void> _becomeDriver() async {
    final res = await _auth.switchRole(2);
    if (!mounted) return;
    if (res.success) {
      Navigator.push(context, MaterialPageRoute(builder: (_) => const DriverHomeScreen()));
    } else {
      WanesAlerts.failure(context, res,
          title: context.tr('driver.switchFailed'), onRetry: _becomeDriver);
    }
  }

  /// The shortcuts read as a summary line — "Home · Work · Gym" — so the row
  /// says what is actually set rather than advertising a fixed trio.
  /// Unread count next to the Notifications row. Listens to the live counter,
  /// so a push that lands while this tab is on screen updates it in place.
  Widget _unreadBadge() => ValueListenableBuilder<int>(
        valueListenable: PushService.instance.unreadCount,
        builder: (context, count, _) {
          if (count <= 0) return const SizedBox.shrink();
          final t = WanesTokens.of(context);
          return Container(
            padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
            decoration: BoxDecoration(color: t.info, borderRadius: BorderRadius.circular(999)),
            child: Text(
              count > 99 ? '99+' : '$count',
              style: WanesTheme.mono(
                  size: 10, weight: FontWeight.w700, color: Colors.white, spacing: 0),
            ),
          );
        },
      );

  String _savedPlacesSummary() {
    final places = SavedPlaces.instance.cached;
    if (places.isEmpty) return context.tr('places.addHomeWorkFavourites');
    return places.take(3).map((p) => p.name).join(' · ');
  }

  Future<void> _openSavedPlaces() async {
    await Navigator.push(context,
        MaterialPageRoute(builder: (_) => const SavedPlacesScreen()));
    if (mounted) setState(() {});
  }

  Future<void> _logout() async {
    // AuthService.logout owns the teardown — see there.
    await _auth.logout();
    if (!mounted) return;
    Navigator.pushAndRemoveUntil(
        context, MaterialPageRoute(builder: (_) => const LoginScreen()), (_) => false);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final p = _profile;
    final name = (p?.name.isNotEmpty ?? false) ? p!.name : context.tr('role.rider');

    if (_loading && p == null) {
      return const Center(child: WanesSpinner());
    }

    final dark = Theme.of(context).brightness == Brightness.dark;
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
            _identity(t, name, p),
            const SizedBox(height: 18),
            _metrics(t, p),
            const SizedBox(height: 16),
            _accountGroup(t),
            const SizedBox(height: 14),
            _becomeDriverButton(t, p),
            const SizedBox(height: 14),
            Center(
              child: GestureDetector(
                onTap: _logout,
                child: Text(context.tr('profile.logOut'),
                    style: TextStyle(
                        fontWeight: FontWeight.w600, fontSize: 14, color: t.amberInk)),
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// 64px avatar, name, and the rating line (design 11).
  Widget _identity(WanesTokens t, String name, Profile? p) {
    return GestureDetector(
      onTap: _edit,
      behavior: HitTestBehavior.opaque,
      child: Row(children: [
        AvatarBadge(_initials(name), size: 64, tint: t.tealTint, fg: t.tealInk),
        const SizedBox(width: 15),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(name,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18, color: t.ink)),
            const SizedBox(height: 3),
            InlineRating(
                '${(p?.ratingAvg ?? 0).toStringAsFixed(1)} · ${context.tr('role.rider')}',
                size: 12),
            const SizedBox(height: 2),
            Text(p?.phone ?? '',
                textDirection: TextDirection.ltr,
                style: WanesTheme.mono(size: 11.5, weight: FontWeight.w500, color: t.ink2, spacing: 0)),
          ]),
        ),
        Icon(Icons.edit_outlined, size: 18, color: t.ink2),
      ]),
    );
  }

  Widget _metrics(WanesTokens t, Profile? p) {
    return IntrinsicHeight(
      child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Expanded(
            child: MetricTile(value: '$_tripsTaken', label: context.tr('nav.trips'))),
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
                value: '$_upcoming',
                label: context.tr('trips.upcoming'),
                valueColor: t.tealInk)),
      ]),
    );
  }

  Widget _accountGroup(WanesTokens t) {
    return GroupedCard(children: [
      GroupedRow(
        icon: Icons.person_outline_rounded,
        title: context.tr('profile.editProfile'),
        subtitle: context.tr('profile.editProfileSubtitle'),
        onTap: _edit,
      ),
      GroupedRow(
        icon: Icons.place_outlined,
        title: context.tr('places.savedPlaces'),
        subtitle: _savedPlacesSummary(),
        onTap: _openSavedPlaces,
      ),
      const LanguageRow(),
      GroupedRow(
        icon: Icons.notifications_none_rounded,
        title: context.tr('notif.title'),
        trailing: _unreadBadge(),
        onTap: () => Navigator.push(
            context, MaterialPageRoute(builder: (_) => const NotificationsScreen())),
      ),
      GroupedRow(
        icon: Icons.help_outline_rounded,
        title: context.tr('faq.title'),
        subtitle: context.tr('faq.subtitle'),
        onTap: () => Navigator.push(
            context, MaterialPageRoute(builder: (_) => const FaqScreen())),
      ),
      GroupedRow(
        icon: Icons.support_agent_rounded,
        title: context.tr('contact.title'),
        subtitle: context.tr('contact.subtitle'),
        onTap: () => Navigator.push(
            context, MaterialPageRoute(builder: (_) => const ContactUsScreen())),
      ),
      // Developer aid. `ApiLog.enabled` is `kDebugMode`, so the row — and the
      // screen behind it — are tree-shaken out of release builds.
      if (ApiLog.enabled)
        GroupedRow(
          icon: Icons.swap_vert_rounded,
          title: context.tr('apiLog.title'),
          subtitle: context.tr('apiLog.subtitle'),
          onTap: () => Navigator.push(
              context, MaterialPageRoute(builder: (_) => const ApiLogScreen())),
        ),
    ]);
  }

  /// The teal-tint, teal-bordered driver CTA from the design.
  Widget _becomeDriverButton(WanesTokens t, Profile? p) {
    final isDriver = p?.isDriver == true;
    return Material(
      color: t.tealTint,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(color: t.teal),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: _becomeDriver,
        child: Container(
          height: 50,
          alignment: Alignment.center,
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            Icon(Icons.directions_car_outlined, size: 18, color: t.tealInk),
            const SizedBox(width: 9),
            Text(context.tr(isDriver ? 'driver.switchToDriving' : 'driver.becomeDriver'),
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14, color: t.tealInk)),
          ]),
        ),
      ),
    );
  }
}
