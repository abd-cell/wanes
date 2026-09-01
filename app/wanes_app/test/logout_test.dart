import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/saved_places.dart';
import 'package:wanes_app/core/session.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/driver/driver_profile_screen.dart';
import 'package:wanes_app/features/profile_screen.dart';
import 'package:wanes_app/models/models.dart';
import 'package:wanes_app/services/services.dart';

/// Signing out.
///
/// Two things are pinned here, both of them things that were wrong. Every
/// profile the app has must offer a way out — the driver's did not, so a
/// driver-only account had no way to sign out at all. And the teardown has to
/// live in one place: it used to be spread across the call sites, and they had
/// already drifted apart on what they remembered to clear.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    AppLocalizations.current = const AppLocalizations(Locale('en'));
    // Both profile screens paint from the cached session while their fetch is
    // in flight; without one the rider screen sits on a spinner instead.
    await Session.instance.save(
      'token',
      'refresh',
      Profile(id: 1, phone: '+962790000000', firstName: 'Test'),
    );
  });

  Widget host(Widget child) => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: Scaffold(body: child),
      );

  /// Both profile screens fetch on open; with no server the calls fail closed
  /// and the screen paints from the cached session, which is what we want here.
  ///
  /// The surface is made tall on purpose: these are long scrolling pages and the
  /// sign-out sits at the very bottom, so on a phone-sized viewport the row is
  /// never built and the finder would report it missing whether it exists or not.
  Future<void> settle(WidgetTester tester, Widget screen) async {
    tester.view.physicalSize = const Size(1200, 4000);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(host(screen));
    await tester.pump();
    await tester.pump(const Duration(seconds: 1));
  }

  testWidgets('the rider profile offers a way out', (tester) async {
    await settle(tester, const ProfileScreen());
    expect(find.text(AppLocalizations.current.t('profile.logOut')), findsOneWidget);
  });

  testWidgets('the driver profile offers one too', (tester) async {
    // The regression: the only Log out in the app lived on the rider profile,
    // so a driver had to switch back to riding first — and an account that was
    // driver-only could not sign out at all.
    await settle(tester, const DriverProfileScreen());
    expect(find.text(AppLocalizations.current.t('profile.logOut')), findsOneWidget);
  });

  testWidgets('signing out clears the session and the cached places',
      (tester) async {
    // Teardown belongs to AuthService.logout, not to whichever screen happened
    // to call it. Driven through the service directly: there is no server here,
    // so the API half fails and the local half must still complete.
    expect(Session.instance.isLoggedIn, isTrue);

    await AuthService().logout();

    expect(Session.instance.isLoggedIn, isFalse,
        reason: 'an unreachable server must not leave the user signed in');
    expect(Session.instance.token, isNull);
    expect(Session.instance.refreshToken, isNull);
    expect(Session.instance.profile, isNull);
    expect(SavedPlaces.instance.cached, isEmpty);
  });
}
