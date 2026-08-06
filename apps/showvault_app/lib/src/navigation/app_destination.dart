import 'package:flutter/material.dart';

enum AppDestination {
  dashboard('/', 'Dashboard', Icons.dashboard_outlined),
  venues('/venues', 'Venues', Icons.apartment_outlined),
  devices('/devices', 'Devices', Icons.memory_outlined),
  discovery('/discovery', 'Discovery', Icons.radar_outlined),
  backups('/backups', 'Backups', Icons.inventory_2_outlined),
  verification('/verification', 'Verification', Icons.verified_outlined),
  recovery('/recovery', 'Recovery', Icons.restore_outlined),
  digitalTwin('/digital-twin', 'Digital Twin', Icons.account_tree_outlined),
  plugins('/plugins', 'Plugins', Icons.extension_outlined),
  settings('/settings', 'Settings', Icons.settings_outlined);

  const AppDestination(this.path, this.label, this.icon);

  final String path;
  final String label;
  final IconData icon;
}
