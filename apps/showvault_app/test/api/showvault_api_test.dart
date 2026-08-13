import 'dart:convert';

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
    expect(history.organizationRole, 'owner');
    expect(history.venueName, 'Main Stage');
    expect(history.runs.single.agentName, 'Control Agent');
    expect(requestedPaths, [
      '/api/v1/organizations',
      '/api/v1/organizations/org-id/venues',
      '/api/v1/organizations/org-id/venues/venue-id/recovery-runs',
    ]);
  });

  test('loads only the minimized owner plan projection', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response(
          '{"payload":{"planCode":"synthetic.standard","licenseStatus":"active",'
          '"subscriptionStatus":"past_due","currentPeriodEndsAt":null,'
          '"graceEndsAt":"2026-08-20T00:00:00Z","logicalStorageLimitBytes":104857600,'
          '"committedBytes":1024,"reservedBytes":512,"eligible":true,'
          '"reasonCode":"eligible"}}',
          200,
        );
      }),
      baseUrl: 'https://api.showvault.test',
    );

    final plan = await api.loadOrganizationPlan(
      accessToken: 'access-token',
      organizationId: 'org-id',
    );

    expect(captured.url.path, '/api/v1/organizations/org-id/plan');
    expect(captured.headers['Authorization'], 'Bearer access-token');
    expect(plan.planCode, 'synthetic.standard');
    expect(plan.subscriptionStatus, 'past_due');
    expect(plan.graceEndsAt, DateTime.utc(2026, 8, 20));
    expect(plan.committedBytes, 1024);
    expect(plan.reservedBytes, 512);
    expect(plan.eligible, isTrue);
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

  test('submits only opaque direct scan candidate keys', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response(
          '{"payload":{"scanId":"scan-id","candidateCount":2,"completedAt":"2026-08-13T00:00:00Z"}}',
          201,
        );
      }),
      baseUrl: 'https://api.showvault.test',
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Test Venue',
      runs: [],
    );

    final count = await api.submitComputerScan(
      accessToken: 'access-token',
      history: history,
      candidateKeys: const [
        'macos.resolume-arena.application',
        'macos.serato-dj-pro.user-data',
      ],
    );

    expect(count, 2);
    expect(captured.method, 'POST');
    expect(
      captured.url.path,
      '/api/v1/organizations/org-id/venues/venue-id/computer-scans',
    );
    expect(captured.headers['Authorization'], 'Bearer access-token');
    final body = jsonDecode(captured.body) as Map<String, Object?>;
    expect(body.keys, ['candidateKeys']);
    expect(captured.body, isNot(contains('/Applications')));
  });
}
