import 'package:flutter/material.dart';
import 'package:showvault_app/src/navigation/app_destination.dart';

class SectionScreen extends StatelessWidget {
  const SectionScreen({required this.destination, super.key});

  final AppDestination destination;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(destination.icon, size: 56),
          const SizedBox(height: 16),
          Text(
            destination.label,
            style: Theme.of(context).textTheme.headlineMedium,
          ),
          const SizedBox(height: 8),
          Text(
            '${destination.label} foundation is ready for its first workflow.',
            textAlign: TextAlign.center,
          ),
        ],
      ),
    ),
  );
}
