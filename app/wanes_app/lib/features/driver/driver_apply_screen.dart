import 'package:flutter/material.dart';
import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_ui.dart';

/// Submit license + id for the verified-driver badge (admin approves in the
/// CMS). Optional — posting trips does not wait on it.
class DriverApplyScreen extends StatefulWidget {
  const DriverApplyScreen({super.key});

  @override
  State<DriverApplyScreen> createState() => _DriverApplyScreenState();
}

class _DriverApplyScreenState extends State<DriverApplyScreen> {
  final _profiles = ProfileService();
  final _license = TextEditingController();
  bool _busy = false;

  @override
  void dispose() {
    _license.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_license.text.trim().isEmpty) return;
    setState(() => _busy = true);
    final res = await _profiles.applyAsDriver(licenseNumber: _license.text.trim());
    if (!mounted) return;
    setState(() => _busy = false);
    if (res.success) {
      WanesAlerts.success(context, context.tr('driver.applicationSubmitted'),
          message: context.tr('driver.applicationSubmittedBody'));
    } else {
      WanesAlerts.failure(context, res,
          title: context.tr('driver.applyFailed'), onRetry: _submit);
    }
    if (res.success) Navigator.pop(context);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(context.tr('driver.documents'))),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 24),
        children: [
          Text(context.tr('driver.getVerified'),
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('driver.getVerifiedBody'),
              style: TextStyle(color: t.ink2, height: 1.5)),
          const SizedBox(height: 22),
          MonoLabel(context.tr('driver.licenseNumber')),
          const SizedBox(height: 8),
          TextField(
            controller: _license,
            decoration: InputDecoration(hintText: context.tr('driver.licenseHint')),
          ),
          const SizedBox(height: 18),
          MonoLabel(context.tr('driver.uploads')),
          const SizedBox(height: 8),
          _uploadStub(t, Icons.badge_outlined, context.tr('driver.licensePhoto')),
          const SizedBox(height: 10),
          _uploadStub(t, Icons.description_outlined, context.tr('driver.idDocument')),
          const SizedBox(height: 26),
          PrimaryButton(
              label: context.tr('driver.submitForVerification'),
              arrow: false,
              busy: _busy,
              onPressed: _busy ? null : _submit),
        ],
      ),
    );
  }

  Widget _uploadStub(WanesTokens t, IconData icon, String label) => InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () => WanesAlerts.info(context, context.tr('common.notAvailableYet'),
            message: context.tr('driver.uploadComingSoon')),
        child: DottedBorderBox(
          child: Row(children: [
            Container(
              width: 40, height: 40,
              decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(10), border: Border.all(color: t.border)),
              child: Icon(icon, size: 18, color: t.ink2),
            ),
            const SizedBox(width: 14),
            Expanded(
                child: Text(context.tr('driver.uploadLabel', {'label': label}),
                    style: TextStyle(fontWeight: FontWeight.w700, color: t.ink))),
            Icon(Icons.upload_file_outlined, size: 20, color: t.tealInk),
          ]),
        ),
      );
}

/// A dashed-outline container used for upload affordances.
class DottedBorderBox extends StatelessWidget {
  const DottedBorderBox({super.key, required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: t.border, width: 1.4),
      ),
      child: child,
    );
  }
}
