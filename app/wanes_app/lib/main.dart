import 'package:flutter/material.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'core/l10n.dart';
import 'core/session.dart';
import 'core/theme.dart';
import 'features/splash_screen.dart';
import 'features/login_screen.dart';
import 'features/rider_shell.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // Weekday/month names for both languages (`DateFormat('EEE, MMM d', 'ar')`).
  await initializeDateFormatting();
  await LocaleController.load();
  await Session.instance.load();
  runApp(const WanesApp());
}

class WanesApp extends StatelessWidget {
  const WanesApp({super.key});

  /// Light/dark selection, driven by the sun/moon button on the profile
  /// screens (prototype 12). Starts on the system setting.
  static final ValueNotifier<ThemeMode> themeMode = ValueNotifier(ThemeMode.system);

  /// Flip between light and dark, resolving "system" against what is on screen.
  static void toggleTheme(BuildContext context) {
    final dark = themeMode.value == ThemeMode.system
        ? Theme.of(context).brightness == Brightness.dark
        : themeMode.value == ThemeMode.dark;
    themeMode.value = dark ? ThemeMode.light : ThemeMode.dark;
  }

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<Locale>(
      valueListenable: LocaleController.locale,
      builder: (_, locale, __) {
        // The type stack is language-dependent, so the themes are rebuilt
        // alongside the locale rather than cached.
        WanesTheme.arabic = locale.languageCode == 'ar';
        return ValueListenableBuilder<ThemeMode>(
          valueListenable: themeMode,
          builder: (_, mode, __) => MaterialApp(
            title: 'Wanes',
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
        );
      },
    );
  }
}
