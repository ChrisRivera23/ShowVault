import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/api/showvault_api.dart';
import 'package:showvault_app/src/auth/auth_provider.dart';

final showVaultApiProvider = Provider<ShowVaultApi>((ref) => ShowVaultApi());

final recoveryHistoryProvider = FutureProvider<RecoveryHistory>((ref) async {
  final session = ref.watch(authSessionProvider).valueOrNull;
  if (session == null) throw StateError('Authentication is required.');
  return ref
      .watch(showVaultApiProvider)
      .loadRecoveryHistory(session.accessToken);
});
