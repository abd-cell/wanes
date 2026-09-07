import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_ui.dart';

/// The user's own complaints and suggestions (`GET /feedback`), and the way in
/// to writing another one.
///
/// The list is the screen rather than the form being the screen: someone
/// arriving here a second time is usually checking whether the desk answered,
/// not filing again. The answers land in the same cards, so a reply is read
/// where the complaint was written — not in a notification the user has to
/// keep.
class FeedbackScreen extends StatefulWidget {
  const FeedbackScreen({super.key, this.initialKind, this.tripId});

  /// Opens the compose sheet straight away when set — how a trip screen hands
  /// off "report a problem with this ride".
  final FeedbackKind? initialKind;

  /// The trip a complaint opened from, passed through to the submission.
  final int? tripId;

  @override
  State<FeedbackScreen> createState() => _FeedbackScreenState();
}

class _FeedbackScreenState extends State<FeedbackScreen> {
  final _service = FeedbackService();

  List<FeedbackEntry> _items = const [];
  bool _loading = true;
  String? _error;

  /// Ids of the expanded cards — held here, not in the tiles, so the open set
  /// survives a refresh. Same reasoning as the FAQ screen.
  final _expanded = <int>{};

  @override
  void initState() {
    super.initState();
    _load();
    if (widget.initialKind != null) {
      // After the first frame: the compose sheet needs a mounted route to push
      // onto, and the list underneath should already be on screen behind it.
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _compose(widget.initialKind!);
      });
    }
  }

  /// [silent] re-fetches without flashing the spinner over content the user is
  /// already reading.
  Future<void> _load({bool silent = false}) async {
    if (!silent) setState(() => _loading = true);

    final response = await _service.mine();
    if (!mounted) return;

    setState(() {
      _loading = false;
      if (response.success) {
        _items = response.data ?? const [];
        _error = null;
      } else if (!silent) {
        _error = response.errorMessage ?? context.tr('feedback.loadFailed');
      }
    });
  }

  Future<void> _compose(FeedbackKind kind) async {
    final sent = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => FeedbackComposeScreen(kind: kind, tripId: widget.tripId),
      ),
    );
    if (sent != true || !mounted) return;

    WanesAlerts.success(context, context.tr('feedback.sentTitle'),
        message: context.tr('feedback.sentBody'));
    await _load(silent: true);
  }

  void _toggle(FeedbackEntry entry) => setState(() {
        if (!_expanded.remove(entry.id)) _expanded.add(entry.id);
      });

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Expanded(
            child: RefreshIndicator(
              color: t.tealInk,
              onRefresh: () => _load(silent: true),
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(20, 14, 20, 24),
                children: [
                  ScreenHeader(title: context.tr('feedback.title')),
                  const SizedBox(height: 16),
                  _kindPicker(t),
                  if (_loading)
                    _busy(t)
                  else if (_error != null)
                    _failed(t)
                  else if (_items.isEmpty)
                    _empty(t)
                  else ...[
                    Padding(
                      padding: const EdgeInsets.only(top: 22, bottom: 10),
                      child: MonoLabel(context.tr('feedback.yours'), size: 10.5, spacing: 1.05),
                    ),
                    for (var i = 0; i < _items.length; i++)
                      Padding(
                        padding: EdgeInsets.only(top: i == 0 ? 0 : 10),
                        child: FeedbackTile(
                          entry: _items[i],
                          isOpen: _expanded.contains(_items[i].id),
                          onTap: () => _toggle(_items[i]),
                        ),
                      ),
                  ],
                ],
              ),
            ),
          ),
        ]),
      ),
    );
  }

  /// The two ways in. Side by side rather than one button and a type dropdown:
  /// which of the two this is changes how the whole form reads, and the user
  /// already knows which one they came to write.
  Widget _kindPicker(WanesTokens t) => Row(children: [
        Expanded(
          child: _kindCard(
            t,
            kind: FeedbackKind.complaint,
            icon: Icons.report_gmailerrorred_rounded,
            colour: t.alert,
            tint: t.alertTint,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _kindCard(
            t,
            kind: FeedbackKind.suggestion,
            icon: Icons.lightbulb_outline_rounded,
            colour: t.tealInk,
            tint: t.tealTint,
          ),
        ),
      ]);

  Widget _kindCard(
    WanesTokens t, {
    required FeedbackKind kind,
    required IconData icon,
    required Color colour,
    required Color tint,
  }) =>
      Material(
        color: tint,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(color: colour.withValues(alpha: .35)),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(16),
          onTap: () => _compose(kind),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 15),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Icon(icon, size: 22, color: colour),
              const SizedBox(height: 10),
              Text(context.tr(kind.labelKey),
                  style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14, color: t.ink)),
              const SizedBox(height: 3),
              Text(
                context.tr(kind == FeedbackKind.complaint
                    ? 'feedback.complaintHint'
                    : 'feedback.suggestionHint'),
                style: TextStyle(fontSize: 11.5, height: 1.35, color: t.ink2),
              ),
            ]),
          ),
        ),
      );

  Widget _busy(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 70),
        child: Center(child: WanesSpinner(color: t.tealInk)),
      );

  Widget _failed(WanesTokens t) => Padding(
        padding: const EdgeInsets.only(top: 60),
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
        padding: const EdgeInsets.only(top: 60),
        child: Column(children: [
          Container(
            width: 64,
            height: 64,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: t.tealTint, borderRadius: BorderRadius.circular(20)),
            child: Icon(Icons.forum_outlined, size: 28, color: t.tealInk),
          ),
          const SizedBox(height: 16),
          Text(context.tr('feedback.emptyTitle'),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('feedback.emptyBody'),
              textAlign: TextAlign.center, style: TextStyle(color: t.ink2)),
        ]),
      );
}

/// One submission: the subject and where it stands always visible, the message
/// and the desk's answer revealed on tap.
///
/// Stateless — [FeedbackScreen] owns which cards are open.
class FeedbackTile extends StatelessWidget {
  const FeedbackTile({
    super.key,
    required this.entry,
    required this.isOpen,
    required this.onTap,
  });

  final FeedbackEntry entry;
  final bool isOpen;
  final VoidCallback onTap;

  /// Where a submission stands, in the palette the rest of the app uses for
  /// waiting / done / closed-without-action.
  static Color _statusColour(FeedbackStatus status, WanesTokens t) => switch (status) {
        FeedbackStatus.isNew => t.info,
        FeedbackStatus.inReview => t.amberInk,
        FeedbackStatus.resolved => t.success,
        FeedbackStatus.dismissed => t.ink2,
      };

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final statusColour = _statusColour(entry.status, t);

    return GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      child: AnimatedContainer(
        duration: WanesMotion.press,
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
        decoration: BoxDecoration(
          color: isOpen ? t.surface2 : t.surface,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: isOpen ? statusColour.withValues(alpha: .45) : t.border),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Expanded(child: StatusPill(label: entry.status.label, color: statusColour)),
            // An unread answer is the only thing on this screen the user is
            // waiting for, so it gets the one dot.
            if (entry.hasReply && !isOpen)
              Container(
                margin: const EdgeInsetsDirectional.only(end: 8),
                width: 8,
                height: 8,
                decoration: BoxDecoration(color: t.teal, shape: BoxShape.circle),
              ),
            MetaChip(context.tr(entry.kind.labelKey)),
          ]),
          const SizedBox(height: 10),
          Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Expanded(
              child: Text(
                entry.subject,
                style: TextStyle(
                    fontWeight: FontWeight.w700, fontSize: 13.5, height: 1.35, color: t.ink),
              ),
            ),
            const SizedBox(width: 10),
            AnimatedRotation(
              turns: isOpen ? 0.5 : 0,
              duration: WanesMotion.press,
              curve: Curves.easeOut,
              child: Icon(Icons.keyboard_arrow_down_rounded,
                  size: 20, color: isOpen ? t.ink : t.ink2),
            ),
          ]),
          AnimatedSize(
            duration: WanesMotion.press,
            curve: Curves.easeOut,
            alignment: Alignment.topCenter,
            child: isOpen
                ? Padding(
                    padding: const EdgeInsets.only(top: 9),
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text(entry.message,
                          style: TextStyle(fontSize: 12.5, height: 1.55, color: t.ink2)),
                      if (entry.hasReply) ...[
                        const SizedBox(height: 12),
                        _reply(context, t),
                      ],
                    ]),
                  )
                : const SizedBox(width: double.infinity),
          ),
        ]),
      ),
    );
  }

  /// The desk's answer, set apart from the user's own words so it is obvious
  /// which half of the card is the reply.
  Widget _reply(BuildContext context, WanesTokens t) => Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 11),
        decoration: BoxDecoration(
          color: t.tealTint,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: t.teal),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          MonoLabel(context.tr('feedback.replyLabel'), size: 9.5, spacing: 1.05, color: t.tealInk),
          const SizedBox(height: 7),
          Text(entry.reply!, style: TextStyle(fontSize: 12.5, height: 1.55, color: t.ink)),
        ]),
      );
}

/// The compose form. Pops `true` once the submission is accepted, so the list
/// behind it knows to refresh and say so.
class FeedbackComposeScreen extends StatefulWidget {
  const FeedbackComposeScreen({super.key, required this.kind, this.tripId});

  final FeedbackKind kind;
  final int? tripId;

  @override
  State<FeedbackComposeScreen> createState() => _FeedbackComposeScreenState();
}

class _FeedbackComposeScreenState extends State<FeedbackComposeScreen> {
  final _service = FeedbackService();
  final _subject = TextEditingController();
  final _message = TextEditingController();

  bool _busy = false;

  /// Mirrors the server's `FeedbackInput` minimums. Checked here so a too-short
  /// note costs no round trip, and so the button says it is not ready yet
  /// rather than failing on tap.
  static const _minSubject = 3;
  static const _minMessage = 10;

  @override
  void dispose() {
    _subject.dispose();
    _message.dispose();
    super.dispose();
  }

  bool get _isReady =>
      _subject.text.trim().length >= _minSubject && _message.text.trim().length >= _minMessage;

  Future<void> _submit() async {
    setState(() => _busy = true);

    final response = await _service.submit(
      kind: widget.kind,
      subject: _subject.text.trim(),
      message: _message.text.trim(),
      tripId: widget.tripId,
    );
    if (!mounted) return;

    if (!response.success) {
      setState(() => _busy = false);
      await WanesAlerts.failure(context, response, title: context.tr('feedback.sendFailed'));
      return;
    }

    Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final isComplaint = widget.kind == FeedbackKind.complaint;

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: Column(children: [
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 14, 20, 24),
              children: [
                ScreenHeader(title: context.tr(widget.kind.labelKey)),
                const SizedBox(height: 18),
                Text(
                  context.tr(isComplaint ? 'feedback.complaintLead' : 'feedback.suggestionLead'),
                  style: TextStyle(fontSize: 13, height: 1.5, color: t.ink2),
                ),
                if (widget.tripId != null) ...[
                  const SizedBox(height: 14),
                  Align(
                    alignment: AlignmentDirectional.centerStart,
                    child: MetaChip(
                      context.tr('feedback.aboutTrip', {'id': '${widget.tripId}'}),
                      color: t.tealInk,
                      tint: t.tealTint,
                    ),
                  ),
                ],
                const SizedBox(height: 18),
                MonoLabel(context.tr('feedback.subjectLabel'), size: 10, spacing: 1.05),
                const SizedBox(height: 8),
                TextField(
                  controller: _subject,
                  textInputAction: TextInputAction.next,
                  maxLength: 150,
                  onChanged: (_) => setState(() {}),
                  decoration: InputDecoration(
                    hintText: context.tr(
                        isComplaint ? 'feedback.subjectHint' : 'feedback.subjectHintSuggestion'),
                    counterText: '',
                  ),
                ),
                const SizedBox(height: 16),
                MonoLabel(context.tr('feedback.messageLabel'), size: 10, spacing: 1.05),
                const SizedBox(height: 8),
                TextField(
                  controller: _message,
                  maxLines: 7,
                  minLines: 5,
                  maxLength: 4000,
                  onChanged: (_) => setState(() {}),
                  decoration: InputDecoration(
                    hintText: context
                        .tr(isComplaint ? 'feedback.messageHint' : 'feedback.messageHintSuggestion'),
                    alignLabelWithHint: true,
                  ),
                ),
                const SizedBox(height: 6),
                Text(context.tr('feedback.privacyNote'),
                    style: TextStyle(fontSize: 11.5, height: 1.45, color: t.ink2)),
              ],
            ),
          ),
          BottomActionBar(
            child: PrimaryButton(
              label: context.tr('feedback.send'),
              arrow: false,
              busy: _busy,
              onPressed: _busy || !_isReady ? null : _submit,
            ),
          ),
        ]),
      ),
    );
  }
}
