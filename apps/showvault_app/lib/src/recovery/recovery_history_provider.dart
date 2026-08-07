import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';
import 'package:showvault_app/src/recovery/recovery_run.dart';

final showVaultApiProvider = Provider<ShowVaultApi>((ref) => ShowVaultApi());

final recoveryHistoryProvider = FutureProvider<RecoveryHistory>((ref) async {
  final session = ref.watch(authSessionProvider).valueOrNull;
  if (session == null) throw StateError('Authentication is required.');
  final history = await ref
      .watch(showVaultApiProvider)
      .loadRecoveryHistory(session.accessToken);
  if (history.runs.any(
    (run) =>
        run.status == RecoveryRunStatus.pending ||
        run.status == RecoveryRunStatus.inProgress,
  )) {
    final timer = Timer(const Duration(seconds: 3), ref.invalidateSelf);
    ref.onDispose(timer.cancel);
  }
  return history;
});
