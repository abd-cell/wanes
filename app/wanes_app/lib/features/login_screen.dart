import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../core/app_response.dart';
import '../core/l10n.dart';
import '../core/theme.dart';
import '../services/services.dart';
import '../widgets/wanes_logo.dart';
import '../widgets/wanes_motion.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';
import 'complete_profile_screen.dart';


/// Phone → OTP sign-in. Mirrors prototype screen 01 (login / OTP): wordmark,
/// "Enter your number", a country-prefixed phone field, then a segmented
/// verification-code entry with a resend countdown.
class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _auth = AuthService();
  final _phone = TextEditingController();
  final _code = TextEditingController();
  bool _codeSent = false;
  bool _busy = false;
  int _resendIn = 0;
  Timer? _timer;

  static const _dialCode = '+962';

  /// Full E.164 phone: the +962 prefix joined to the local part the user typed
  /// (tolerant of a leading 0, spaces, or a pasted +962 / 962).
  String get _fullPhone {
    var local = _phone.text.trim().replaceAll(' ', '');
    local = local.replaceFirst(RegExp(r'^\+?962'), '');
    local = local.replaceFirst(RegExp(r'^0+'), '');
    return '$_dialCode$local';
  }

  @override
  void dispose() {
    _timer?.cancel();
    _phone.dispose();
    _code.dispose();
    super.dispose();
  }

  void _startResendCountdown() {
    _resendIn = 24;
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      setState(() {
        if (_resendIn > 0) _resendIn--;
        if (_resendIn == 0) _timer?.cancel();
      });
    });
  }

  Future<void> _sendCode() async {
    if (_phone.text.trim().isEmpty) return;
    setState(() => _busy = true);
    final res = await _auth.requestOtp(_fullPhone);
    if (!mounted) return;
    setState(() {
      _busy = false;
      if (res.success) _codeSent = true;
    });
    if (res.success) {
      _startResendCountdown();
    } else {
      _fail(res, context.tr('login.sendFailed'), () => _sendCode());
    }
  }

  Future<void> _verify() async {
    if (_code.text.trim().isEmpty) return;
    setState(() => _busy = true);
    final res = await _auth.verifyOtp(_fullPhone, _code.text.trim());
    if (!mounted) return;
    setState(() => _busy = false);
    if (!res.success) {
      _fail(res, context.tr('login.verifyFailed'), () => _verify());
      return;
    }
    // A first sign-in registers the account from the phone number alone, so
    // there is no name on it yet — collect one before the app proper.
    if (res.data?.profile.isComplete != true) {
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => CompleteProfileScreen(profile: res.data?.profile)),
      );
      return;
    }
    Navigator.of(context).pushReplacementNamed('/home');
  }

  /// Transport problems get the full-screen "Connection lost" card with a
  /// Retry that re-runs the request; anything else is an error toast.
  void _fail(AppResponse res, String title, Future<void> Function() retry) =>
      WanesAlerts.failure(context, res, title: title, onRetry: retry);

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 24),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const SizedBox(height: 20),
                  const Align(
                    alignment: AlignmentDirectional.centerStart,
                    child: AnimatedWanesLogo(
                      size: 46,
                      showWordmark: true,
                      horizontal: true,
                      drawDelay: Duration(milliseconds: 300),
                      pinDelay: Duration(milliseconds: 1150),
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(context.tr('brand.tagline'),
                      style: WanesTheme.mono(
                          size: 12, weight: FontWeight.w500, color: t.ink2, spacing: 0.25)),
                  const SizedBox(height: 40),
                  Text(context.tr(_codeSent ? 'login.enterCode' : 'login.enterNumber'),
                      style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800, letterSpacing: -0.4, color: t.ink)),
                  const SizedBox(height: 8),
                  Text(
                    _codeSent
                        ? context.tr('login.codeSentTo', {'phone': _phone.text.trim()})
                        : context.tr('login.smsHint'),
                    style: TextStyle(color: t.ink2, height: 1.5, fontSize: 14),
                  ),
                  const SizedBox(height: 28),
                  if (!_codeSent) _phoneField(t) else _codeField(t),
                  const SizedBox(height: 24),
                  PrimaryButton(
                    label: context.tr('common.continue'),
                    busy: _busy,
                    onPressed: _busy ? null : (_codeSent ? _verify : _sendCode),
                  ),
                  if (_codeSent) ...[
                    const SizedBox(height: 16),
                    Center(
                      child: _resendIn > 0
                          ? Text(
                              context.tr('login.resendIn',
                                  {'seconds': '0:${_resendIn.toString().padLeft(2, '0')}'}),
                              style: WanesTheme.mono(size: 12, color: t.ink2))
                          : GestureDetector(
                              onTap: _sendCode,
                              child: Text(context.tr('login.resend'),
                                  style: WanesTheme.mono(size: 12, weight: FontWeight.w700, color: t.tealInk)),
                            ),
                    ),
                  ],
                  const SizedBox(height: 24),
                  Center(
                    child: Text(
                      context.tr('login.terms'),
                      textAlign: TextAlign.center,
                      style: TextStyle(color: t.ink2, fontSize: 12, height: 1.5),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _phoneField(WanesTokens t) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        MonoLabel(context.tr('login.phoneLabel')),
        const SizedBox(height: 8),
        // One pill holding the dial code, a hairline divider and the number —
        // as the design draws it. Phone numbers read left-to-right in both
        // languages, so the row keeps its own direction.
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 16),
          decoration: BoxDecoration(
            color: t.surface2,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: t.border),
          ),
          child: Row(textDirection: TextDirection.ltr, children: [
            Text('+962',
                style: WanesTheme.mono(size: 15, weight: FontWeight.w700, color: t.ink, spacing: 0)),
            const SizedBox(width: 12),
            Container(width: 1, height: 20, color: t.border),
            const SizedBox(width: 12),
            Expanded(
              child: TextField(
                controller: _phone,
                autofocus: true,
                keyboardType: TextInputType.phone,
                style: WanesTheme.mono(size: 16, weight: FontWeight.w500, color: t.ink, spacing: 0.6),
                decoration: InputDecoration(
                  hintText: '79 000 0000',
                  hintStyle: WanesTheme.mono(size: 16, weight: FontWeight.w500, color: t.ink2, spacing: 0.6),
                  filled: false,
                  border: InputBorder.none,
                  enabledBorder: InputBorder.none,
                  focusedBorder: InputBorder.none,
                  contentPadding: const EdgeInsets.symmetric(vertical: 15),
                ),
              ),
            ),
          ]),
        ),
      ],
    );
  }

  Widget _codeField(WanesTokens t) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        MonoLabel(context.tr('login.codeLabel')),
        const SizedBox(height: 10),
        _OtpBoxes(controller: _code, length: 4, onComplete: (_) => _verify()),
      ],
    );
  }
}

/// Segmented OTP entry — a hidden field drives N boxes that mirror the code.
class _OtpBoxes extends StatefulWidget {
  const _OtpBoxes({required this.controller, this.length = 4, this.onComplete});
  final TextEditingController controller;
  final int length;
  final ValueChanged<String>? onComplete;

  @override
  State<_OtpBoxes> createState() => _OtpBoxesState();
}

class _OtpBoxesState extends State<_OtpBoxes> {
  final _focus = FocusNode();

  @override
  void initState() {
    super.initState();
    widget.controller.addListener(_onChange);
    WidgetsBinding.instance.addPostFrameCallback((_) => _focus.requestFocus());
  }

  void _onChange() {
    setState(() {});
    if (widget.controller.text.length == widget.length) {
      widget.onComplete?.call(widget.controller.text);
    }
  }

  @override
  void dispose() {
    widget.controller.removeListener(_onChange);
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final text = widget.controller.text;
    // The code is a number: the boxes fill left-to-right in both languages.
    return Stack(children: [
      Row(
        textDirection: TextDirection.ltr,
        children: List.generate(widget.length, (i) {
          final filled = i < text.length;
          final active = i == text.length;
          return Expanded(
            child: Padding(
              padding: EdgeInsetsDirectional.only(end: i == widget.length - 1 ? 0 : 9),
              // The active box gets the teal ring and the blinking caret from
              // the prototype (`animation:wanelive 1s steps(1) infinite`); the
              // border eases in so the focus walks across the row.
              child: AnimatedContainer(
                duration: WanesMotion.pressFast,
                curve: Curves.ease,
                height: 54,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: t.surface,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: active ? t.teal : t.border, width: active ? 2 : 1),
                ),
                child: filled
                    ? Text(text[i],
                        style: WanesTheme.mono(
                            size: 22, weight: FontWeight.w700, color: t.ink, spacing: 0))
                    : active
                        ? BlinkingCaret(color: t.teal)
                        : const SizedBox.shrink(),
              ),
            ),
          );
        }),
      ),
      Positioned.fill(
        child: Opacity(
          opacity: 0,
          child: TextField(
            controller: widget.controller,
            focusNode: _focus,
            keyboardType: TextInputType.number,
            maxLength: widget.length,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            showCursor: false,
            decoration: const InputDecoration(counterText: '', border: InputBorder.none),
          ),
        ),
      ),
    ]);
  }
}
