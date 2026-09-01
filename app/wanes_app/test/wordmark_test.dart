import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:wanes_app/core/theme.dart';
import 'package:wanes_app/widgets/wanes_logo.dart';

/// The app-name lockup. What can actually go wrong here is bidi: the three
/// runs are Latin, Arabic and Arabic, so a single paragraph — or an RTL host —
/// will happily reorder them and park the `ع` on the far side of the Arabic
/// word. These tests pin the visual order in both directions.
void main() {
  Widget host(Widget child, {TextDirection direction = TextDirection.ltr}) =>
      MaterialApp(
        theme: WanesTheme.light(),
        home: Directionality(
          textDirection: direction,
          child: Scaffold(body: Center(child: child)),
        ),
      );

  /// Left edge of a run, in screen coordinates.
  double left(WidgetTester tester, String text) =>
      tester.getTopLeft(find.text(text)).dx;

  for (final direction in TextDirection.values) {
    testWidgets('reads "Wanees ع الطريق" left-to-right in a $direction host',
        (tester) async {
      await tester.pumpWidget(
          host(const WanesWordmark(size: 32), direction: direction));

      expect(tester.takeException(), isNull);
      expect(find.text('Wanees'), findsOneWidget);
      expect(find.text('ع'), findsOneWidget);
      expect(find.text('الطريق'), findsOneWidget);

      expect(left(tester, 'Wanees'), lessThan(left(tester, 'ع')));
      expect(left(tester, 'ع'), lessThan(left(tester, 'الطريق')));
    });
  }

  testWidgets('the joining glyph takes the amber accent', (tester) async {
    await tester.pumpWidget(host(const WanesWordmark(size: 32)));

    final glyph = tester.widget<Text>(find.text('ع'));
    expect(glyph.style?.color, WanesTokens.light.amber);
  });

  testWidgets('fits beside the badge on a narrow phone', (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    // Same gutters the login and complete-profile screens use.
    await tester.pumpWidget(host(const Padding(
      padding: EdgeInsets.symmetric(horizontal: 24),
      child: WanesLogo(size: 46, showWordmark: true, horizontal: true),
    )));

    // No RenderFlex overflow, and the last run stays inside the gutter — the
    // lockup scales itself down instead of running off the edge. The test font
    // (Ahem, one full em square per glyph) is far wider than the real faces,
    // so this is the pessimistic case.
    expect(tester.takeException(), isNull);
    expect(tester.getBottomRight(find.text('الطريق')).dx,
        lessThanOrEqualTo(375 - 24));
  });

  /// The plain-text label (launcher icon, task switcher, browser tab) is a
  /// single paragraph, so it cannot lean on separate widgets for its order —
  /// it carries bidi control marks instead. These lay it out with a real
  /// [TextPainter] and check where the glyphs actually land.
  group('kWanesAppName', () {
    Rect boxOf(TextPainter painter, int start, int end) => painter
        .getBoxesForSelection(
            TextSelection(baseOffset: start, extentOffset: end))
        .map((b) => b.toRect())
        .reduce((a, b) => a.expandToInclude(b));

    for (final direction in TextDirection.values) {
      test('renders "Wanees ع الطريق" in that order in a $direction paragraph',
          () {
        final painter = TextPainter(
          text: const TextSpan(text: kWanesAppName),
          textDirection: direction,
        )..layout();

        final latin = kWanesAppName.indexOf(kWanesNameLatin);
        final joiner = kWanesAppName.indexOf(kWanesNameJoiner);
        final arabic = kWanesAppName.indexOf(kWanesNameArabic);

        final latinBox =
            boxOf(painter, latin, latin + kWanesNameLatin.length);
        final joinerBox = boxOf(painter, joiner, joiner + 1);
        final arabicBox =
            boxOf(painter, arabic, arabic + kWanesNameArabic.length);

        expect(latinBox.right, lessThanOrEqualTo(joinerBox.left),
            reason: 'the ع must follow Wanees');
        expect(joinerBox.right, lessThanOrEqualTo(arabicBox.left),
            reason: 'the ع must sit before الطريق, not past it');
      });
    }

    test('is the three runs plus only invisible marks', () {
      final visible =
          kWanesAppName.replaceAll(RegExp('[\u200E\u2066\u2069]'), '');
      expect(visible,
          '$kWanesNameLatin $kWanesNameJoiner $kWanesNameArabic');
    });
  });
}
