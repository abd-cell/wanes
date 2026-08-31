import 'dart:async';

import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/session.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import 'wanes_ui.dart';

/// The "Language" row that sits in the account group on both profile screens.
/// Its subtitle is the language currently in use, written in that language.
class LanguageRow extends StatelessWidget {
  const LanguageRow({super.key});

  @override
  Widget build(BuildContext context) {
    return GroupedRow(
      icon: Icons.language_rounded,
      title: context.tr('language.title'),
      subtitle: LocaleController.nameOf(context.l10n.locale),
      onTap: () => showLanguagePicker(context),
    );
  }
}

/// Bottom sheet listing the supported languages. Picking one writes the choice
/// through [LocaleController], which rebuilds the app in the new language and
/// text direction — there is nothing to restart.
Future<void> showLanguagePicker(BuildContext context) {
  final t = WanesTokens.of(context);
  return showModalBottomSheet<void>(
    context: context,
    backgroundColor: t.surface,
    showDragHandle: true,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
    ),
    builder: (sheetContext) {
      final current = sheetContext.l10n.locale.languageCode;
      return SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 18),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                sheetContext.tr('language.title'),
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: t.ink),
              ),
              const SizedBox(height: 4),
              Text(
                sheetContext.tr('language.subtitle'),
                style: TextStyle(fontSize: 12.5, height: 1.45, color: t.ink2),
              ),
              const SizedBox(height: 14),
              WanesCard(
                padding: EdgeInsets.zero,
                shadow: false,
                child: Column(
                  children: [
                    for (var i = 0; i < AppLocalizations.supportedLocales.length; i++) ...[
                      if (i > 0) Divider(height: 1, thickness: 1, color: t.border),
                      _LanguageOption(
                        locale: AppLocalizations.supportedLocales[i],
                        selected:
                            AppLocalizations.supportedLocales[i].languageCode == current,
                        onTap: () async {
                          final picked = AppLocalizations.supportedLocales[i];
                          await LocaleController.set(picked);
                          // Push payloads are built server-side and can only
                          // carry one language, so the account has to be told.
                          // Fire-and-forget: the UI has already switched, and a
                          // failed sync self-corrects at the next sign-in.
                          if (Session.instance.isLoggedIn) {
                            unawaited(AuthService().updatePreferences(
                                language:
                                    AppLanguage.fromLanguageCode(picked.languageCode)));
                          }
                          if (sheetContext.mounted) Navigator.pop(sheetContext);
                        },
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
      );
    },
  );
}

/// One language: its own name in its own script, the English/Arabic gloss
/// underneath, and a tick on the active one.
class _LanguageOption extends StatelessWidget {
  const _LanguageOption({
    required this.locale,
    required this.selected,
    required this.onTap,
  });

  final Locale locale;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final code = locale.languageCode;
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        child: Row(children: [
          Container(
            width: 34,
            height: 34,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: selected ? t.tealTint : t.surface2,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Text(
              code.toUpperCase(),
              style: WanesTheme.mono(
                  size: 11, weight: FontWeight.w800, color: selected ? t.tealInk : t.ink2),
            ),
          ),
          const SizedBox(width: 13),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              // Each name is drawn in a face that carries its own script, so
              // Arabic stays legible while the app is still in English.
              Text(
                LocaleController.nameOf(locale),
                textDirection: code == 'ar' ? TextDirection.rtl : TextDirection.ltr,
                style: WanesTheme.displayFor(code,
                    size: 15, weight: FontWeight.w700, color: t.ink),
              ),
              const SizedBox(height: 1),
              Text(
                context.tr('language.$code'),
                style: TextStyle(fontSize: 12, color: t.ink2),
              ),
            ]),
          ),
          const SizedBox(width: 10),
          if (selected) Icon(Icons.check_rounded, size: 19, color: t.tealInk),
        ]),
      ),
    );
  }
}
