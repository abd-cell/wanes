import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/wanes_alerts.dart';
import '../../widgets/wanes_motion.dart';
import '../../widgets/wanes_ui.dart';

/// Submit licence + id for the verified-driver badge. An admin looks at what is
/// uploaded and decides by hand — nothing here is automatic.
///
/// The uploads used to be two dashed boxes that toasted "coming soon", so an
/// application reached the admin queue carrying a licence *number* and no way
/// to check it against anything. The slots below are the real thing: pick a
/// photo, it goes up, and the reviewer sees exactly what was sent.
class DriverApplyScreen extends StatefulWidget {
  const DriverApplyScreen({super.key});

  @override
  State<DriverApplyScreen> createState() => _DriverApplyScreenState();
}

class _DriverApplyScreenState extends State<DriverApplyScreen> {
  final _profiles = ProfileService();
  final _picker = ImagePicker();
  final _license = TextEditingController();

  DriverVerification? _state;
  bool _loading = true;
  bool _busy = false;

  /// The type currently uploading, so only that slot shows a spinner.
  DriverDocumentType? _uploading;

  /// documentId → bytes, so a rebuild does not re-fetch every thumbnail.
  final Map<int, Uint8List> _thumbnails = {};

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _license.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    final res = await _profiles.driverVerification();
    if (!mounted) return;

    if (res.success && res.data != null) {
      final state = res.data!;
      // Don't stamp over what the driver is in the middle of typing.
      if (_license.text.trim().isEmpty) _license.text = state.licenseNumber ?? '';
      setState(() {
        _state = state;
        _loading = false;
      });
      for (final document in state.documents) {
        unawaited(_loadThumbnail(document));
      }
    } else {
      setState(() => _loading = false);
      if (mounted) {
        WanesAlerts.failure(context, res,
            title: context.tr('driver.loadFailed'), onRetry: _load);
      }
    }
  }

  Future<void> _loadThumbnail(DriverDocument document) async {
    if (document.isPdf || _thumbnails.containsKey(document.id)) return;
    final res = await _profiles.driverDocumentBytes(document.id);
    if (!mounted || !res.success || res.data == null) return;
    setState(() => _thumbnails[document.id] = res.data!);
  }

  /// Camera or gallery, then straight up. A licence is a thing you photograph,
  /// so the camera is offered first.
  Future<void> _pick(DriverDocumentType type) async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          ListTile(
            leading: const Icon(Icons.photo_camera_outlined),
            title: Text(sheetContext.tr('driver.takePhoto')),
            onTap: () => Navigator.pop(sheetContext, ImageSource.camera),
          ),
          ListTile(
            leading: const Icon(Icons.photo_library_outlined),
            title: Text(sheetContext.tr('driver.chooseFromGallery')),
            onTap: () => Navigator.pop(sheetContext, ImageSource.gallery),
          ),
        ]),
      ),
    );
    if (source == null) return;

    XFile? file;
    try {
      // Downscaled and re-encoded before it leaves the phone: a modern camera
      // writes 6 MB a shot, and 2000px is still more than enough to read an
      // expiry date off a licence.
      file = await _picker.pickImage(
          source: source, maxWidth: 2000, maxHeight: 2000, imageQuality: 85);
    } catch (_) {
      // No camera, or the OS refused. Nothing to recover from here beyond
      // saying so.
      if (mounted) {
        WanesAlerts.error(context, context.tr('driver.pickFailed'),
            message: context.tr('driver.pickFailedBody'));
      }
      return;
    }
    if (file == null) return;

    final bytes = await file.readAsBytes();
    if (!mounted) return;

    if (bytes.length > _maxUploadBytes) {
      WanesAlerts.error(context, context.tr('driver.fileTooLarge'),
          message: context.tr('driver.fileTooLargeBody'));
      return;
    }

    setState(() => _uploading = type);
    final res = await _profiles.uploadDriverDocument(
      type: type,
      bytes: bytes,
      filename: file.name.isEmpty ? 'document.jpg' : file.name,
      contentType: _contentTypeOf(file),
    );
    if (!mounted) return;
    setState(() => _uploading = null);

    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('driver.uploadFailed'));
      return;
    }
    await _load();
  }

  Future<void> _remove(DriverDocument document) async {
    final res = await _profiles.deleteDriverDocument(document.id);
    if (!mounted) return;
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('driver.removeFailed'));
      return;
    }
    _thumbnails.remove(document.id);
    await _load();
  }

  Future<void> _submit() async {
    final license = _license.text.trim();
    if (license.isEmpty) return;

    setState(() => _busy = true);
    final res = await _profiles.applyAsDriver(licenseNumber: license);
    if (!mounted) return;
    setState(() => _busy = false);

    if (!res.success) {
      WanesAlerts.failure(context, res,
          title: context.tr('driver.applyFailed'), onRetry: _submit);
      return;
    }

    WanesAlerts.success(context, context.tr('driver.applicationSubmitted'),
        message: context.tr('driver.applicationSubmittedBody'));
    Navigator.pop(context);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final state = _state;

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('driver.documents'))),
      body: _loading
          ? const Center(child: WanesSpinner())
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
                children: [
                  Text(context.tr('driver.getVerified'),
                      style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: t.ink)),
                  const SizedBox(height: 6),
                  Text(context.tr('driver.getVerifiedBody'),
                      style: TextStyle(color: t.ink2, height: 1.5)),

                  if (state != null && state.status != 0) ...[
                    const SizedBox(height: 18),
                    _statusBanner(t, state),
                  ],

                  const SizedBox(height: 22),
                  MonoLabel(context.tr('driver.licenseNumber')),
                  const SizedBox(height: 8),
                  TextField(
                    controller: _license,
                    decoration: InputDecoration(hintText: context.tr('driver.licenseHint')),
                  ),

                  const SizedBox(height: 20),
                  MonoLabel(context.tr('driver.uploads')),
                  const SizedBox(height: 6),
                  Text(context.tr('driver.uploadsHint'),
                      style: TextStyle(fontSize: 12.5, color: t.ink2, height: 1.4)),
                  const SizedBox(height: 12),

                  for (final type in DriverDocumentType.values) ...[
                    _slot(t, type, state?.documentOf(type)),
                    const SizedBox(height: 10),
                  ],

                  const SizedBox(height: 16),
                  PrimaryButton(
                    label: context.tr(state != null && state.isPending
                        ? 'driver.resubmit'
                        : 'driver.submitForVerification'),
                    arrow: false,
                    busy: _busy,
                    onPressed: _canSubmit ? _submit : null,
                  ),
                  if (state != null && !state.canSubmit) ...[
                    const SizedBox(height: 10),
                    Text(context.tr('driver.missingDocuments'),
                        textAlign: TextAlign.center,
                        style: TextStyle(fontSize: 12.5, color: t.ink2)),
                  ],
                ],
              ),
            ),
    );
  }

  bool get _canSubmit =>
      !_busy &&
      _uploading == null &&
      _license.text.trim().isNotEmpty &&
      (_state?.canSubmit ?? false);

  /// Where the application stands. On a rejection this is the only place the
  /// driver learns what to fix.
  Widget _statusBanner(WanesTokens t, DriverVerification state) {
    final (Color tint, Color ink, IconData icon, String key) = switch (state.status) {
      2 => (t.tealTint, t.tealInk, Icons.verified_outlined, 'driver.statusVerified'),
      3 => (t.alertTint, t.alert, Icons.error_outline, 'driver.statusRejected'),
      4 => (t.alertTint, t.alert, Icons.block_outlined, 'driver.statusSuspended'),
      _ => (t.warningTint, t.warning, Icons.hourglass_top_outlined, 'driver.statusPending'),
    };

    final note = state.reviewNote?.trim();

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: tint, borderRadius: BorderRadius.circular(14)),
      child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Icon(icon, size: 20, color: ink),
        const SizedBox(width: 12),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(context.tr(key),
                style: TextStyle(fontWeight: FontWeight.w800, color: ink)),
            if (note != null && note.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(note, style: TextStyle(fontSize: 13, height: 1.45, color: t.ink)),
            ],
          ]),
        ),
      ]),
    );
  }

  /// One document slot: empty and tappable, or filled with its thumbnail.
  Widget _slot(WanesTokens t, DriverDocumentType type, DriverDocument? document) {
    final uploading = _uploading == type;
    final thumbnail = document == null ? null : _thumbnails[document.id];

    return InkWell(
      borderRadius: BorderRadius.circular(14),
      onTap: uploading ? null : () => _pick(type),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: t.surface,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
              color: document != null ? t.tealInk.withValues(alpha: 0.45) : t.border, width: 1.4),
        ),
        child: Row(children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(10),
            child: Container(
              width: 56,
              height: 56,
              alignment: Alignment.center,
              decoration: BoxDecoration(color: t.surface2, border: Border.all(color: t.border)),
              child: uploading
                  ? WanesSpinner.mono(t.ink2)
                  : thumbnail != null
                      // Cover here, unlike the reviewer's tile: this is a
                      // reminder of which photo is in the slot, and the admin
                      // panel is where it gets looked at properly.
                      ? Image.memory(thumbnail, width: 56, height: 56, fit: BoxFit.cover)
                      : Icon(document != null ? Icons.description_outlined : Icons.add_a_photo_outlined,
                          size: 20, color: t.ink2),
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Flexible(
                  child: Text(type.label,
                      style: TextStyle(fontWeight: FontWeight.w700, color: t.ink)),
                ),
                if (type.required) ...[
                  const SizedBox(width: 6),
                  Text('*', style: TextStyle(fontWeight: FontWeight.w800, color: t.alert)),
                ],
              ]),
              const SizedBox(height: 2),
              Text(
                document == null
                    ? context.tr('driver.notUploaded')
                    : '${_sizeLabel(document.sizeBytes)} · ${context.tr('driver.tapToReplace')}',
                style: TextStyle(fontSize: 12, color: document == null ? t.ink2 : t.tealInk),
              ),
            ]),
          ),
          if (document != null)
            IconButton(
              tooltip: context.tr('common.remove'),
              icon: Icon(Icons.close_rounded, size: 18, color: t.ink2),
              onPressed: uploading ? null : () => _remove(document),
            )
          else
            Icon(Icons.upload_file_outlined, size: 20, color: t.tealInk),
        ]),
      ),
    );
  }

  static String _sizeLabel(int bytes) {
    final kb = bytes / 1024;
    return kb < 1024 ? '${kb.round()} KB' : '${(kb / 1024).toStringAsFixed(1)} MB';
  }

  /// Mirrors `DriverDocumentRules.MaxBytes` on the server. Checked here too so
  /// a doomed upload does not spend the driver's data first.
  static const _maxUploadBytes = 8 * 1024 * 1024;

  static String _contentTypeOf(XFile file) {
    final declared = file.mimeType;
    if (declared != null && declared.startsWith('image/')) return declared;

    final name = file.name.toLowerCase();
    if (name.endsWith('.png')) return 'image/png';
    if (name.endsWith('.webp')) return 'image/webp';
    if (name.endsWith('.heic')) return 'image/heic';
    return 'image/jpeg';
  }
}
