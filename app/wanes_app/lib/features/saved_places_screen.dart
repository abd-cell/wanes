import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/saved_places.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../widgets/place_picker.dart';
import '../widgets/wanes_alerts.dart';
import '../widgets/wanes_ui.dart';


/// Manage the rider's shortcuts: the single Home and Work entries plus any
/// number of named favourites. Everything is server-side (`api/v1/me/places`),
/// so a change here shows up on the home screen and in the place picker.
class SavedPlacesScreen extends StatefulWidget {
  const SavedPlacesScreen({super.key});

  @override
  State<SavedPlacesScreen> createState() => _SavedPlacesScreenState();
}

class _SavedPlacesScreenState extends State<SavedPlacesScreen> {
  final _store = SavedPlaces.instance;

  bool _loading = true;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    await _store.load(force: true);
    if (mounted) setState(() => _loading = false);
  }

  /// Picks a place, then saves it under [label]. Favourites also get a name;
  /// Home and Work name themselves.
  Future<void> _add(SavedPlaceLabel label) async {
    final place = await showPlacePicker(
      context,
      title: context.tr(
          label == SavedPlaceLabel.custom ? 'places.newFavourite' : label.labelKey),
      hint: context.tr('places.searchHint'),
    );
    if (place == null || !mounted) return;

    var name = context.tr(label.labelKey);
    if (label == SavedPlaceLabel.custom) {
      final typed = await _askName(place.name);
      if (typed == null || !mounted) return;
      name = typed;
    }

    setState(() => _busy = true);
    final res = await _store.save(label: label, name: name, place: place);
    if (!mounted) return;
    setState(() => _busy = false);
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('places.saveFailed'));
    }
  }

  Future<void> _delete(SavedPlace place) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(context.tr('places.removeTitle')),
        content: Text(context.tr('places.removeBody', {'name': place.name})),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: Text(context.tr('common.cancel'))),
          TextButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: Text(context.tr('common.remove'),
                style: TextStyle(color: WanesTokens.of(ctx).alert)),
          ),
        ],
      ),
    );
    if (ok != true || !mounted) return;

    setState(() => _busy = true);
    final res = await _store.remove(place.id);
    if (!mounted) return;
    setState(() => _busy = false);
    if (!res.success) {
      WanesAlerts.failure(context, res, title: context.tr('places.removeFailed'));
    }
  }

  /// Asks what to call a favourite, pre-filled with the geocoder's short name.
  Future<String?> _askName(String suggestion) {
    final controller = TextEditingController(text: suggestion);
    return showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(context.tr('places.nameThisPlace')),
        content: TextField(
          controller: controller,
          autofocus: true,
          textCapitalization: TextCapitalization.words,
          decoration: InputDecoration(hintText: context.tr('places.nameHint')),
          onSubmitted: (v) => Navigator.pop(ctx, v.trim().isEmpty ? null : v.trim()),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx), child: Text(context.tr('common.cancel'))),
          TextButton(
            onPressed: () {
              final v = controller.text.trim();
              Navigator.pop(ctx, v.isEmpty ? null : v);
            },
            child: Text(context.tr('common.save')),
          ),
        ],
      ),
    );
  }



  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    final favourites = _store.favourites;

    return Scaffold(
      backgroundColor: t.bg,
      body: SafeArea(
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 4),
            child: ScreenHeader(
              title: context.tr('places.savedPlaces'),
              trailing: _busy
                  ? SizedBox(
                      width: 18, height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2, color: t.teal))
                  : null,
            ),
          ),
          Expanded(
            child: _loading
                ? Center(child: CircularProgressIndicator(color: t.teal))
                : RefreshIndicator(
                    onRefresh: _load,
                    child: ListView(
                      padding: const EdgeInsets.fromLTRB(20, 10, 20, 28),
                      children: [
                        Text(
                          context.tr('places.intro'),
                          style: TextStyle(fontSize: 12.5, height: 1.45, color: t.ink2),
                        ),
                        const SizedBox(height: 18),
                        MonoLabel(context.tr('places.shortcuts')),
                        const SizedBox(height: 8),
                        GroupedCard(children: [
                          _fixedRow(SavedPlaceLabel.home),
                          _fixedRow(SavedPlaceLabel.work),
                        ]),
                        const SizedBox(height: 22),
                        SectionHeader(context.tr('places.favourites'),
                            actionLabel: context.tr('common.add'),
                            onAction: _busy ? null : () => _add(SavedPlaceLabel.custom)),
                        const SizedBox(height: 8),
                        if (favourites.isEmpty)
                          _emptyFavourites(t)
                        else
                          GroupedCard(
                            children: favourites.map(_favouriteRow).toList(),
                          ),
                      ],
                    ),
                  ),
          ),
        ]),
      ),
    );
  }

  /// Home / Work: tapping the row sets — or replaces — the place; the trailing
  /// control clears it once one is set.
  Widget _fixedRow(SavedPlaceLabel label) {
    final t = WanesTokens.of(context);
    final saved = _store.byLabel(label);
    return GroupedRow(
      icon: savedPlaceIcon(label),
      title: context.tr(label.labelKey),
      subtitle: saved == null
          ? context.tr('places.notSetYet')
          : (saved.address.isEmpty ? saved.place.detail : saved.address),
      iconColor: saved == null ? t.ink2 : t.tealInk,
      onTap: _busy ? null : () => _add(label),
      trailing: saved == null
          ? Icon(Icons.add_rounded, size: 18, color: t.tealInk)
          : _removeButton(saved),
    );
  }

  Widget _favouriteRow(SavedPlace place) {
    final t = WanesTokens.of(context);
    return GroupedRow(
      icon: savedPlaceIcon(place.label),
      title: place.name,
      subtitle: place.address.isEmpty ? place.place.detail : place.address,
      iconColor: t.tealInk,
      trailing: _removeButton(place),
    );
  }

  /// Sits inside the row's InkWell, so it takes the tap before the row does.
  Widget _removeButton(SavedPlace place) {
    final t = WanesTokens.of(context);
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: _busy ? null : () => _delete(place),
      child: Padding(
        padding: const EdgeInsets.all(4),
        child: Icon(Icons.close_rounded, size: 18, color: t.ink2),
      ),
    );
  }

  Widget _emptyFavourites(WanesTokens t) => WanesCard(
        onTap: _busy ? null : () => _add(SavedPlaceLabel.custom),
        child: Row(children: [
          Container(
            width: 38, height: 38,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: t.tealTint,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(Icons.star_outline_rounded, size: 18, color: t.tealInk),
          ),
          const SizedBox(width: 13),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(context.tr('places.addFavourite'),
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14, color: t.ink)),
              const SizedBox(height: 2),
              Text(context.tr('places.addFavouriteBody'),
                  style: TextStyle(fontSize: 12, color: t.ink2)),
            ]),
          ),
          Icon(Icons.add_rounded, size: 18, color: t.tealInk),
        ]),
      );
}
