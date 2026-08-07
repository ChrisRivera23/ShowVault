import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:showvault_app/src/auth/auth_service.dart';
import 'package:showvault_app/src/auth/auth_session.dart';

final authServiceProvider = Provider<AuthService>((ref) => AuthService());

final authSessionProvider =
    StateNotifierProvider<AuthController, AsyncValue<AuthSession?>>((ref) {
      return AuthController(ref.watch(authServiceProvider));
    });

class AuthController extends StateNotifier<AsyncValue<AuthSession?>> {
  AuthController(this._service) : super(const AsyncLoading()) {
    _restore();
  }

  final AuthService _service;

  Future<void> _restore() async {
    state = await AsyncValue.guard(_service.restore);
  }

  Future<void> login() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_service.login);
  }

  Future<void> logout() async {
    state = const AsyncLoading();
    final result = await AsyncValue.guard(_service.logout);
    state = result.hasError
        ? AsyncError(result.error!, result.stackTrace!)
        : const AsyncData(null);
  }
}
