import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';

final localEngineClientProvider = Provider<LocalEngineClient>(
  (ref) => ProcessLocalEngineClient(),
);

class LocalSaveProgress {
  const LocalSaveProgress(this.stage, this.completedUnits, this.totalUnits);

  final String stage;
  final int completedUnits;
  final int totalUnits;
}

class LocalSaveResult {
  const LocalSaveResult({
    required this.recoveryPointId,
    required this.productName,
    required this.fileCount,
    required this.totalBytes,
    required this.localStatus,
    required this.cloudStatus,
  });

  factory LocalSaveResult.fromJson(Map<String, Object?> json) =>
      LocalSaveResult(
        recoveryPointId: _string(json, 'recoveryPointId'),
        productName: _string(json, 'productName'),
        fileCount: _integer(json, 'fileCount'),
        totalBytes: _integer(json, 'totalBytes'),
        localStatus: _string(json, 'localStatus'),
        cloudStatus: _string(json, 'cloudStatus'),
      );

  final String recoveryPointId;
  final String productName;
  final int fileCount;
  final int totalBytes;
  final String localStatus;
  final String cloudStatus;
}

class LocalRecoveryPointSummary extends LocalSaveResult {
  const LocalRecoveryPointSummary({
    required super.recoveryPointId,
    required this.candidateKey,
    required super.productName,
    required super.fileCount,
    required super.totalBytes,
    required this.createdAt,
    required super.localStatus,
    required super.cloudStatus,
  });

  factory LocalRecoveryPointSummary.fromJson(Map<String, Object?> json) =>
      LocalRecoveryPointSummary(
        recoveryPointId: _string(json, 'recoveryPointId'),
        candidateKey: _string(json, 'candidateKey'),
        productName: _string(json, 'productName'),
        fileCount: _integer(json, 'fileCount'),
        totalBytes: _integer(json, 'totalBytes'),
        createdAt: DateTime.parse(_string(json, 'createdAt')),
        localStatus: _string(json, 'localStatus'),
        cloudStatus: _string(json, 'cloudStatus'),
      );

  final String candidateKey;
  final DateTime createdAt;
}

class LocalVaultInspection {
  const LocalVaultInspection({
    required this.recoveryPoints,
    required this.queueAttentionCount,
  });

  final List<LocalRecoveryPointSummary> recoveryPoints;
  final int queueAttentionCount;
}

class LocalSaveOperation {
  const LocalSaveOperation({required this.result, required this.cancel});

  final Future<LocalSaveResult> result;
  final Future<void> Function() cancel;
}

abstract class LocalEngineClient {
  LocalSaveOperation startSave({
    required String candidateKey,
    required String selectedSource,
    required String selectedVault,
    required void Function(LocalSaveProgress progress) onProgress,
  });

  Future<LocalVaultInspection> inspectVault(String selectedVault);
}

class ProcessLocalEngineClient implements LocalEngineClient {
  ProcessLocalEngineClient({String? executablePath})
    : _executablePath = executablePath ?? _packagedHostPath();

  final String _executablePath;

  @override
  LocalSaveOperation startSave({
    required String candidateKey,
    required String selectedSource,
    required String selectedVault,
    required void Function(LocalSaveProgress progress) onProgress,
  }) {
    Process? process;
    final result = () async {
      process = await Process.start(_executablePath, const []);
      final current = process!;
      current.stdin.writeln(
        jsonEncode({
          'operation': 'save',
          'candidateKey': candidateKey,
          'selectedSource': selectedSource,
          'selectedVault': selectedVault,
        }),
      );
      await current.stdin.flush();
      LocalSaveResult? saveResult;
      await for (final line
          in current.stdout
              .transform(utf8.decoder)
              .transform(const LineSplitter())) {
        final envelope = _envelope(line);
        switch (envelope.type) {
          case 'progress':
            final payload = envelope.payload;
            onProgress(
              LocalSaveProgress(
                _string(payload, 'stage'),
                _integer(payload, 'completedUnits'),
                _integer(payload, 'totalUnits'),
              ),
            );
          case 'result':
            saveResult = LocalSaveResult.fromJson(envelope.payload);
          case 'error':
            throw LocalEngineClientException(
              envelope.code ?? 'local_io_failed',
            );
          default:
            throw const LocalEngineClientException('invalid_host_response');
        }
      }
      final errorOutput = await current.stderr.transform(utf8.decoder).join();
      final exitCode = await current.exitCode;
      await current.stdin.close();
      if (errorOutput.isNotEmpty || exitCode != 0 || saveResult == null) {
        throw const LocalEngineClientException('local_io_failed');
      }
      return saveResult;
    }();
    return LocalSaveOperation(
      result: result,
      cancel: () async {
        final current = process;
        if (current != null) {
          try {
            current.stdin.writeln(jsonEncode({'operation': 'cancel'}));
            await current.stdin.flush();
          } on StateError {
            current.kill(ProcessSignal.sigint);
          }
        }
      },
    );
  }

  @override
  Future<LocalVaultInspection> inspectVault(String selectedVault) async {
    final process = await Process.start(_executablePath, const []);
    process.stdin.writeln(
      jsonEncode({'operation': 'inspect', 'selectedVault': selectedVault}),
    );
    await process.stdin.close();
    LocalVaultInspection? inspection;
    await for (final line
        in process.stdout
            .transform(utf8.decoder)
            .transform(const LineSplitter())) {
      final envelope = _envelope(line);
      if (envelope.type == 'error') {
        throw LocalEngineClientException(envelope.code ?? 'local_io_failed');
      }
      if (envelope.type != 'result') {
        throw const LocalEngineClientException('invalid_host_response');
      }
      final payload = envelope.payload;
      final rawItems = payload['recoveryPoints'];
      if (rawItems is! List<Object?>) {
        throw const LocalEngineClientException('invalid_host_response');
      }
      final summaries = rawItems
          .map((item) {
            if (item is! Map<String, Object?>) {
              throw const LocalEngineClientException('invalid_host_response');
            }
            return LocalRecoveryPointSummary.fromJson(item);
          })
          .toList(growable: false);
      inspection = LocalVaultInspection(
        recoveryPoints: summaries,
        queueAttentionCount: _integer(payload, 'queueAttentionCount'),
      );
    }
    final errorOutput = await process.stderr.transform(utf8.decoder).join();
    if (await process.exitCode != 0 ||
        errorOutput.isNotEmpty ||
        inspection == null) {
      throw const LocalEngineClientException('local_io_failed');
    }
    return inspection;
  }

  static String _packagedHostPath() {
    final executable = File(Platform.resolvedExecutable);
    if (Platform.isMacOS) {
      final contents = executable.parent.parent;
      return '${contents.path}${Platform.pathSeparator}Resources'
          '${Platform.pathSeparator}local-engine${Platform.pathSeparator}'
          'showvault-local-engine';
    }
    return '${executable.parent.path}${Platform.pathSeparator}local-engine'
        '${Platform.pathSeparator}showvault-local-engine'
        '${Platform.isWindows ? '.exe' : ''}';
  }
}

class LocalEngineClientException implements Exception {
  const LocalEngineClientException(this.code);
  final String code;
}

({String type, Map<String, Object?> payload, String? code}) _envelope(
  String line,
) {
  Object? decoded;
  try {
    decoded = jsonDecode(line);
  } on FormatException {
    throw const LocalEngineClientException('invalid_host_response');
  }
  if (decoded is! Map<String, Object?> || decoded['type'] is! String) {
    throw const LocalEngineClientException('invalid_host_response');
  }
  final payload = decoded['payload'];
  return (
    type: decoded['type']! as String,
    payload: payload is Map<String, Object?> ? payload : const {},
    code: decoded['code'] as String?,
  );
}

String _string(Map<String, Object?> json, String key) {
  final value = json[key];
  if (value is! String || value.isEmpty || value.length > 512) {
    throw const LocalEngineClientException('invalid_host_response');
  }
  return value;
}

int _integer(Map<String, Object?> json, String key) {
  final value = json[key];
  if (value is! int || value < 0) {
    throw const LocalEngineClientException('invalid_host_response');
  }
  return value;
}
