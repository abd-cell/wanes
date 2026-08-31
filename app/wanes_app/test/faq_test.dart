import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/faq_screen.dart';
import 'package:wanes_app/models/models.dart';

/// The help centre: how the `GET /faq` payload is read, how entries are grouped
/// and localised, and what the screen renders.
///
/// The default test binding answers every request with 400 offline, so the
/// [FaqScreen] tests cover the failure path. The populated list is covered by
/// exercising the grouping and [FaqTile] directly, which needs no server —
/// pumping a real fetch would not work anyway, since `pumpAndSettle` waits on
/// animations, not network I/O.
void main() {
  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  Widget host() => MaterialApp(
        theme: WanesTheme.light(),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: const FaqScreen(),
      );

  /// One entry per category, deliberately out of order so the grouping has
  /// something to actually sort.
  Faq sample() => Faq.fromJson(const {
        'items': [
          {
            'id': 3,
            'category': 3,
            'questionEn': 'How do I start driving?',
            'questionAr': 'كيف أبدأ القيادة؟',
            'answerEn': 'Apply from your profile and add a vehicle.',
            'answerAr': 'قدّم طلبًا من حسابك وأضف مركبة.',
          },
          {
            'id': 1,
            'category': 1,
            'questionEn': 'What is Wanes?',
            'questionAr': 'ما هو ونس؟',
            'answerEn': 'Wanes matches riders with drivers.',
            'answerAr': 'يربط ونس الركاب بالسائقين.',
          },
          {
            'id': 2,
            'category': 1,
            'questionEn': 'Does Wanes handle payment?',
            'questionAr': 'هل يتولى ونس الدفع؟',
            'answerEn': 'Not yet.',
            'answerAr': 'ليس بعد.',
          },
        ],
      });

  group('the payload', () {
    test('reads the entries the API sends', () {
      final faq = sample();
      expect(faq.items, hasLength(3));
      expect(faq.items.first.id, 3);
      expect(faq.items.first.category, FaqCategory.driving);
      expect(faq.items.first.questionEn, 'How do I start driving?');
    });

    test('an empty or absent list is not an error', () {
      expect(Faq.fromJson(const {}).items, isEmpty);
      expect(Faq.fromJson(const {'items': []}).items, isEmpty);
    });

    test('a missing string degrades to empty rather than throwing', () {
      final item = FaqItem.fromJson(const {'id': 9, 'category': 2});
      expect(item.questionEn, '');
      expect(item.answerAr, '');
      expect(item.category, FaqCategory.riding);
    });

    test('an unknown category falls back to General', () {
      expect(FaqCategory.fromValue(99), FaqCategory.general);
      expect(FaqCategory.fromValue(null), FaqCategory.general);
      expect(FaqCategory.fromValue(5), FaqCategory.safety);
    });
  });

  group('grouping', () {
    test('groups by category in enum order, not payload order', () {
      final grouped = sample().byCategory;
      expect(grouped.keys.toList(), [FaqCategory.general, FaqCategory.driving]);
    });

    test('keeps the order the server sent within a category', () {
      final general = sample().byCategory[FaqCategory.general]!;
      expect(general.map((i) => i.id), [1, 2]);
    });

    test('skips categories with nothing in them', () {
      final grouped = sample().byCategory;
      expect(grouped.containsKey(FaqCategory.riding), isFalse);
      expect(grouped.containsKey(FaqCategory.safety), isFalse);
    });

    test('an empty FAQ groups to nothing', () {
      expect(Faq.empty.byCategory, isEmpty);
    });
  });

  group('language selection', () {
    const bilingual = FaqItem(
      id: 1,
      category: FaqCategory.general,
      questionEn: 'What is Wanes?',
      questionAr: 'ما هو ونس؟',
      answerEn: 'A ride-matching app.',
      answerAr: 'تطبيق لمطابقة الرحلات.',
    );

    test('English locale reads the English half', () {
      AppLocalizations.current = const AppLocalizations(Locale('en'));
      expect(bilingual.question, 'What is Wanes?');
      expect(bilingual.answer, 'A ride-matching app.');
    });

    test('Arabic locale reads the Arabic half', () {
      AppLocalizations.current = const AppLocalizations(Locale('ar'));
      expect(bilingual.question, 'ما هو ونس؟');
      expect(bilingual.answer, 'تطبيق لمطابقة الرحلات.');
    });

    test('a half-translated entry falls back to the language it does have', () {
      const onlyEnglish = FaqItem(
        id: 2,
        category: FaqCategory.general,
        questionEn: 'Only English',
        questionAr: '   ',
        answerEn: 'Answer.',
        answerAr: '',
      );

      AppLocalizations.current = const AppLocalizations(Locale('ar'));
      expect(onlyEnglish.question, 'Only English');
      expect(onlyEnglish.answer, 'Answer.');
    });
  });

  group('the screen, offline', () {
    testWidgets('shows the failure state with a way to retry', (tester) async {
      await tester.pumpWidget(host());
      await tester.pumpAndSettle();

      // The title is always there; the body is the part that failed.
      expect(find.text('Help & FAQ'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
      expect(find.byIcon(Icons.cloud_off_rounded), findsOneWidget);
    });

    testWidgets('renders in Arabic without falling over', (tester) async {
      AppLocalizations.current = const AppLocalizations(Locale('ar'));
      await tester.pumpWidget(MaterialApp(
        theme: WanesTheme.light(),
        locale: const Locale('ar'),
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: const FaqScreen(),
      ));
      await tester.pumpAndSettle();

      expect(find.text('المساعدة والأسئلة الشائعة'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  });

  group('a question card', () {
    const item = FaqItem(
      id: 1,
      category: FaqCategory.general,
      questionEn: 'What is Wanes?',
      questionAr: 'ما هو ونس؟',
      answerEn: 'Wanes matches riders with drivers going the same way.',
      answerAr: 'يربط ونس الركاب بالسائقين المتوجهين إلى الطريق نفسه.',
    );

    Widget tileHost({required bool isOpen, VoidCallback? onTap, Locale? locale}) =>
        MaterialApp(
          theme: WanesTheme.light(),
          locale: locale,
          supportedLocales: AppLocalizations.supportedLocales,
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          home: Scaffold(
            body: FaqTile(item: item, isOpen: isOpen, onTap: onTap ?? () {}),
          ),
        );

    testWidgets('collapsed shows the question and withholds the answer',
        (tester) async {
      await tester.pumpWidget(tileHost(isOpen: false));
      await tester.pumpAndSettle();

      expect(find.text('What is Wanes?'), findsOneWidget);
      expect(find.textContaining('matches riders'), findsNothing);
      expect(tester.takeException(), isNull);
    });

    testWidgets('open shows the answer', (tester) async {
      await tester.pumpWidget(tileHost(isOpen: true));
      await tester.pumpAndSettle();

      expect(find.text('What is Wanes?'), findsOneWidget);
      expect(find.textContaining('matches riders'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('a tap anywhere on the card reports back', (tester) async {
      var taps = 0;
      await tester.pumpWidget(tileHost(isOpen: false, onTap: () => taps++));
      await tester.pumpAndSettle();

      await tester.tap(find.byType(FaqTile));
      expect(taps, 1);
    });

    testWidgets('renders the Arabic half under an Arabic locale', (tester) async {
      AppLocalizations.current = const AppLocalizations(Locale('ar'));
      await tester.pumpWidget(
          tileHost(isOpen: true, locale: const Locale('ar')));
      await tester.pumpAndSettle();

      expect(find.text('ما هو ونس؟'), findsOneWidget);
      expect(find.textContaining('يربط ونس الركاب'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('a collapsed card still spans the full width', (tester) async {
      await tester.pumpWidget(tileHost(isOpen: false));
      await tester.pumpAndSettle();

      // The empty box in the collapsed branch is what holds the card open;
      // without it the card would shrink to the width of its question.
      final card = tester.getSize(find.byType(FaqTile));
      expect(card.width, 800); // the default test surface
    });
  });
}
