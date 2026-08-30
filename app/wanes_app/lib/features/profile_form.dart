import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;
import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../widgets/wanes_ui.dart';

/// Holds the editable state of a profile form. Both the first-login
/// CompleteProfileScreen and the EditProfileScreen drive the same fields
/// through this, so the two stay in step.
class ProfileFormState {
  ProfileFormState([Profile? p])
      : firstName = TextEditingController(text: p?.firstName ?? ''),
        lastName = TextEditingController(text: p?.lastName ?? ''),
        displayName = TextEditingController(text: p?.displayName ?? ''),
        email = TextEditingController(text: p?.email ?? ''),
        bio = TextEditingController(text: p?.bio ?? ''),
        gender = p?.gender ?? Gender.unspecified,
        dateOfBirth = p?.dateOfBirth;

  final TextEditingController firstName;
  final TextEditingController lastName;
  final TextEditingController displayName;
  final TextEditingController email;
  final TextEditingController bio;
  Gender gender;
  DateTime? dateOfBirth;

  void dispose() {
    firstName.dispose();
    lastName.dispose();
    displayName.dispose();
    email.dispose();
    bio.dispose();
  }

  /// The first problem with what is typed, or null when the form may be sent.
  /// First and last name are the only required pair — everything else is
  /// optional and only checked once it has been filled in.
  String? validate() {
    final l = AppLocalizations.current;
    if (firstName.text.trim().isEmpty) return l.t('profile.firstNameRequired');
    if (lastName.text.trim().isEmpty) return l.t('profile.lastNameRequired');
    final mail = email.text.trim();
    if (mail.isNotEmpty && !RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(mail)) {
      return l.t('profile.emailInvalid');
    }
    final dob = dateOfBirth;
    if (dob != null && _age(dob) < 16) return l.t('profile.minimumAge');
    return null;
  }

  static int _age(DateTime dob) {
    final now = DateTime.now();
    var age = now.year - dob.year;
    if (now.month < dob.month || (now.month == dob.month && now.day < dob.day)) age--;
    return age;
  }
}

/// The profile fields themselves. [compact] drops display name and bio for the
/// first-login pass, where we only want the minimum before letting someone in.
class ProfileFields extends StatelessWidget {
  const ProfileFields({
    super.key,
    required this.state,
    required this.onChanged,
    this.compact = false,
    this.phone,
  });

  final ProfileFormState state;
  final VoidCallback onChanged;
  final bool compact;
  final String? phone;

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Expanded(
            child: _field(
              label: context.tr('profile.firstName'),
              controller: state.firstName,
              hint: context.tr('profile.firstNameHint'),
              capitalization: TextCapitalization.words,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: _field(
              label: context.tr('profile.lastName'),
              controller: state.lastName,
              hint: context.tr('profile.lastNameHint'),
              capitalization: TextCapitalization.words,
            ),
          ),
        ]),
        if (phone != null) ...[
          const SizedBox(height: 16),
          MonoLabel(context.tr('profile.phone')),
          const SizedBox(height: 8),
          Container(
            height: 52,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            decoration: BoxDecoration(
              color: t.surface2,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: t.border),
            ),
            child: Row(children: [
              Icon(Icons.verified_rounded, size: 16, color: t.tealInk),
              const SizedBox(width: 10),
              Expanded(
                child: Text(phone!,
                    textDirection: TextDirection.ltr,
                    style: WanesTheme.mono(size: 13, color: t.ink)),
              ),
              Text(context.tr('profile.verified'),
                  style: WanesTheme.mono(size: 10, weight: FontWeight.w700, color: t.ink2)),
            ]),
          ),
        ],
        if (!compact) ...[
          const SizedBox(height: 16),
          _field(
            label: context.tr('profile.displayName'),
            controller: state.displayName,
            hint: context.tr('profile.displayNameHint'),
            capitalization: TextCapitalization.words,
          ),
        ],
        const SizedBox(height: 16),
        _field(
          label: context.tr('profile.email'),
          controller: state.email,
          hint: context.tr('profile.emailHint'),
          keyboard: TextInputType.emailAddress,
        ),
        const SizedBox(height: 16),
        MonoLabel(context.tr('profile.gender')),
        const SizedBox(height: 8),
        Row(
          children: Gender.values.map((g) {
            final selected = state.gender == g;
            return Expanded(
              child: Padding(
                padding: EdgeInsetsDirectional.only(end: g == Gender.values.last ? 0 : 8),
                child: GestureDetector(
                  onTap: () {
                    state.gender = g;
                    onChanged();
                  },
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 140),
                    height: 46,
                    alignment: Alignment.center,
                    padding: const EdgeInsets.symmetric(horizontal: 6),
                    decoration: BoxDecoration(
                      color: selected ? t.tealTint : t.surface2,
                      borderRadius: BorderRadius.circular(13),
                      border: Border.all(
                        color: selected ? t.teal : t.border,
                        width: selected ? 1.6 : 1,
                      ),
                    ),
                    child: Text(
                      context.tr(g.labelKey),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w700,
                        color: selected ? t.tealInk : t.ink2,
                      ),
                    ),
                  ),
                ),
              ),
            );
          }).toList(),
        ),
        const SizedBox(height: 16),
        MonoLabel(context.tr('profile.dateOfBirth')),
        const SizedBox(height: 8),
        _DateField(
          value: state.dateOfBirth,
          onPicked: (d) {
            state.dateOfBirth = d;
            onChanged();
          },
        ),
        if (!compact) ...[
          const SizedBox(height: 16),
          MonoLabel(context.tr('profile.about')),
          const SizedBox(height: 8),
          TextField(
            controller: state.bio,
            maxLines: 3,
            maxLength: 240,
            textCapitalization: TextCapitalization.sentences,
            decoration: InputDecoration(
              hintText: context.tr('profile.aboutHint'),
              counterText: '',
            ),
          ),
        ],
      ],
    );
  }

  Widget _field({
    required String label,
    required TextEditingController controller,
    String? hint,
    TextInputType? keyboard,
    TextCapitalization capitalization = TextCapitalization.none,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        MonoLabel(label),
        const SizedBox(height: 8),
        TextField(
          controller: controller,
          keyboardType: keyboard,
          textCapitalization: capitalization,
          decoration: InputDecoration(hintText: hint),
        ),
      ],
    );
  }
}

/// Tap-to-pick date row, styled like the other inputs.
class _DateField extends StatelessWidget {
  const _DateField({required this.value, required this.onPicked});
  final DateTime? value;
  final ValueChanged<DateTime> onPicked;

  Future<void> _pick(BuildContext context) async {
    final now = DateTime.now();
    // 16 is the floor the form validates against, so the picker cannot offer a
    // date that Save would immediately reject.
    final picked = await showDatePicker(
      context: context,
      initialDate: value ?? DateTime(now.year - 25, now.month, now.day),
      firstDate: DateTime(now.year - 100),
      lastDate: DateTime(now.year - 16, now.month, now.day),
      helpText: context.tr('profile.dateOfBirth'),
    );
    if (picked != null) onPicked(picked);
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final v = value;
    return GestureDetector(
      onTap: () => _pick(context),
      child: Container(
        height: 52,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        decoration: BoxDecoration(
          color: t.surface2,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: t.border),
        ),
        child: Row(children: [
          Expanded(
            child: Text(
              v == null
                  ? context.tr('profile.pickDate')
                  : DateFormat('d MMM yyyy', context.l10n.localeName).format(v),
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: v == null ? t.ink2 : t.ink,
              ),
            ),
          ),
          Icon(Icons.calendar_today_outlined, size: 17, color: t.ink2),
        ]),
      ),
    );
  }
}
