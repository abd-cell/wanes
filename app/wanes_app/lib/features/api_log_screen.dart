import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../core/api_log.dart';
import '../core/environment.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';

/// On-device API log. Lists every call this build has made, newest first, and
/// opens each one to show the request and response bodies.
///
/// Reached from the profile screen in debug builds only — see [ApiLog.enabled].
class ApiLogScreen extends StatelessWidget {
  const ApiLogScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        child: ValueListenableBuilder<List<ApiLogEntry>>(
          valueListenable: ApiLog.instance.entries,
          builder: (context, entries, _) => Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 14, 20, 0),
                child: ScreenHeader(
                  title: context.tr('apiLog.title'),
                  trailing: entries.isEmpty
                      ? null
                      : CircleIconButton(
                          icon: Icons.delete_outline_rounded,
                          color: t.ink2,
                          onTap: ApiLog.instance.clear,
                        ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 14, 20, 0),
                child: _Summary(entries: entries),
              ),
              Expanded(
                child: entries.isEmpty
                    ? _Empty()
                    : ListView.separated(
                        padding: const EdgeInsets.fromLTRB(20, 14, 20, 24),
                        itemCount: entries.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (_, i) => _Row(entry: entries[i]),
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Call count, failure count and median-ish average latency for what is held.
class _Summary extends StatelessWidget {
  const _Summary({required this.entries});

  final List<ApiLogEntry> entries;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final failed = entries.where((e) => !e.isOk).length;
    final avg = entries.isEmpty
        ? 0
        : entries.map((e) => e.durationMs).reduce((a, b) => a + b) ~/ entries.length;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(
                child: MetricTile(
                    value: '${entries.length}', label: context.tr('apiLog.calls')),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: MetricTile(
                  value: '$failed',
                  label: context.tr('apiLog.failed'),
                  valueColor: failed == 0 ? t.tealInk : t.amberInk,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: MetricTile(
                    value: '$avg ms', label: context.tr('apiLog.average')),
              ),
            ],
          ),
        ),
        const SizedBox(height: 10),
        // The base URL is the single most common cause of "nothing loads" on a
        // real device, so it is shown rather than buried in the build command.
        Row(
          children: [
            Icon(Icons.link_rounded, size: 14, color: t.ink2),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                Environment.apiBaseUrl,
                textDirection: TextDirection.ltr,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: WanesTheme.mono(size: 11, weight: FontWeight.w500, color: t.ink2, spacing: 0),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _Empty extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.swap_vert_rounded, size: 38, color: t.ink2),
            const SizedBox(height: 12),
            Text(context.tr('apiLog.empty'),
                textAlign: TextAlign.center,
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15, color: t.ink)),
            const SizedBox(height: 6),
            Text(context.tr('apiLog.emptyHint'),
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 13, color: t.ink2)),
          ],
        ),
      ),
    );
  }
}

/// One call: method, path, outcome pill and latency.
class _Row extends StatelessWidget {
  const _Row({required this.entry});

  final ApiLogEntry entry;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Material(
      color: t.surface,
      borderRadius: BorderRadius.circular(14),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () => Navigator.push(
            context, MaterialPageRoute(builder: (_) => _DetailScreen(entry: entry))),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(14, 12, 12, 12),
          child: Row(
            children: [
              _OutcomeDot(entry: entry),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(children: [
                      Text(entry.method,
                          style: WanesTheme.mono(
                              size: 10.5, weight: FontWeight.w800, color: t.ink2, spacing: 0.4)),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(entry.shortPath,
                            textDirection: TextDirection.ltr,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: WanesTheme.mono(
                                size: 12, weight: FontWeight.w600, color: t.ink, spacing: 0)),
                      ),
                    ]),
                    const SizedBox(height: 4),
                    Text(
                      '${_clock(entry.at)} · ${entry.durationMs} ms'
                      '${entry.authenticated ? ' · ${context.tr('apiLog.authed')}' : ''}',
                      style: TextStyle(fontSize: 11.5, color: t.ink2),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              _OutcomePill(entry: entry),
            ],
          ),
        ),
      ),
    );
  }
}

/// Colour-codes the three outcomes that matter: transport failure, an error
/// carried inside a 200 envelope, and a genuine success.
class _OutcomeDot extends StatelessWidget {
  const _OutcomeDot({required this.entry});

  final ApiLogEntry entry;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final color = entry.isTransportFailure
        ? t.alert
        : entry.isOk
            ? t.success
            : t.warning;
    return Container(
      width: 8,
      height: 8,
      decoration: BoxDecoration(color: color, shape: BoxShape.circle),
    );
  }
}

class _OutcomePill extends StatelessWidget {
  const _OutcomePill({required this.entry});

  final ApiLogEntry entry;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final label = entry.isTransportFailure ? (entry.failure ?? '—') : '${entry.statusCode}';
    final fg = entry.isTransportFailure
        ? t.alert
        : entry.isOk
            ? t.tealInk
            : t.amberInk;
    final bg = entry.isTransportFailure
        ? t.alertTint
        : entry.isOk
            ? t.tealTint
            : t.amberTint;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(7)),
      child: Text(label,
          style: WanesTheme.mono(size: 10.5, weight: FontWeight.w800, color: fg, spacing: 0.2)),
    );
  }
}

/// Full request/response for a single call.
class _DetailScreen extends StatelessWidget {
  const _DetailScreen({required this.entry});

  final ApiLogEntry entry;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 14, 20, 24),
          children: [
            ScreenHeader(
              title: context.tr('apiLog.detail'),
              trailing: CircleIconButton(
                icon: Icons.copy_rounded,
                color: t.ink2,
                onTap: () => _copyAll(context),
              ),
            ),
            const SizedBox(height: 16),
            Row(children: [
              _OutcomePill(entry: entry),
              const SizedBox(width: 8),
              Text('${entry.durationMs} ms',
                  style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600, color: t.ink2)),
              const Spacer(),
              Text(_clock(entry.at), style: TextStyle(fontSize: 12.5, color: t.ink2)),
            ]),
            const SizedBox(height: 12),
            _Block(
              label: '${entry.method} ${context.tr('apiLog.url')}',
              body: entry.uri.toString(),
            ),
            if (entry.failure != null) ...[
              const SizedBox(height: 12),
              _Block(label: context.tr('apiLog.failure'), body: entry.failure!, tone: t.alert),
            ],
            const SizedBox(height: 12),
            _Block(
              label: context.tr('apiLog.request'),
              body: entry.requestBody.isEmpty
                  ? context.tr('apiLog.noBody')
                  : ApiLogEntry.pretty(entry.requestBody),
            ),
            const SizedBox(height: 12),
            _Block(
              label: context.tr('apiLog.response'),
              body: entry.responseBody.isEmpty
                  ? context.tr('apiLog.noBody')
                  : ApiLogEntry.pretty(entry.responseBody),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _copyAll(BuildContext context) async {
    final text = StringBuffer()
      ..writeln('${entry.method} ${entry.uri}')
      ..writeln('status: ${entry.statusCode ?? entry.failure}')
      ..writeln('duration: ${entry.durationMs} ms')
      ..writeln()
      ..writeln('--- request ---')
      ..writeln(ApiLogEntry.pretty(entry.requestBody))
      ..writeln()
      ..writeln('--- response ---')
      ..writeln(ApiLogEntry.pretty(entry.responseBody));
    await Clipboard.setData(ClipboardData(text: text.toString()));
    if (context.mounted) WanesAlerts.success(context, context.tr('apiLog.copied'));
  }
}

/// A labelled monospace panel. Scrolls sideways so a long URL or a wide JSON
/// line is readable rather than wrapped into noise.
class _Block extends StatelessWidget {
  const _Block({required this.label, required this.body, this.tone});

  final String label;
  final String body;
  final Color? tone;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        MonoLabel(label),
        const SizedBox(height: 6),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: t.surface2,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: t.border),
          ),
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: SelectableText(
              body,
              textDirection: TextDirection.ltr,
              style: WanesTheme.mono(
                  size: 11.5, weight: FontWeight.w500, color: tone ?? t.ink, spacing: 0),
            ),
          ),
        ),
      ],
    );
  }
}

String _two(int n) => n.toString().padLeft(2, '0');

String _clock(DateTime d) => '${_two(d.hour)}:${_two(d.minute)}:${_two(d.second)}';
