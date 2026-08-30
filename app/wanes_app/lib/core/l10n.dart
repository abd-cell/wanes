import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../l10n/strings_ar.dart';
import '../l10n/strings_en.dart';
import 'environment.dart';

/// ─────────────────────────────────────────────────────────────────────────
/// Wanes localisation — English (LTR) and Arabic (RTL).
///
/// Copy lives in `lib/l10n/strings_{en,ar}.dart` as flat `key → text` maps.
/// Look a string up with `context.tr('profile.title')`; interpolate with
/// `{placeholders}`:
///
///   context.tr('login.codeSentTo', {'phone': '+96279…'})
///
/// Counted nouns go through [AppLocalizations.plural], which resolves the
/// CLDR category ("one"/"other" in English, the full zero/one/two/few/many/
/// other set in Arabic) and appends it to the key.
/// ─────────────────────────────────────────────────────────────────────────
class AppLocalizations {
  const AppLocalizations(this.locale);

  final Locale locale;

  static const supportedLocales = <Locale>[Locale('en'), Locale('ar')];

  static const localizationsDelegates = <LocalizationsDelegate<dynamic>>[
    delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
  ];

  static const delegate = _AppLocalizationsDelegate();

  /// The locale currently on screen. Mirrors what [LocaleController] holds so
  /// non-widget code (error mapping, formatters) can translate without a
  /// [BuildContext].
  static AppLocalizations current = const AppLocalizations(Locale('en'));

  static AppLocalizations of(BuildContext context) =>
      Localizations.of<AppLocalizations>(context, AppLocalizations) ?? current;

  bool get isArabic => locale.languageCode == 'ar';

  /// Tag for `intl` formatters (`DateFormat`, `NumberFormat`).
  String get localeName => locale.languageCode;

  Map<String, String> get _table => isArabic ? arStrings : enStrings;

  /// Looks up [key], falling back to English and then to the key itself so a
  /// missing entry degrades to something readable instead of crashing.
  String t(String key, [Map<String, Object?>? args]) {
    var value = _table[key] ?? enStrings[key];
    assert(() {
      if (value == null) debugPrint('[l10n] missing key: $key');
      return true;
    }());
    value ??= key;
    if (args != null) {
      args.forEach((name, arg) {
        value = value!.replaceAll('{$name}', '${arg ?? ''}');
      });
    }
    return value!;
  }

  /// Counted form of [key]: looks for `key.<category>`, then `key.other`.
  /// `{count}` is always available to the copy.
  String plural(String key, int count, [Map<String, Object?>? args]) {
    final category = isArabic ? _arabicCategory(count) : (count == 1 ? 'one' : 'other');
    final merged = <String, Object?>{...?args, 'count': count};
    final table = _table;
    final resolved = table['$key.$category'] ??
        table['$key.other'] ??
        enStrings['$key.$category'] ??
        enStrings['$key.other'];
    if (resolved == null) return t('$key.other', merged);
    var value = resolved;
    merged.forEach((name, arg) => value = value.replaceAll('{$name}', '${arg ?? ''}'));
    return value;
  }

  /// CLDR plural categories for Arabic.
  static String _arabicCategory(int n) {
    if (n == 0) return 'zero';
    if (n == 1) return 'one';
    if (n == 2) return 'two';
    final mod100 = n % 100;
    if (mod100 >= 3 && mod100 <= 10) return 'few';
    if (mod100 >= 11 && mod100 <= 99) return 'many';
    return 'other';
  }
}

class _AppLocalizationsDelegate extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  bool isSupported(Locale locale) =>
      AppLocalizations.supportedLocales.any((l) => l.languageCode == locale.languageCode);

  @override
  Future<AppLocalizations> load(Locale locale) async {
    final resolved = AppLocalizations(Locale(locale.languageCode));
    AppLocalizations.current = resolved;
    return resolved;
  }

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

/// Shorthands so screens read `context.tr('home.title')`.
extension AppLocalizationsX on BuildContext {
  AppLocalizations get l10n => AppLocalizations.of(this);

  String tr(String key, [Map<String, Object?>? args]) => AppLocalizations.of(this).t(key, args);

  String trPlural(String key, int count, [Map<String, Object?>? args]) =>
      AppLocalizations.of(this).plural(key, count, args);

  bool get isRtl => Directionality.of(this) == TextDirection.rtl;
}

/// The user's language choice, persisted across launches and settable from the
/// profile screens. [MaterialApp] listens to [locale] and rebuilds the tree, so
/// a change takes effect everywhere at once (including the text direction).
class LocaleController {
  LocaleController._();

  static const _key = '${Environment.appName}_locale';

  static final ValueNotifier<Locale> locale = ValueNotifier(const Locale('en'));

  static Locale get value => locale.value;

  /// Restores the saved language. Falls back to the device language when the
  /// user has never chosen one, and to English when that isn't supported.
  static Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    final saved = prefs.getString(_key);
    final code = saved ??
        WidgetsBinding.instance.platformDispatcher.locale.languageCode;
    locale.value = AppLocalizations.supportedLocales
        .firstWhere((l) => l.languageCode == code, orElse: () => const Locale('en'));
    AppLocalizations.current = AppLocalizations(locale.value);
  }

  static Future<void> set(Locale next) async {
    if (next.languageCode == locale.value.languageCode) return;
    locale.value = Locale(next.languageCode);
    AppLocalizations.current = AppLocalizations(locale.value);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, next.languageCode);
  }

  /// Native name of a supported language, for the language picker.
  static String nameOf(Locale locale) =>
      locale.languageCode == 'ar' ? 'العربية' : 'English';
}
