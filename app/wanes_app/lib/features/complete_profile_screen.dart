import 'package:flutter/material.dart';
import '../core/l10n.dart';
import '../core/notification_router.dart';
import '../core/session.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/wanes_logo.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'login_screen.dart';
import 'profile_form.dart';
import 'rider_shell.dart';


/// Shown once, right after the first verified sign-in (and on any later start
/// where the account still has no name). A phone number alone is not enough to
/// share a car with someone — the people you match with see a name, so we ask
/// for it before letting the account into the app.
///
/// There is no back button and no skip: the only ways out are saving or
/// signing out.
class CompleteProfileScreen extends StatefulWidget {
  const CompleteProfileScreen({super.key, this.profile});

  final Profile? profile;

  @override
  State<CompleteProfileScreen> createState() => _CompleteProfileScreenState();
}

class _CompleteProfileScreenState extends State<CompleteProfileScreen> {
  final _auth = AuthService();
  late final Profile? _initial = widget.profile ?? Session.instance.profile;
  late final ProfileFormState _form = ProfileFormState(_initial);
  bool _busy = false;

  @override
  void dispose() {
    _form.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final problem = _form.validate();
    if (problem != null) {
      WanesAlerts.warning(context, problem);
      return;
    }

    setState(() => _busy = true);
    final res = await _auth.updateProfile(
      firstName: _form.firstName.text.trim(),
      lastName: _form.lastName.text.trim(),
      email: _form.email.text.trim(),
      gender: _form.gender,
      dateOfBirth: _form.dateOfBirth,
    );
    if (!mounted) return;

    if (res.success) {
      Navigator.pushAndRemoveUntil(
          context, MaterialPageRoute(builder: (_) => const RiderShell()), (_) => false);
      // A notification tapped on the way in, held while this gate was up.
      NotificationRouter.drainPending();
      return;
    }
    setState(() => _busy = false);
    WanesAlerts.failure(context, res,
        title: context.tr('profile.saveFailed'), onRetry: _submit);
  }

  Future<void> _signOut() async {
    await _auth.logout();
    if (!mounted) return;
    Navigator.pushAndRemoveUntil(
        context, MaterialPageRoute(builder: (_) => const LoginScreen()), (_) => false);
  }



  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    // The screen is a gate, not a step in a stack — swiping back would leave a
    // half-registered account looking at the app.
    return PopScope(
      canPop: false,
      child: Scaffold(
        body: SafeArea(
          child: SingleChildScrollView(
            padding: EdgeInsets.fromLTRB(
                24, 20, 24, 24 + MediaQuery.viewInsetsOf(context).bottom),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 440),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const SizedBox(height: 4),
                    const Center(child: WanesLogo(size: 38, showWordmark: true, horizontal: true)),
                    const SizedBox(height: 30),
                    Text(context.tr('onboarding.almostThere'),
                        style: TextStyle(
                            fontSize: 24,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -0.4,
                            color: t.ink)),
                    const SizedBox(height: 8),
                    Text(
                      context.tr('onboarding.almostThereBody'),
                      style: TextStyle(color: t.ink2, height: 1.5, fontSize: 14),
                    ),
                    const SizedBox(height: 26),
                    ProfileFields(
                      state: _form,
                      compact: true,
                      phone: _initial?.phone,
                      onChanged: () => setState(() {}),
                    ),
                    const SizedBox(height: 26),
                    PrimaryButton(
                      label: context.tr('common.continue'),
                      busy: _busy,
                      onPressed: _busy ? null : _submit,
                    ),
                    const SizedBox(height: 14),
                    Center(
                      child: TextButton(
                        onPressed: _busy ? null : _signOut,
                        child: Text(context.tr('onboarding.useDifferentNumber'),
                            style: TextStyle(color: t.ink2, fontWeight: FontWeight.w700, fontSize: 13)),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Center(
                      child: Text(context.tr('onboarding.changeLater'),
                          textAlign: TextAlign.center,
                          style: TextStyle(color: t.ink2, fontSize: 12, height: 1.5)),
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
