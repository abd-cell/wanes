import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:url_launcher/url_launcher.dart';

import '../core/app_config.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';

/// How to reach the support desk.
///
/// Every channel comes from the admin-owned platform settings
/// ([AppConfigController]), so the desk can move without shipping a build. A
/// channel the admin has not filled in is simply absent — the screen never
/// offers a row that dials nowhere. It listens to the config notifier and
/// re-fetches on open, so a channel added in the CMS shows up on the next visit
/// rather than the next launch.
class ContactUsScreen extends StatefulWidget {
  const ContactUsScreen({super.key});

  @override
  State<ContactUsScreen> createState() => _ContactUsScreenState();
}

class _ContactUsScreenState extends State<ContactUsScreen> {
  @override
  void initState() {
    super.initState();
    // Fire-and-forget: whatever is cached is already on screen, and the
    // notifier swaps in the fresh values if anything changed.
    ConfigService().refresh();
  }

  /// Opens [uri], falling back to the clipboard when the device has nothing
  /// registered for the scheme — a dead tap is worse than a copied number.
  Future<void> _open(Uri uri, String copyable) async {
    var launched = false;
    try {
      launched = await launchUrl(uri, mode: _modeFor(uri));
    } catch (_) {
      // A PlatformException here means the same thing as `false`.
    }
    if (!mounted || launched) return;

    await Clipboard.setData(ClipboardData(text: copyable));
    if (!mounted) return;
    WanesAlerts.warning(context, context.tr('contact.cannotOpen'),
        message: context.tr('contact.copiedInstead', {'value': copyable}));
  }

  /// `tel:`/`mailto:` are handed to the platform handler; web links are pushed
  /// out to the browser or the WhatsApp app rather than an in-app view.
  static LaunchMode _modeFor(Uri uri) => uri.scheme == 'http' || uri.scheme == 'https'
      ? LaunchMode.externalApplication
      : LaunchMode.platformDefault;

  Future<void> _copy(String value) async {
    await Clipboard.setData(ClipboardData(text: value));
    if (!mounted) return;
    WanesAlerts.success(context, context.tr('contact.copied'), message: value);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        bottom: false,
        child: ValueListenableBuilder<AppConfig>(
          valueListenable: AppConfigController.config,
          builder: (context, config, _) => ListView(
            padding: const EdgeInsets.fromLTRB(20, 14, 20, 28),
            children: [
              ScreenHeader(title: context.tr('contact.title')),
              const SizedBox(height: 18),
              _intro(t),
              const SizedBox(height: 16),
              if (config.hasSupportChannel) _channels(t, config) else _noChannels(t),
              if (config.supportHours.isNotEmpty) ...[
                const SizedBox(height: 14),
                _hours(t, config.supportHours),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _intro(WanesTokens t) => WanesCard(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(context.tr('contact.heading'),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
          const SizedBox(height: 5),
          Text(context.tr('contact.body'),
              style: TextStyle(fontSize: 13, height: 1.45, color: t.ink2)),
        ]),
      );

  /// One row per configured channel, in the order a user in trouble wants them:
  /// talk to someone, then message, then read.
  Widget _channels(WanesTokens t, AppConfig config) {
    final rows = <Widget>[];

    if (config.supportPhone.isNotEmpty) {
      rows.add(_channelRow(
        t,
        icon: Icons.call_outlined,
        title: context.tr('contact.callUs'),
        value: config.supportPhone,
        iconColor: t.tealInk,
        uri: Uri(scheme: 'tel', path: _dialable(config.supportPhone)),
      ));
    }
    if (config.supportWhatsApp.isNotEmpty) {
      rows.add(_channelRow(
        t,
        icon: Icons.chat_outlined,
        title: context.tr('contact.whatsapp'),
        value: config.supportWhatsApp,
        iconColor: t.success,
        // wa.me wants bare digits — a leading "+" or any spacing 404s.
        uri: Uri.parse('https://wa.me/${_digits(config.supportWhatsApp)}'),
      ));
    }
    if (config.supportEmail.isNotEmpty) {
      rows.add(_channelRow(
        t,
        icon: Icons.mail_outline_rounded,
        title: context.tr('contact.emailUs'),
        value: config.supportEmail,
        iconColor: t.info,
        // A subject spares the desk a mail with an empty one.
        uri: Uri(
          scheme: 'mailto',
          path: config.supportEmail,
          queryParameters: {'subject': context.tr('contact.emailSubject')},
        ),
      ));
    }
    if (config.supportWebsite.isNotEmpty) {
      rows.add(_channelRow(
        t,
        icon: Icons.help_outline_rounded,
        title: context.tr('contact.helpCentre'),
        value: config.supportWebsite,
        iconColor: t.amberInk,
        uri: Uri.parse(config.supportWebsite),
        // Nobody copies a URL out of a link they can just open.
        copyable: false,
      ));
    }

    return GroupedCard(children: rows);
  }

  Widget _channelRow(
    WanesTokens t, {
    required IconData icon,
    required String title,
    required String value,
    required Uri uri,
    Color? iconColor,
    bool copyable = true,
  }) {
    return GroupedRow(
      icon: icon,
      iconColor: iconColor,
      title: title,
      subtitle: value,
      onTap: () => _open(uri, value),
      trailing: copyable
          ? Semantics(
              button: true,
              label: context.tr('contact.copy'),
              child: InkResponse(
                onTap: () => _copy(value),
                radius: 20,
                child: Padding(
                  padding: const EdgeInsets.all(4),
                  child: Icon(Icons.copy_rounded, size: 17, color: t.ink2),
                ),
              ),
            )
          : null,
    );
  }

  Widget _hours(WanesTokens t, String hours) => WanesCard(
        child: Row(children: [
          Icon(Icons.schedule_rounded, size: 18, color: t.ink2),
          const SizedBox(width: 11),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              MonoLabel(context.tr('contact.hours')),
              const SizedBox(height: 3),
              Text(hours,
                  style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13.5, color: t.ink)),
            ]),
          ),
        ]),
      );

  /// Nothing configured yet. Says so plainly rather than showing an empty card
  /// the user would read as a loading failure.
  Widget _noChannels(WanesTokens t) => WanesCard(
        child: Column(children: [
          Icon(Icons.support_agent_rounded, size: 30, color: t.ink2),
          const SizedBox(height: 10),
          Text(context.tr('contact.noneTitle'),
              textAlign: TextAlign.center,
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14.5, color: t.ink)),
          const SizedBox(height: 4),
          Text(context.tr('contact.noneBody'),
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 12.5, height: 1.45, color: t.ink2)),
        ]),
      );

  /// Keeps the leading `+` and the digits; strips the spacing and brackets an
  /// admin may have typed for readability.
  static String _dialable(String raw) {
    final digits = _digits(raw);
    return raw.trimLeft().startsWith('+') ? '+$digits' : digits;
  }

  static String _digits(String raw) => raw.replaceAll(RegExp(r'\D'), '');
}
