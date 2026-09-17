import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/session.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'profile_form.dart';


/// Edit the signed-in user's details. Opened from the Profile tab (rider) and
/// the driver profile. Pops with `true` when something was saved so the caller
/// can refresh.
class EditProfileScreen extends StatefulWidget {
  const EditProfileScreen({super.key, this.profile});

  /// The profile to seed the form with; falls back to the cached session one.
  final Profile? profile;

  @override
  State<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends State<EditProfileScreen> {
  final _auth = AuthService();
  late final Profile? _initial = widget.profile ?? Session.instance.profile;
  late final ProfileFormState _form = ProfileFormState(_initial);
  late final _contactName = TextEditingController(text: _initial?.emergencyContactName ?? '');
  late final _contactPhone = TextEditingController(text: _initial?.emergencyContactPhone ?? '');
  bool _busy = false;

  @override
  void dispose() {
    _form.dispose();
    _contactName.dispose();
    _contactPhone.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final problem = _form.validate();
    if (problem != null) {
      WanesAlerts.warning(context, problem);
      return;
    }

    setState(() => _busy = true);
    final res = await _auth.updateProfile(
      firstName: _form.firstName.text.trim(),
      lastName: _form.lastName.text.trim(),
      // Empty strings rather than null: the API reads a missing key as
      // "unchanged", so clearing a field has to send the blank explicitly.
      displayName: _form.displayName.text.trim(),
      email: _form.email.text.trim(),
      gender: _form.gender,
      dateOfBirth: _form.dateOfBirth,
      bio: _form.bio.text.trim(),
      emergencyContactName: _contactName.text.trim(),
      emergencyContactPhone: _contactPhone.text.trim(),
    );
    if (!mounted) return;
    setState(() => _busy = false);

    if (res.success) {
      WanesAlerts.success(context, context.tr('profile.updated'));
      Navigator.pop(context, true);
      return;
    }
    WanesAlerts.failure(context, res,
        title: context.tr('profile.saveFailed'), onRetry: _save);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(context.tr('profile.editProfile'))),
      body: ListView(
        padding: EdgeInsets.fromLTRB(
            20, 8, 20, 24 + MediaQuery.viewInsetsOf(context).bottom),
        children: [
          Text(context.tr('profile.yourDetails'),
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: t.ink)),
          const SizedBox(height: 6),
          Text(context.tr('profile.yourDetailsBody'),
              style: TextStyle(color: t.ink2, height: 1.5)),
          const SizedBox(height: 22),
          ProfileFields(
            state: _form,
            phone: _initial?.phone,
            onChanged: () => setState(() {}),
          ),
          const SizedBox(height: 26),
          // Who the SOS button messages, with a link to follow the ride.
          Text(context.tr('profile.emergencyContact'),
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.w800, color: t.ink)),
          const SizedBox(height: 4),
          Text(context.tr('profile.emergencyContactBody'),
              style: TextStyle(color: t.ink2, fontSize: 12.5, height: 1.4)),
          const SizedBox(height: 12),
          TextField(
            controller: _contactName,
            textCapitalization: TextCapitalization.words,
            decoration: InputDecoration(labelText: context.tr('profile.emergencyName')),
          ),
          const SizedBox(height: 10),
          TextField(
            controller: _contactPhone,
            keyboardType: TextInputType.phone,
            textDirection: TextDirection.ltr,
            decoration: InputDecoration(
                labelText: context.tr('profile.emergencyPhone'), hintText: '+9627…'),
          ),
          const SizedBox(height: 26),
          PrimaryButton(
            label: context.tr('common.saveChanges'),
            arrow: false,
            busy: _busy,
            onPressed: _busy ? null : _save,
          ),
        ],
      ),
    );
  }
}
