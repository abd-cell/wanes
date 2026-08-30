import 'dart:async';

import 'package:flutter/material.dart';

import '../core/app_response.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import 'wanes_ui.dart';

/// ─────────────────────────────────────────────────────────────────────────
/// Feedback states — prototype screen 13 "Alerts & toasts".
///
/// Three surfaces, all taken 1:1 from the design:
///   • [WanesToast]       — the stacked cards with a 4px coloured left edge.
///   • [WanesErrorPanel]  — the full-screen error card (Dismiss / Retry).
///   • [WanesInlineAlert] — the pulsing-dot strip ("Payment declined …").
///
/// Call them through [WanesAlerts]; nothing in the app should reach for a
/// bare `SnackBar` any more.
/// ─────────────────────────────────────────────────────────────────────────

/// Severity of a toast — picks the colour, tint and glyph from the design.
enum WanesAlertKind { error, warning, success, info }

/// Resolved per-kind colours + the prototype's mono glyph.
class _KindStyle {
  const _KindStyle(this.color, this.tint, this.glyph);
  final Color color;
  final Color tint;
  final String glyph;
}

_KindStyle _styleFor(WanesAlertKind kind, WanesTokens t) => switch (kind) {
      WanesAlertKind.error => _KindStyle(t.alert, t.alertTint, '!'),
      WanesAlertKind.warning => _KindStyle(t.warning, t.warningTint, '△'),
      WanesAlertKind.success => _KindStyle(t.success, t.successTint, '✓'),
      WanesAlertKind.info => _KindStyle(t.info, t.infoTint, 'i'),
    };

/// One toast card:
/// `surface · 1px border · 4px left border in the kind colour · radius 14`,
/// a 30px tinted glyph chip, title + message, and a ✕ to dismiss.
class WanesToast extends StatelessWidget {
  const WanesToast({
    super.key,
    required this.kind,
    required this.title,
    this.message,
    this.onDismiss,
  });

  final WanesAlertKind kind;
  final String title;
  final String? message;
  final VoidCallback? onDismiss;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final s = _styleFor(kind, t);

    return Container(
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: t.border),
        boxShadow: [
          BoxShadow(
              color: t.shadow,
              blurRadius: 18,
              spreadRadius: -12,
              offset: const Offset(0, 6)),
        ],
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(13),
        // The coloured edge runs the full height of the card, so the row needs
        // a height before it lays out — toasts sit in unbounded columns.
        child: IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // The 4px coloured edge. Drawn as a strip rather than a border
              // side — Flutter only allows a borderRadius on a uniform border.
              Container(width: 4, color: s.color),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Container(
                        width: 30,
                        height: 30,
                        alignment: Alignment.center,
                        decoration: BoxDecoration(
                          color: s.tint,
                          borderRadius: BorderRadius.circular(9),
                        ),
                        child: Text(
                          s.glyph,
                          style: WanesTheme.mono(
                              size: 15,
                              weight: FontWeight.w800,
                              color: s.color,
                              spacing: 0),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              title,
                              style: TextStyle(
                                  fontSize: 13.5,
                                  fontWeight: FontWeight.w700,
                                  color: t.ink),
                            ),
                            if (message != null && message!.isNotEmpty) ...[
                              const SizedBox(height: 2),
                              Text(
                                message!,
                                style: TextStyle(
                                    fontSize: 12, height: 1.4, color: t.ink2),
                              ),
                            ],
                          ],
                        ),
                      ),
                      if (onDismiss != null) ...[
                        const SizedBox(width: 10),
                        GestureDetector(
                          behavior: HitTestBehavior.opaque,
                          onTap: onDismiss,
                          child: Padding(
                            padding: const EdgeInsetsDirectional.only(
                                top: 1, start: 2),
                            child: Text('✕',
                                style: TextStyle(fontSize: 14, color: t.ink2)),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The prototype's full-screen error card — a tinted alert disc, a headline,
/// a line of explanation and the Dismiss / Retry pair.
///
/// [onRetry] is optional; without it only Dismiss is shown, full width.
class WanesErrorPanel extends StatelessWidget {
  const WanesErrorPanel({
    super.key,
    this.title,
    this.message,
    this.dismissLabel,
    this.retryLabel,
    this.onDismiss,
    this.onRetry,
  });

  /// All four labels fall back to the localised defaults when omitted.
  final String? title;
  final String? message;
  final String? dismissLabel;
  final String? retryLabel;
  final VoidCallback? onDismiss;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final title = this.title ?? context.tr('alerts.connectionLost');
    final message = this.message ?? context.tr('alerts.connectionLostBody');
    final dismissLabel = this.dismissLabel ?? context.tr('common.dismiss');
    final retryLabel = this.retryLabel ?? context.tr('common.retry');

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: t.border),
        boxShadow: [
          BoxShadow(
              color: t.shadow,
              blurRadius: 18,
              spreadRadius: -12,
              offset: const Offset(0, 6)),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 52,
            height: 52,
            alignment: Alignment.center,
            decoration:
                BoxDecoration(color: t.alertTint, shape: BoxShape.circle),
            child: Icon(Icons.error_outline_rounded, size: 28, color: t.alert),
          ),
          const SizedBox(height: 12),
          Text(
            title,
            textAlign: TextAlign.center,
            style: TextStyle(
                fontSize: 16, fontWeight: FontWeight.w800, color: t.ink),
          ),
          const SizedBox(height: 5),
          Text(
            message,
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 12.5, height: 1.45, color: t.ink2),
          ),
          const SizedBox(height: 14),
          Row(children: [
            Expanded(child: _ghostButton(context, dismissLabel, onDismiss)),
            if (onRetry != null) ...[
              const SizedBox(width: 9),
              Expanded(child: _tealButton(context, retryLabel, onRetry!)),
            ],
          ]),
        ],
      ),
    );
  }

  Widget _ghostButton(BuildContext context, String label, VoidCallback? onTap) {
    final t = WanesTokens.of(context);
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(11),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 11),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(11),
          border: Border.all(color: t.border),
        ),
        child: Text(label,
            style: TextStyle(
                fontSize: 13, fontWeight: FontWeight.w700, color: t.ink)),
      ),
    );
  }

  Widget _tealButton(BuildContext context, String label, VoidCallback onTap) {
    final t = WanesTokens.of(context);
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(11),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 11),
        alignment: Alignment.center,
        decoration: BoxDecoration(
            color: t.teal, borderRadius: BorderRadius.circular(11)),
        child: Text(label,
            style: TextStyle(
                fontSize: 13, fontWeight: FontWeight.w800, color: t.onTeal)),
      ),
    );
  }
}

/// The strip at the bottom of screen 13 — a pulsing dot on a tinted bar.
/// Use it for persistent, in-page conditions (offline, payment declined),
/// where a toast would disappear too soon.
class WanesInlineAlert extends StatelessWidget {
  const WanesInlineAlert(
    this.text, {
    super.key,
    this.kind = WanesAlertKind.error,
    this.onTap,
  });

  final String text;
  final WanesAlertKind kind;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final s = _styleFor(kind, t);

    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 10),
        decoration: BoxDecoration(
            color: s.tint, borderRadius: BorderRadius.circular(11)),
        child: Row(children: [
          PulseDot(color: s.color),
          const SizedBox(width: 9),
          Expanded(
            child: Text(
              text,
              style: TextStyle(
                  fontSize: 12, fontWeight: FontWeight.w600, color: s.color),
            ),
          ),
        ]),
      ),
    );
  }
}

/// ─────────────────────────────────────────────────────────────────────────
/// Entry points
/// ─────────────────────────────────────────────────────────────────────────

/// Shows the screen-13 feedback states. Toasts stack from the top of the
/// screen (newest last, at most [_maxVisible]) in the root overlay, so they
/// survive the `toast → Navigator.pop` pattern used across the app.
class WanesAlerts {
  WanesAlerts._();

  static const _maxVisible = 3;
  static const _defaultDuration = Duration(seconds: 4);

  static final Map<OverlayState, _ToastHost> _hosts = {};

  /// Queue a toast. Returns immediately.
  static void show(
    BuildContext context, {
    required String title,
    String? message,
    WanesAlertKind kind = WanesAlertKind.info,
    Duration duration = _defaultDuration,
  }) {
    final overlay = Overlay.maybeOf(context, rootOverlay: true);
    if (overlay == null) return;
    final host = _hosts.putIfAbsent(overlay, () => _ToastHost(overlay));
    host.add(_ToastData(
        kind: kind, title: title, message: message, duration: duration));
  }

  static void success(BuildContext context, String title, {String? message}) =>
      show(context,
          title: title, message: message, kind: WanesAlertKind.success);

  static void error(BuildContext context, String title, {String? message}) =>
      show(context, title: title, message: message, kind: WanesAlertKind.error);

  static void warning(BuildContext context, String title, {String? message}) =>
      show(context,
          title: title, message: message, kind: WanesAlertKind.warning);

  static void info(BuildContext context, String title, {String? message}) =>
      show(context, title: title, message: message, kind: WanesAlertKind.info);

  /// The full-screen error card as a modal. Resolves `true` when Retry was
  /// tapped, `false` on Dismiss or a barrier tap.
  static Future<bool> showErrorDialog(
    BuildContext context, {
    String? title,
    String? message,
    String? dismissLabel,
    String? retryLabel,
    bool canRetry = true,
  }) async {
    final t = WanesTokens.of(context);
    title ??= context.tr('alerts.connectionLost');
    message ??= context.tr('alerts.connectionLostBody');
    dismissLabel ??= context.tr('common.dismiss');
    retryLabel ??= context.tr('common.retry');
    final result = await showDialog<bool>(
      context: context,
      barrierColor: t.ink.withValues(alpha: 0.45),
      builder: (ctx) => Dialog(
        backgroundColor: Colors.transparent,
        elevation: 0,
        insetPadding: const EdgeInsets.symmetric(horizontal: 26, vertical: 24),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 340),
          child: WanesErrorPanel(
            title: title,
            message: message,
            dismissLabel: dismissLabel,
            retryLabel: retryLabel,
            onDismiss: () => Navigator.of(ctx).pop(false),
            onRetry: canRetry ? () => Navigator.of(ctx).pop(true) : null,
          ),
        ),
      ),
    );
    return result ?? false;
  }

  /// The one call every failed request should make.
  ///
  /// A transport failure (no signal, timeout, unreadable body) gets the
  /// full-screen "Connection lost" card with a Retry that re-runs [onRetry];
  /// a business-rule failure gets an error toast carrying the resolved
  /// backend message. Does nothing for a successful response.
  static Future<void> failure(
    BuildContext context,
    AppResponse<Object?> res, {
    String? title,
    String? fallbackMessage,
    FutureOr<void> Function()? onRetry,
  }) async {
    if (res.success) return;
    final message =
        res.errorMessage ?? fallbackMessage ?? context.tr('errors.tryAgain');

    if (res.isTransportError) {
      final retry = await showErrorDialog(
        context,
        title: context.tr('alerts.connectionLost'),
        message: message,
        canRetry: onRetry != null,
      );
      if (retry && onRetry != null) await onRetry();
      return;
    }
    error(context, title ?? context.tr('alerts.somethingWentWrong'),
        message: message);
  }
}

/// Owns the overlay entry + the live toast list for one [OverlayState].
class _ToastHost {
  _ToastHost(this.overlay) {
    entry = OverlayEntry(builder: (_) => _ToastLayer(host: this));
    overlay.insert(entry);
  }

  final OverlayState overlay;
  late final OverlayEntry entry;
  final ValueNotifier<List<_ToastData>> items = ValueNotifier(const []);

  void add(_ToastData data) {
    final next = [...items.value, data];
    // Keep the stack to the design's three cards. One already on screen slides
    // away; one queued in this same frame has no state to animate — several
    // calls in a row (a burst of failures) would otherwise all show at once.
    final excess = next.length - WanesAlerts._maxVisible;
    if (excess > 0) {
      for (final old in next.take(excess).toList()) {
        if (old.key.currentState == null) {
          next.remove(old);
        } else {
          old.dismiss();
        }
      }
    }
    items.value = next;
  }

  void remove(_ToastData data) {
    items.value = items.value.where((x) => x != data).toList();
    if (items.value.isEmpty) _disposeIfIdle();
  }

  void _disposeIfIdle() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (items.value.isNotEmpty) return;
      if (WanesAlerts._hosts[overlay] != this) return;
      WanesAlerts._hosts.remove(overlay);
      if (entry.mounted) entry.remove();
    });
  }
}

class _ToastData {
  _ToastData(
      {required this.kind,
      required this.title,
      this.message,
      required this.duration});

  final WanesAlertKind kind;
  final String title;
  final String? message;
  final Duration duration;
  final GlobalKey<_ToastItemState> key = GlobalKey<_ToastItemState>();

  /// Animate this card out (no-op once it has left the tree).
  void dismiss() => key.currentState?.dismiss();
}

/// The stack itself: top-aligned under the status bar, 9px gaps, centred and
/// width-capped so it stays phone-shaped on a tablet.
class _ToastLayer extends StatelessWidget {
  const _ToastLayer({required this.host});

  final _ToastHost host;

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    return Positioned(
      top: media.padding.top + 10,
      left: 0,
      right: 0,
      child: Align(
        alignment: Alignment.topCenter,
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 420),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 18),
            // An overlay entry sits outside the app's Material, so text there
            // would render with Flutter's missing-Material debug style.
            child: Material(
              type: MaterialType.transparency,
              child: ValueListenableBuilder<List<_ToastData>>(
                valueListenable: host.items,
                builder: (_, items, __) => Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    for (final d in items)
                      _ToastItem(
                        key: d.key,
                        data: d,
                        onRemoved: () => host.remove(d),
                      ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// One card's lifecycle — slide + fade in, auto-dismiss, swipe or ✕ to close,
/// then collapse so the cards below reflow.
class _ToastItem extends StatefulWidget {
  const _ToastItem({super.key, required this.data, required this.onRemoved});

  final _ToastData data;
  final VoidCallback onRemoved;

  @override
  State<_ToastItem> createState() => _ToastItemState();
}

class _ToastItemState extends State<_ToastItem>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
      vsync: this, duration: const Duration(milliseconds: 260))
    ..forward();
  Timer? _timer;
  bool _leaving = false;

  @override
  void initState() {
    super.initState();
    _timer = Timer(widget.data.duration, dismiss);
  }

  @override
  void dispose() {
    _timer?.cancel();
    _c.dispose();
    super.dispose();
  }

  /// Reverse the entry animation, then drop the card from the list.
  Future<void> dismiss() async {
    if (_leaving) return;
    _leaving = true;
    _timer?.cancel();
    if (!mounted) return;
    await _c.reverse();
    if (mounted) widget.onRemoved();
  }

  @override
  Widget build(BuildContext context) {
    final curve = CurvedAnimation(parent: _c, curve: Curves.easeOutCubic);
    return SizeTransition(
      sizeFactor: curve,
      alignment: Alignment.topCenter,
      child: FadeTransition(
        opacity: curve,
        child: SlideTransition(
          position: Tween(begin: const Offset(0, -0.28), end: Offset.zero)
              .animate(curve),
          child: Padding(
            padding: const EdgeInsets.only(bottom: 9),
            child: Dismissible(
              key: ValueKey(widget.data),
              direction: DismissDirection.horizontal,
              onDismissed: (_) {
                _timer?.cancel();
                _leaving = true;
                widget.onRemoved();
              },
              child: WanesToast(
                kind: widget.data.kind,
                title: widget.data.title,
                message: widget.data.message,
                onDismiss: dismiss,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
