import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:showvault_app/src/api/showvault_api.dart';

void main() {
  test('loads the first accessible venue and its live recovery history', () async {
    final requestedPaths = <String>[];
    final api = ShowVaultApi(
      client: MockClient((request) async {
        requestedPaths.add(request.url.path);
        expect(request.headers['Authorization'], 'Bearer access-token');
        final payload = switch (request.url.path) {
          '/api/v1/organizations' =>
            '[{"id":"org-id","name":"ShowVault","slug":"showvault","role":"owner"}]',
          '/api/v1/organizations/org-id/venues' =>
            '[{"id":"venue-id","organizationId":"org-id","name":"Main Stage","timeZoneId":"America/New_York"}]',
          _ =>
            '[{"discoveryCommandId":"command-id","agentName":"Control Agent","startedAt":"2026-08-07T02:14:00Z","status":"completed","stages":[{"stage":"scan","status":"completed","occurredAt":"2026-08-07T02:15:00Z"},{"stage":"backup","status":"completed","occurredAt":null},{"stage":"verify","status":"completed","occurredAt":null},{"stage":"restore","status":"completed","occurredAt":null}]}]',
        };
        return http.Response('{"payload":$payload}', 200);
      }),
    );

    final history = await api.loadRecoveryHistory('access-token');

    expect(history.organizationName, 'ShowVault');
    expect(history.venueName, 'Main Stage');
    expect(history.runs.single.agentName, 'Control Agent');
    expect(requestedPaths, [
      '/api/v1/organizations',
      '/api/v1/organizations/org-id/venues',
      '/api/v1/organizations/org-id/venues/venue-id/recovery-runs',
    ]);
  });

  test('rejects an insecure API origin before sending an access token', () {
    var requestSent = false;
    final client = MockClient((request) async {
      requestSent = true;
      return http.Response('{"payload":[]}', 200);
    });

    expect(
      () => ShowVaultApi(client: client, baseUrl: 'http://api.showvault.test'),
      throwsArgumentError,
    );
    expect(requestSent, isFalse);
  });

  test('rejects an empty access token before making a request', () async {
    var requestSent = false;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        requestSent = true;
        return http.Response('{"payload":[]}', 200);
      }),
      baseUrl: 'https://api.showvault.test',
    );

    await expectLater(api.loadRecoveryHistory('   '), throwsArgumentError);
    expect(requestSent, isFalse);
  });
}
