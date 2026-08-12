import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

final recoveryHistoryProvider = Provider<List<RecoveryRun>>((ref) {
  // Authenticated API loading replaces this honest empty state after sign-in.
  return const [];
});
