import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_ui.dart';

/// Help centre. The entries are admin-curated (`GET /faq`), grouped by category
/// and collapsed by default so the whole list stays scannable on a phone.
///
/// Reachable without a token, matching the endpoint: the questions here are the
/// ones people ask before they commit to an account.
class FaqScreen extends StatefulWidget {
  const FaqScreen({super.key});

  @override
  State<FaqScreen> createState() => _FaqScreenState();
}

class _FaqScreenState extends State<FaqScreen> {
  final _service = FaqService();

  Faq _faq = Faq.empty;
  bool _loading = true;
  String? _error;

  /// Ids of the expanded entries. Kept here rather than in each tile so the open
  /// entries survive a refresh, and so a language switch does not collapse them.
  final _expanded = <int>{};

  @override
  void initState() {
    super.initState();
    _load();
  }

  /// [silent] keeps the list on screen while re-fetching, instead of flashing
  /// the spinner over content the user is already reading.
  Future<void> _load({bool silent = false}) async {
    if (!silent) setState(() => _loading = true);

    final response = await _service.get();
    if (!mounted) return;

    setState(() {
      _loading = false;
      if (response.success && response.data != null) {
        _faq = response.data!;
        _error = null;
      } else if (!silent) {
        _error = response.errorMessage ?? context.tr('faq.loadFailed');
      }
    });
  }

  void _toggle(FaqItem item) => setState(() {
        if (!_expanded.remove(item.id)) _expanded.add(item.id);
      });

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final groups = _faq.byCategory;

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          color: t.tealInk,
          onRefresh: () => _load(silent: true),
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(20, 14, 20, 24),
            children: [
              ScreenHeader(title: context.tr('faq.title')),
              if (_loading)
                _busy(t)
              else if (_error != null)
                _failed(t)
              else if (groups.isEmpty)
                _empty(t)
              else
                for (final entry in groups.entries) ...[
                  _groupLabel(context.tr(entry.key.labelKey)),
                  for (var i = 0; i < entry.value.length; i++)
                    Padding(
                      padding: EdgeInsets.only(top: i == 0 ? 0 : 10),
                      child: FaqTile(
                        item: entry.value[i],
                        isOpen: _expanded.contains(entry.value[i].id),
                        onTap: () => _toggle(entry.value[i]),
                      ),
                    ),
                ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _groupLabel(String label) => Padding(
        padding: const EdgeInsets.only(top: 18, bottom: 10),
        child: MonoLabel(label, size: 10.5, spacing: 1.05),
      );

  Widget _busy(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 90),
        child: Center(child: WanesSpinner(color: t.tealInk)),
      );

  Widget _failed(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 80),
        child: Column(children: [
          Icon(Icons.cloud_off_rounded, size: 34, color: t.ink2),
          const SizedBox(height: 14),
          Text(_error!, textAlign: TextAlign.center, style: TextStyle(color: t.ink2)),
          const SizedBox(height: 16),
          GestureDetector(
            onTap: _load,
            child: Text(context.tr('common.retry'),
                style: WanesTheme.mono(
                    size: 12, weight: FontWeight.w600, color: t.tealInk, spacing: 0)),
          ),
        ]),
      );

  Widget _empty(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 80),
        child: Column(children: [
          Container(
            width: 64,
            height: 64,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: t.tealTint, borderRadius: BorderRadius.circular(20)),
            child: Icon(Icons.help_outline_rounded, size: 28, color: t.tealInk),
          ),
          const SizedBox(height: 16),
          Text(context.tr('faq.emptyTitle'),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('faq.emptyBody'),
              textAlign: TextAlign.center, style: TextStyle(color: t.ink2)),
        ]),
      );
}

/// One question card: the question always visible, the answer revealed on tap.
///
/// Stateless on purpose — [FaqScreen] owns which entries are open, so the open
/// set survives a refresh and a language switch.
class FaqTile extends StatelessWidget {
  const FaqTile({
    super.key,
    required this.item,
    required this.isOpen,
    required this.onTap,
  });

  final FaqItem item;
  final bool isOpen;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);

    return GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      child: AnimatedContainer(
        duration: WanesMotion.press,
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
        decoration: BoxDecoration(
          color: isOpen ? t.tealTint : t.surface,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: isOpen ? t.teal : t.border),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Expanded(
              child: Text(
                item.question,
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 13.5,
                  height: 1.35,
                  color: t.ink,
                ),
              ),
            ),
            const SizedBox(width: 10),
            // Points down when collapsed, up when open — the chevron is the only
            // affordance on the row, so it has to carry the state.
            AnimatedRotation(
              turns: isOpen ? 0.5 : 0,
              duration: WanesMotion.press,
              curve: Curves.easeOut,
              child: Icon(
                Icons.keyboard_arrow_down_rounded,
                size: 20,
                color: isOpen ? t.tealInk : t.ink2,
              ),
            ),
          ]),
          // AnimatedSize over an empty full-width box rather than a zero-height
          // child: a collapsed tile must contribute no padding of its own, but
          // must not narrow the card either.
          AnimatedSize(
            duration: WanesMotion.press,
            curve: Curves.easeOut,
            alignment: Alignment.topCenter,
            child: isOpen
                ? Padding(
                    padding: const EdgeInsets.only(top: 9),
                    child: Text(
                      item.answer,
                      style: TextStyle(fontSize: 12.5, height: 1.55, color: t.ink2),
                    ),
                  )
                : const SizedBox(width: double.infinity),
          ),
        ]),
      ),
    );
  }
}
