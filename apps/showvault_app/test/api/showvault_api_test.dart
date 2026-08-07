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
          '/api/v1/organizations/org-id/venues/venue-id/agents' =>
            '[{"id":"agent-id","name":"Control Agent","createdAt":"2026-08-07T02:00:00Z"}]',
          _ =>
            '[{"discoveryCommandId":"command-id","agentName":"Control Agent","startedAt":"2026-08-07T02:14:00Z","status":"completed","stages":[{"stage":"scan","status":"completed","occurredAt":"2026-08-07T02:15:00Z"},{"stage":"backup","status":"completed","occurredAt":null},{"stage":"verify","status":"completed","occurredAt":null},{"stage":"restore","status":"completed","occurredAt":null}]}]',
        };
        return http.Response('{"payload":$payload}', 200);
      }),
    );

    final history = await api.loadRecoveryHistory('access-token');

    expect(history.organizationName, 'ShowVault');
    expect(history.venueName, 'Main Stage');
    expect(history.agents.single.id, 'agent-id');
    expect(history.runs.single.agentName, 'Control Agent');
    expect(requestedPaths, [
      '/api/v1/organizations',
      '/api/v1/organizations/org-id/venues',
      '/api/v1/organizations/org-id/venues/venue-id/agents',
      '/api/v1/organizations/org-id/venues/venue-id/recovery-runs',
    ]);
  });

  test('issues typed recovery workflow commands', () async {
    final requests = <http.Request>[];
    final api = ShowVaultApi(
      client: MockClient((request) async {
        requests.add(request);
        return http.Response(
          '{"payload":{"commandId":"command-${requests.length}"}}',
          202,
        );
      }),
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Main Stage',
      agents: [VenueAgent(id: 'agent-id', name: 'Control Agent')],
      runs: [],
    );

    final discoveryId = await api.startDiscovery(
      accessToken: 'access-token',
      history: history,
      agentId: 'agent-id',
      pluginId: 'showvault.filesystem',
      rootPath: '/approved/show',
    );
    final backupId = await api.createBackup(
      accessToken: 'access-token',
      history: history,
      agentId: 'agent-id',
      discoveryCommandId: discoveryId,
    );
    final verificationId = await api.verifyBackup(
      accessToken: 'access-token',
      history: history,
      agentId: 'agent-id',
      backupCommandId: backupId,
    );
    await api.startRestore(
      accessToken: 'access-token',
      history: history,
      agentId: 'agent-id',
      backupCommandId: backupId,
      verificationCommandId: verificationId,
      targetPath: '/approved/restore',
    );

    expect(requests.map((request) => request.url.path), [
      '/api/v1/organizations/org-id/venues/venue-id/agents/agent-id/recovery/discover',
      '/api/v1/organizations/org-id/venues/venue-id/agents/agent-id/recovery/backup',
      '/api/v1/organizations/org-id/venues/venue-id/agents/agent-id/recovery/verify',
      '/api/v1/organizations/org-id/venues/venue-id/agents/agent-id/recovery/restore',
    ]);
    expect(requests.every((request) => request.method == 'POST'), isTrue);
    expect(
      requests.every(
        (request) => request.headers['Authorization'] == 'Bearer access-token',
      ),
      isTrue,
    );
  });
}
