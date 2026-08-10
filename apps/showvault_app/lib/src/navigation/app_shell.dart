import 'package:flutter/material.dart';

class AppShell extends StatelessWidget {
  const AppShell({required this.child, super.key});

  final Widget child;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.shield_outlined),
          SizedBox(width: 12),
          Text('ShowVault'),
        ],
      ),
    ),
    body: SafeArea(child: child),
  );
}
