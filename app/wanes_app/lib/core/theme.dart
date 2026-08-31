import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../models/models.dart';
import 'app_config.dart';
import 'environment.dart';

/// Wanes brand palette — single source of truth, taken 1:1 from the
/// `Wanes prototype` design (CSS custom properties). Brand constants live
/// here; the full per-theme token set is exposed through [WanesTokens].
class WanesColors {
  // ── Brand ──
  static const ink = Color(0xFF0E1726); // primary text (light) / dark bg
  static const inkDeep = Color(0xFF04241F); // logo mark ink / text on teal
  static const muted = Color(0xFF5B6880); // secondary text (light)
  static const route = Color(0xFF0FAE9E); // teal — "go" / confirm accent
  static const routeDeep = Color(0xFF0A8578); // teal-ink (text on tint)
  static const ping = Color(0xFFFF7A2F); // amber — pin / live / hail
  static const pingDeep = Color(0xFFD1591A); // amber-ink (text on tint)

  // ── Functional (shared across themes) ──
  static const success = Color(0xFF12A877);
  static const alert = Color(0xFFE04B4F);
  static const info = Color(0xFF3F7FE6);
  static const warning = Color(0xFFE0A326);

  // Dark-theme variants of the functional colours — the prototype brightens
  // each one on dark so alerts keep their contrast against `bgDark`.
  static const successDark = Color(0xFF2BC088);
  static const alertDark = Color(0xFFF2686C);
  static const infoDark = Color(0xFF5E97F0);
  static const warningDark = Color(0xFFF0B647);

  // ── Light surfaces ──
  static const bgLight = Color(0xFFF4F7FB);
  static const surfaceLight = Color(0xFFFFFFFF);
  static const surface2Light = Color(0xFFEEF2F7);
  static const lineLight = Color(0xFFE4E9F0);

  // ── Dark surfaces ──
  static const bgDark = Color(0xFF0E1726);
  static const surfaceDark = Color(0xFF172336);
  static const surface2Dark = Color(0xFF1E2C42);
  static const lineDark = Color(0xFF26344C);
}

/// Extended, brightness-aware design tokens — the exact prototype palette.
/// Pull these anywhere with `WanesTokens.of(context)`.
@immutable
class WanesTokens extends ThemeExtension<WanesTokens> {
  const WanesTokens({
    required this.bg,
    required this.surface,
    required this.surface2,
    required this.ink,
    required this.ink2,
    required this.border,
    required this.teal,
    required this.tealInk,
    required this.tealTint,
    required this.amber,
    required this.amberInk,
    required this.amberTint,
    required this.success,
    required this.alert,
    required this.info,
    required this.warning,
    required this.successTint,
    required this.alertTint,
    required this.infoTint,
    required this.warningTint,
    required this.shadow,
    required this.mapBg,
    required this.mapLine,
    required this.mapRoad,
    required this.onTeal,
  });

  final Color bg;
  final Color surface;
  final Color surface2;
  final Color ink;
  final Color ink2;
  final Color border;
  final Color teal;
  final Color tealInk;
  final Color tealTint;
  final Color amber;
  final Color amberInk;
  final Color amberTint;
  final Color success;
  final Color alert;
  final Color info;
  final Color warning;

  // Soft fills behind a functional colour (toast glyph chips, alert banners).
  final Color successTint;
  final Color alertTint;
  final Color infoTint;
  final Color warningTint;

  final Color shadow;
  final Color mapBg;
  final Color mapLine;
  final Color mapRoad;

  /// Foreground colour for content sitting on a solid [teal] fill.
  final Color onTeal;

  static const light = WanesTokens(
    bg: Color(0xFFF4F7FB),
    surface: Color(0xFFFFFFFF),
    surface2: Color(0xFFEEF2F7),
    ink: Color(0xFF0E1726),
    ink2: Color(0xFF5B6880),
    border: Color(0xFFE4E9F0),
    teal: Color(0xFF0FAE9E),
    tealInk: Color(0xFF0A8578),
    tealTint: Color(0x1F0FAE9E), // rgba(15,174,158,0.12)
    amber: Color(0xFFFF7A2F),
    amberInk: Color(0xFFD1591A),
    amberTint: Color(0x21FF7A2F), // rgba(255,122,47,0.13)
    success: WanesColors.success,
    alert: WanesColors.alert,
    info: WanesColors.info,
    warning: WanesColors.warning,
    successTint: Color(0x1C12A877), // rgba(18,168,119,0.11)
    alertTint: Color(0x1CE04B4F), // rgba(224,75,79,0.11)
    infoTint: Color(0x1C3F7FE6), // rgba(63,127,230,0.11)
    warningTint: Color(0x1FE0A326), // rgba(224,163,38,0.12)
    shadow: Color(0x240E1726), // rgba(14,23,38,0.14)
    mapBg: Color(0xFFE6ECF3),
    mapLine: Color(0xFFD4DDE7),
    mapRoad: Color(0xFFC4CFDC),
    onTeal: Color(0xFF04241F),
  );

  static const dark = WanesTokens(
    bg: Color(0xFF0E1726),
    surface: Color(0xFF172336),
    surface2: Color(0xFF1E2C42),
    ink: Color(0xFFEAF0F7),
    ink2: Color(0xFF93A2BA),
    border: Color(0xFF26344C),
    teal: Color(0xFF16C6B2),
    tealInk: Color(0xFF63E9D9),
    tealTint: Color(0x2916C6B2), // rgba(22,198,178,0.16)
    amber: Color(0xFFFF8A45),
    amberInk: Color(0xFFFFB488),
    amberTint: Color(0x29FF8A45), // rgba(255,138,69,0.16)
    success: WanesColors.successDark,
    alert: WanesColors.alertDark,
    info: WanesColors.infoDark,
    warning: WanesColors.warningDark,
    successTint: Color(0x292BC088), // rgba(43,192,136,0.16)
    alertTint: Color(0x29F2686C), // rgba(242,104,108,0.16)
    infoTint: Color(0x295E97F0), // rgba(94,151,240,0.16)
    warningTint: Color(0x29F0B647), // rgba(240,182,71,0.16)
    shadow: Color(0x80000000), // rgba(0,0,0,0.5)
    mapBg: Color(0xFF101D31),
    mapLine: Color(0xFF1B2A41),
    mapRoad: Color(0xFF243651),
    onTeal: Color(0xFF04241F),
  );

  /// The light palette re-skinned to an admin-configured [primary].
  ///
  /// Only the teal family moves — surfaces, ink and the functional colours are
  /// the design's and stay put. The derived shades mirror the CMS
  /// (`brand-color.ts`) so both clients land on the same colours for a given
  /// brand: `ink` is the same hue darkened for text on a light background, and
  /// `tint` is the fill at 12% so it reads as a wash rather than a second colour.
  static WanesTokens lightFor(Color primary) {
    final hsl = HSLColor.fromColor(primary);
    return light.copyWith(
      teal: primary,
      tealInk: hsl.withLightness((hsl.lightness * 0.75).clamp(0.0, 1.0)).toColor(),
      tealTint: primary.withValues(alpha: 0.12),
      onTeal: _onBrand(primary, hsl),
    );
  }

  /// The dark palette re-skinned to [primary].
  ///
  /// The configured colour is lifted to a lightness floor first: a brand that
  /// works on white can sit too close to the dark surface to be seen, and
  /// `ink` goes *lighter* still, because on dark it is text rather than a fill.
  static WanesTokens darkFor(Color primary) {
    final hsl = HSLColor.fromColor(primary);
    final lifted = hsl.withLightness(hsl.lightness.clamp(0.46, 1.0));
    final brand = lifted.toColor();
    return dark.copyWith(
      teal: brand,
      tealInk: lifted.withLightness(lifted.lightness.clamp(0.65, 1.0)).toColor(),
      tealTint: brand.withValues(alpha: 0.16),
      onTeal: _onBrand(brand, lifted),
    );
  }

  /// Foreground for content on a solid brand fill: near-black or near-white,
  /// whichever wins on contrast, tinted with the brand hue so it does not read
  /// as a foreign colour. 0.179 is where black overtakes white on WCAG contrast.
  static Color _onBrand(Color fill, HSLColor hsl) =>
      fill.computeLuminance() > 0.179
          ? hsl.withSaturation(hsl.saturation.clamp(0.0, 0.85)).withLightness(0.08).toColor()
          : hsl.withSaturation(hsl.saturation.clamp(0.0, 0.35)).withLightness(0.97).toColor();

  static WanesTokens of(BuildContext context) =>
      Theme.of(context).extension<WanesTokens>() ?? light;

  /// The colour a booking status is shown in — one table, so the rider's list,
  /// their booking details and the driver's rider manifest never disagree about
  /// what a seat looks like. Takes the server's `BookingStatus`:
  /// 1 Pending · 2 Confirmed · 3 InProgress · 4 Completed · 5 Cancelled.
  Color bookingStatus(int status) => switch (status) {
        1 => amber,   // waiting on something
        3 => teal,    // under way
        4 => info,    // completed — the same blue trips use when they finish
        5 => alert,   // cancelled
        _ => success, // confirmed
      };

  /// Same idea for a trip's own status:
  /// 1 Posted · 2 Full · 3 Active · 4 Completed · 5 Cancelled · 6 Arrived.
  Color tripStatus(int status) => switch (status) {
        3 || 6 => teal, // arrived / under way
        4 => info,      // completed
        5 => alert,     // cancelled
        _ => amber,     // posted or full
      };

  @override
  WanesTokens copyWith({
    Color? bg,
    Color? surface,
    Color? surface2,
    Color? ink,
    Color? ink2,
    Color? border,
    Color? teal,
    Color? tealInk,
    Color? tealTint,
    Color? amber,
    Color? amberInk,
    Color? amberTint,
    Color? success,
    Color? alert,
    Color? info,
    Color? warning,
    Color? successTint,
    Color? alertTint,
    Color? infoTint,
    Color? warningTint,
    Color? shadow,
    Color? mapBg,
    Color? mapLine,
    Color? mapRoad,
    Color? onTeal,
  }) =>
      WanesTokens(
        bg: bg ?? this.bg,
        surface: surface ?? this.surface,
        surface2: surface2 ?? this.surface2,
        ink: ink ?? this.ink,
        ink2: ink2 ?? this.ink2,
        border: border ?? this.border,
        teal: teal ?? this.teal,
        tealInk: tealInk ?? this.tealInk,
        tealTint: tealTint ?? this.tealTint,
        amber: amber ?? this.amber,
        amberInk: amberInk ?? this.amberInk,
        amberTint: amberTint ?? this.amberTint,
        success: success ?? this.success,
        alert: alert ?? this.alert,
        info: info ?? this.info,
        warning: warning ?? this.warning,
        successTint: successTint ?? this.successTint,
        alertTint: alertTint ?? this.alertTint,
        infoTint: infoTint ?? this.infoTint,
        warningTint: warningTint ?? this.warningTint,
        shadow: shadow ?? this.shadow,
        mapBg: mapBg ?? this.mapBg,
        mapLine: mapLine ?? this.mapLine,
        mapRoad: mapRoad ?? this.mapRoad,
        onTeal: onTeal ?? this.onTeal,
      );

  @override
  WanesTokens lerp(ThemeExtension<WanesTokens>? other, double t) {
    if (other is! WanesTokens) return this;
    Color c(Color a, Color b) => Color.lerp(a, b, t)!;
    return WanesTokens(
      bg: c(bg, other.bg),
      surface: c(surface, other.surface),
      surface2: c(surface2, other.surface2),
      ink: c(ink, other.ink),
      ink2: c(ink2, other.ink2),
      border: c(border, other.border),
      teal: c(teal, other.teal),
      tealInk: c(tealInk, other.tealInk),
      tealTint: c(tealTint, other.tealTint),
      amber: c(amber, other.amber),
      amberInk: c(amberInk, other.amberInk),
      amberTint: c(amberTint, other.amberTint),
      success: c(success, other.success),
      alert: c(alert, other.alert),
      info: c(info, other.info),
      warning: c(warning, other.warning),
      successTint: c(successTint, other.successTint),
      alertTint: c(alertTint, other.alertTint),
      infoTint: c(infoTint, other.infoTint),
      warningTint: c(warningTint, other.warningTint),
      shadow: c(shadow, other.shadow),
      mapBg: c(mapBg, other.mapBg),
      mapLine: c(mapLine, other.mapLine),
      mapRoad: c(mapRoad, other.mapRoad),
      onTeal: c(onTeal, other.onTeal),
    );
  }
}

/// Material theme wired to the design tokens + typography
/// (Plus Jakarta Sans display, JetBrains Mono for labels/data).
class WanesTheme {
  /// True while the app is showing Arabic. Plus Jakarta Sans and JetBrains
  /// Mono carry no Arabic glyphs, so the type stack swaps to Cairo (display /
  /// body) and IBM Plex Sans Arabic (the mono/data role) for `ar`. Set from
  /// [WanesApp] whenever the locale changes, before the themes are rebuilt.
  static bool arabic = false;

  static ThemeData light() => _base(Brightness.light);
  static ThemeData dark() => _base(Brightness.dark);

  /// The prototype's uppercase micro-labels and numeric/data type — JetBrains
  /// Mono in English, IBM Plex Sans Arabic in Arabic. Defaults mirror the
  /// design (tracked, semibold).
  static TextStyle mono({
    double size = 12,
    FontWeight weight = FontWeight.w600,
    Color? color,
    double spacing = 0.1,
  }) =>
      arabic
          ? GoogleFonts.ibmPlexSansArabic(
              fontSize: size,
              fontWeight: weight,
              color: color,
              // Arabic is a joined script — letter-spacing breaks the joins.
              letterSpacing: 0,
            )
          : GoogleFonts.jetBrainsMono(
              fontSize: size,
              fontWeight: weight,
              color: color,
              letterSpacing: spacing,
            );

  /// Display/body face for the current language.
  static TextStyle display({
    double? size,
    FontWeight? weight,
    Color? color,
    double? spacing,
  }) =>
      arabic
          ? GoogleFonts.cairo(
              fontSize: size, fontWeight: weight, color: color, letterSpacing: 0)
          : GoogleFonts.plusJakartaSans(
              fontSize: size, fontWeight: weight, color: color, letterSpacing: spacing);

  /// Display face for a *named* language rather than the active one — the
  /// language picker has to draw each language's name in its own script.
  static TextStyle displayFor(
    String languageCode, {
    double? size,
    FontWeight? weight,
    Color? color,
  }) =>
      languageCode == 'ar'
          ? GoogleFonts.cairo(fontSize: size, fontWeight: weight, color: color)
          : GoogleFonts.plusJakartaSans(fontSize: size, fontWeight: weight, color: color);

  static ThemeData _base(Brightness brightness) {
    final isDark = brightness == Brightness.dark;
    // The brand colour is admin-controlled, so the palette is derived per build
    // rather than taken from the const design tokens. `WanesApp` rebuilds the
    // themes whenever the configuration changes.
    final primary = AppConfigController.primary;
    final t = isDark ? WanesTokens.darkFor(primary) : WanesTokens.lightFor(primary);

    final scheme = ColorScheme.fromSeed(
      seedColor: t.teal,
      brightness: brightness,
    ).copyWith(
      primary: t.teal,
      onPrimary: t.onTeal,
      secondary: t.amber,
      surface: t.surface,
      onSurface: t.ink,
      error: t.alert,
      outline: t.border,
    );

    final base = ThemeData(useMaterial3: true, brightness: brightness, colorScheme: scheme);

    return base.copyWith(
      extensions: [t],
      textTheme: (arabic
              ? GoogleFonts.cairoTextTheme(base.textTheme)
              : GoogleFonts.plusJakartaSansTextTheme(base.textTheme))
          .apply(bodyColor: t.ink, displayColor: t.ink),
      scaffoldBackgroundColor: t.bg,
      hintColor: t.ink2,
      dividerColor: t.border,
      dividerTheme: DividerThemeData(color: t.border, thickness: 1, space: 1),
      appBarTheme: AppBarTheme(
        centerTitle: false,
        elevation: 0,
        scrolledUnderElevation: 0,
        backgroundColor: t.bg,
        foregroundColor: t.ink,
        titleTextStyle: display(
          size: 19,
          weight: FontWeight.w800,
          spacing: -0.3,
          color: t.ink,
        ),
      ),
      cardTheme: CardThemeData(
        elevation: 0,
        color: t.surface,
        surfaceTintColor: Colors.transparent,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(18),
          side: BorderSide(color: t.border),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          backgroundColor: t.teal,
          foregroundColor: t.onTeal,
          disabledBackgroundColor: t.teal.withValues(alpha: .4),
          disabledForegroundColor: t.onTeal.withValues(alpha: .6),
          elevation: 0,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
          textStyle: display(weight: FontWeight.w800, size: 16),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          foregroundColor: t.ink,
          side: BorderSide(color: t.border),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
          textStyle: display(weight: FontWeight.w700, size: 15),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: t.tealInk),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: t.surface2,
        hintStyle: TextStyle(color: t.ink2),
        labelStyle: TextStyle(color: t.ink2),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide(color: t.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide(color: t.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide(color: t.teal, width: 1.6),
        ),
      ),
    );
  }
}

/// Light/dark selection, driven by the sun/moon button on the profile screens
/// (prototype 12).
///
/// The choice is an account preference — [AppTheme] on the user profile — so it
/// follows the user to a new device. It is also mirrored into
/// shared_preferences so a cold start paints the right theme before the profile
/// call comes back, and so a signed-out device keeps the choice.
class ThemeController {
  ThemeController._();

  static const _key = '${Environment.appName}_theme';

  static final ValueNotifier<ThemeMode> mode = ValueNotifier(ThemeMode.system);

  static AppTheme value = AppTheme.system;

  /// Pushes the choice onto the account. Wired to `AuthService` in `main`, so
  /// this file stays clear of the service layer (which depends on core).
  static Future<void> Function(AppTheme theme)? saveToAccount;

  /// Restores the last choice from disk. Runs before `runApp`, so there is no
  /// flash of the wrong theme. The cached [profile] wins over the local mirror,
  /// since that is what the server last confirmed.
  static Future<void> load({Profile? profile}) async {
    final prefs = await SharedPreferences.getInstance();
    _apply(profile?.theme ?? AppTheme.fromValue(prefs.getInt(_key)));
  }

  /// Adopts the theme carried on a freshly fetched profile — sign-in on a new
  /// device, or a change made on another one. Local only: the server is already
  /// the source of this value.
  static Future<void> applyFromProfile(Profile? profile) async {
    if (profile == null || profile.theme == value) return;
    _apply(profile.theme);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setInt(_key, profile.theme.value);
  }

  /// Flips between light and dark, resolving "system" against what is on screen.
  static Future<void> toggle(BuildContext context) {
    final dark = value == AppTheme.system
        ? Theme.of(context).brightness == Brightness.dark
        : value == AppTheme.dark;
    return set(dark ? AppTheme.light : AppTheme.dark);
  }

  /// Repaints immediately, then persists — locally always, and on the account
  /// through [saveToAccount]. A failed sync is not surfaced: the theme is not
  /// worth an error toast, and the local mirror still holds the choice.
  static Future<void> set(AppTheme next) async {
    if (next == value) return;
    _apply(next);

    final prefs = await SharedPreferences.getInstance();
    await prefs.setInt(_key, next.value);

    await saveToAccount?.call(next);
  }

  static void _apply(AppTheme next) {
    value = next;
    mode.value = switch (next) {
      AppTheme.system => ThemeMode.system,
      AppTheme.light => ThemeMode.light,
      AppTheme.dark => ThemeMode.dark,
    };
  }
}
