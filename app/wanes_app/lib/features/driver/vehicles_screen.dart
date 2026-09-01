import 'package:flutter/material.dart';
import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/wanes_ui.dart';
import '../../widgets/wanes_motion.dart';

/// A driver's vehicles. Reskinned to the Wanes card language.
class VehiclesScreen extends StatefulWidget {
  const VehiclesScreen({super.key});

  @override
  State<VehiclesScreen> createState() => _VehiclesScreenState();
}

class _VehiclesScreenState extends State<VehiclesScreen> {
  final _service = VehicleService();
  List<Vehicle> _vehicles = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    final res = await _service.myVehicles();
    if (!mounted) return;
    setState(() {
      _loading = false;
      _vehicles = res.data ?? [];
    });
  }

  Future<void> _addDialog() async {
    final make = TextEditingController();
    final model = TextEditingController();
    final plate = TextEditingController();
    int seats = 4;
    final t = WanesTokens.of(context);

    final added = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setLocal) => AlertDialog(
          backgroundColor: t.surface,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
          title: Text(context.tr('vehicle.addVehicleTitle'),
              style: TextStyle(fontWeight: FontWeight.w800, color: t.ink)),
          content: Column(mainAxisSize: MainAxisSize.min, children: [
            TextField(
                controller: make,
                decoration: InputDecoration(hintText: context.tr('vehicle.makeHint'))),
            const SizedBox(height: 10),
            TextField(
                controller: model,
                decoration: InputDecoration(hintText: context.tr('vehicle.modelHint'))),
            const SizedBox(height: 10),
            TextField(
                controller: plate,
                decoration: InputDecoration(hintText: context.tr('vehicle.plate'))),
            const SizedBox(height: 14),
            Row(children: [
              MonoLabel(context.tr('vehicle.seatCapacity'), color: t.ink2),
              const Spacer(),
              SeatStepper(value: seats, min: 1, max: 8, onChanged: (v) => setLocal(() => seats = v)),
            ]),
          ]),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: Text(context.tr('common.cancel'))),
            FilledButton(
              onPressed: () async {
                final res = await _service.add(
                  make: make.text.trim(), model: model.text.trim(),
                  plate: plate.text.trim(), seatCapacity: seats,
                );
                if (ctx.mounted) Navigator.pop(ctx, res.success);
              },
              style: FilledButton.styleFrom(minimumSize: const Size(88, 44)),
              child: Text(context.tr('common.add')),
            ),
          ],
        ),
      ),
    );
    if (added == true) _load();
  }

  @override
  Widget build(BuildContext context) {
    final t = WanesTokens.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(context.tr('vehicle.myVehicles'))),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _addDialog,
        backgroundColor: t.teal,
        foregroundColor: t.onTeal,
        icon: const Icon(Icons.add),
        label: Text(context.tr('common.add'),
            style: const TextStyle(fontWeight: FontWeight.w700)),
      ),
      body: _loading
          ? const Center(child: WanesSpinner())
          : _vehicles.isEmpty
              ? _empty(t)
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(20, 8, 20, 90),
                  itemCount: _vehicles.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 12),
                  itemBuilder: (_, i) => _vehicleCard(t, _vehicles[i]),
                ),
    );
  }

  Widget _vehicleCard(WanesTokens t, Vehicle v) {
    return WanesCard(
      child: Row(children: [
        Container(
          width: 46, height: 46,
          decoration: BoxDecoration(color: t.surface2, borderRadius: BorderRadius.circular(12), border: Border.all(color: t.border)),
          child: Icon(Icons.directions_car_filled_rounded, color: t.ink2, size: 22),
        ),
        const SizedBox(width: 14),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('${v.make} ${v.model}', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: t.ink)),
            const SizedBox(height: 3),
            Row(children: [
              Text(v.plate,
                  textDirection: TextDirection.ltr,
                  style: WanesTheme.mono(size: 12, weight: FontWeight.w700, color: t.ink2)),
              Text('  ·  ${context.trPlural('vehicle.seatCount', v.seatCapacity)}',
                  style: TextStyle(fontSize: 12.5, color: t.ink2)),
            ]),
          ]),
        ),
        if (v.isDefault)
          StatusPill(label: context.tr('vehicle.default'), color: t.tealInk, dot: false),
      ]),
    );
  }

  Widget _empty(WanesTokens t) => Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Container(
              width: 64, height: 64,
              decoration: BoxDecoration(color: t.tealTint, borderRadius: BorderRadius.circular(20)),
              child: Icon(Icons.directions_car_outlined, size: 28, color: t.tealInk),
            ),
            const SizedBox(height: 16),
            Text(context.tr('vehicle.noVehicles'),
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: t.ink)),
            const SizedBox(height: 6),
            Text(context.tr('vehicle.noVehiclesBody'), style: TextStyle(color: t.ink2)),
          ]),
        ),
      );
}
