import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/l10n.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/features/feedback_screen.dart';
import 'package:wanes_app/models/models.dart';

/// Complaints and suggestions: how the `GET /feedback` payload reads, what the
/// list shows, and when the compose form will let itself be sent.
///
/// The test binding answers every request with 400 offline, so the screen tests
/// cover the failure path and the parts that need no server — the two entry
/// cards and the compose form. A populated list is exercised through
/// [FeedbackTile] directly, the same split the FAQ tests use.
void main() {
  setUp(() => AppLocalizations.current = const AppLocalizations(Locale('en')));

  Widget host(Widget screen, {Locale locale = const Locale('en')}) => MaterialApp(
        theme: WanesTheme.light(),
        locale: locale,
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        home: screen,
      );

  FeedbackEntry answered() => FeedbackEntry.fromJson(const {
        'id': 7,
        'kind': 1,
        'status': 3,
        'subject': 'Driver took a long detour',
        'message': 'We went the wrong way for twenty minutes.',
        'reply': 'Sorry about that. We have spoken to the driver.',
        'repliedAt': '2026-09-05T10:00:00Z',
        'creationDate': '2026-09-04T09:00:00Z',
      });

  group('the payload', () {
    test('reads a submission the API sends', () {
      final entry = answered();
      expect(entry.id, 7);
      expect(entry.kind, FeedbackKind.complaint);
      expect(entry.status, FeedbackStatus.resolved);
      expect(entry.subject, 'Driver took a long detour');
      expect(entry.hasReply, isTrue);
      expect(entry.repliedAt, isNotNull);
    });

    test('an unanswered submission has no reply', () {
      final entry = FeedbackEntry.fromJson(const {
        'id': 8,
        'kind': 2,
        'status': 1,
        'subject': 'Let me save a favourite driver',
        'message': 'I would like to ride with the same driver again.',
      });

      expect(entry.kind, FeedbackKind.suggestion);
      expect(entry.status, FeedbackStatus.isNew);
      expect(entry.hasReply, isFalse);
      expect(entry.reply, isNull);
    });

    /// A reply of pure whitespace is the desk having saved an empty box, not an
    /// answer — the card must not sprout an empty reply panel for it.
    test('a whitespace-only reply does not count as answered', () {
      final entry = FeedbackEntry.fromJson(const {
        'id': 9,
        'kind': 1,
        'status': 2,
        'subject': 'S',
        'message': 'M',
        'reply': '   ',
      });

      expect(entry.hasReply, isFalse);
    });

    test('a missing string degrades to empty rather than throwing', () {
      final entry = FeedbackEntry.fromJson(const {'id': 10});
      expect(entry.subject, '');
      expect(entry.message, '');
      expect(entry.kind, FeedbackKind.complaint);
      expect(entry.status, FeedbackStatus.isNew);
    });

    test('unknown enum values fall back rather than throwing', () {
      expect(FeedbackKind.fromValue(99), FeedbackKind.complaint);
      expect(FeedbackKind.fromValue(null), FeedbackKind.complaint);
      expect(FeedbackStatus.fromValue(99), FeedbackStatus.isNew);
      expect(FeedbackStatus.fromValue(4), FeedbackStatus.dismissed);
    });

    test('only New and InReview are still open', () {
      expect(FeedbackStatus.isNew.isOpen, isTrue);
      expect(FeedbackStatus.inReview.isOpen, isTrue);
      expect(FeedbackStatus.resolved.isOpen, isFalse);
      expect(FeedbackStatus.dismissed.isOpen, isFalse);
    });
  });

  group('a submission card', () {
    testWidgets('collapsed shows the subject and status, not the body', (tester) async {
      await tester.pumpWidget(host(Scaffold(
        body: FeedbackTile(entry: answered(), isOpen: false, onTap: () {}),
      )));
      await tester.pumpAndSettle();

      expect(find.text('Driver took a long detour'), findsOneWidget);
      expect(find.text('Resolved'), findsOneWidget);
      expect(find.text('We went the wrong way for twenty minutes.'), findsNothing);
    });

    testWidgets('open shows the message and the reply', (tester) async {
      await tester.pumpWidget(host(Scaffold(
        body: FeedbackTile(entry: answered(), isOpen: true, onTap: () {}),
      )));
      await tester.pumpAndSettle();

      expect(find.text('We went the wrong way for twenty minutes.'), findsOneWidget);
      expect(find.text('Sorry about that. We have spoken to the driver.'), findsOneWidget);
      expect(find.text('SUPPORT REPLIED'), findsOneWidget);
    });

    testWidgets('an unanswered card has no reply panel when open', (tester) async {
      final waiting = FeedbackEntry.fromJson(const {
        'id': 8,
        'kind': 2,
        'status': 1,
        'subject': 'An idea',
        'message': 'Here it is.',
      });

      await tester.pumpWidget(host(Scaffold(
        body: FeedbackTile(entry: waiting, isOpen: true, onTap: () {}),
      )));
      await tester.pumpAndSettle();

      expect(find.text('Here it is.'), findsOneWidget);
      expect(find.text('SUPPORT REPLIED'), findsNothing);
    });

    testWidgets('renders in Arabic without falling over', (tester) async {
      AppLocalizations.current = const AppLocalizations(Locale('ar'));

      await tester.pumpWidget(host(
        Scaffold(body: FeedbackTile(entry: answered(), isOpen: true, onTap: () {})),
        locale: const Locale('ar'),
      ));
      await tester.pumpAndSettle();

      expect(find.text('تمت المعالجة'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  });

  group('the list, offline', () {
    testWidgets('offers both ways in and reports the load failure', (tester) async {
      await tester.pumpWidget(host(const FeedbackScreen()));
      await tester.pumpAndSettle();

      // Each kind appears once on its entry card.
      expect(find.text('Complaint'), findsOneWidget);
      expect(find.text('Suggestion'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
      expect(find.byIcon(Icons.cloud_off_rounded), findsOneWidget);
    });

    testWidgets('tapping a kind opens the compose form for it', (tester) async {
      await tester.pumpWidget(host(const FeedbackScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Suggestion'));
      await tester.pumpAndSettle();

      expect(find.text('What would you change?'), findsOneWidget);
      expect(find.text('Send'), findsOneWidget);
    });
  });

  group('the compose form', () {
    /// The server enforces the same minimums. Refusing early means a too-short
    /// note costs no round trip, and the button says so instead of failing on
    /// tap.
    testWidgets('will not send until the subject and details are long enough',
        (tester) async {
      await tester.pumpWidget(host(
        const FeedbackComposeScreen(kind: FeedbackKind.complaint),
      ));
      await tester.pumpAndSettle();

      final button = find.ancestor(
        of: find.text('Send'),
        matching: find.byType(GestureDetector),
      );
      expect(button, findsWidgets);

      // Nothing typed: tapping does nothing, so the form is still on screen.
      await tester.tap(find.text('Send'));
      await tester.pumpAndSettle();
      expect(find.text('DETAILS'), findsOneWidget);

      final fields = find.byType(TextField);
      await tester.enterText(fields.first, 'Ab');
      await tester.enterText(fields.last, 'Too short');
      await tester.pumpAndSettle();

      await tester.tap(find.text('Send'));
      await tester.pumpAndSettle();
      expect(find.text('DETAILS'), findsOneWidget);
    });

    testWidgets('a trip it opened from is shown as context', (tester) async {
      await tester.pumpWidget(host(
        const FeedbackComposeScreen(kind: FeedbackKind.complaint, tripId: 42),
      ));
      await tester.pumpAndSettle();

      expect(find.text('About trip #42'), findsOneWidget);
    });

    testWidgets('the suggestion form reads as a suggestion, not a complaint',
        (tester) async {
      await tester.pumpWidget(host(
        const FeedbackComposeScreen(kind: FeedbackKind.suggestion),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Suggestion'), findsOneWidget);
      expect(find.text('What would you change?'), findsOneWidget);
      expect(find.text('What went wrong?'), findsNothing);
    });
  });
}
