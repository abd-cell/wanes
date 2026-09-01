import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'core/app_config.dart';
import 'core/l10n.dart';
import 'core/notification_router.dart';
import 'core/push_service.dart';
import 'core/session.dart';
import 'core/theme.dart';
import 'core/trip_sort.dart';
import 'models/models.dart';
import 'services/services.dart';
import 'features/splash_screen.dart';
import 'features/login_screen.dart';
import 'features/rider_shell.dart';
import 'widgets/wanes_logo.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // Weekday/month names for both languages (`DateFormat('EEE, MMM d', 'ar')`).
  await initializeDateFormatting();
  await LocaleController.load();
  // The admin-controlled brand colour and currency. Reads the local mirror only,
  // so the first frame is already on-brand; the server copy is fetched below,
  // off the launch path.
  await AppConfigController.load();
  await Session.instance.load();
  // The rider's results sort, so their first search asks the server for the
  // order they already chose rather than defaulting back to best-match.
  await SortPreference.instance.load();
  // After the session: the cached profile carries the account's saved theme,
  // and the toggle writes changes back to the account through this hook.
  await ThemeController.load(profile: Session.instance.profile);
  ThemeController.saveToAccount =
      (AppTheme theme) => AuthService().updatePreferences(theme: theme);
  // No-op until Firebase credentials are filled in (lib/core/firebase_options.dart);
  // in-app notifications still arrive over SSE either way.
  await PushService.instance.init();
  // Attach before the first frame: a tap that launched the app is already
  // waiting inside PushService, and the router holds anything that arrives
  // while the splash still owns the screen.
  NotificationRouter.attach();
  runApp(const WanesApp());

  // Not awaited: settings rarely change, and a slow or unreachable backend must
  // not hold up the first frame. When it does land, the notifier rebuilds the
  // themes in place.
  unawaited(ConfigService().refresh());
}

class WanesApp extends StatelessWidget {
  const WanesApp({super.key});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<Locale>(
      valueListenable: LocaleController.locale,
      builder: (_, locale, __) {
        // The type stack is language-dependent, so the themes are rebuilt
        // alongside the locale rather than cached.
        WanesTheme.arabic = locale.languageCode == 'ar';
        // The brand colour feeds every theme token, so an admin changing it has
        // to rebuild the themes — not just repaint what is already on screen.
        return ValueListenableBuilder<AppConfig>(
          valueListenable: AppConfigController.config,
          builder: (_, __, ___) => ValueListenableBuilder<ThemeMode>(
            valueListenable: ThemeController.mode,
            builder: (_, mode, __) => MaterialApp(
              title: kWanesAppName,
              // A tapped notification routes from outside the widget tree, so
              // it needs a navigator it can reach without a BuildContext.
              navigatorKey: NotificationRouter.navigatorKey,
              debugShowCheckedModeBanner: false,
              theme: WanesTheme.light(),
              darkTheme: WanesTheme.dark(),
              themeMode: mode,
              locale: locale,
              supportedLocales: AppLocalizations.supportedLocales,
              localizationsDelegates: AppLocalizations.localizationsDelegates,
              home: const SplashScreen(),
              routes: {
                '/login': (_) => const LoginScreen(),
                '/home': (_) => const RiderShell(),
              },
            ),
          ),
        );
      },
    );
  }
}
