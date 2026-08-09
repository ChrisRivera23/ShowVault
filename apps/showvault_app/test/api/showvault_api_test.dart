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
          '/api/v1/organizations/org-id/venues/venue-id/recovery-candidates' =>
            '[{"id":"candidate-id","agentName":"Control Agent","productName":"Resolume Arena","candidateType":"UserDataRoot","evidence":"Standard Resolume user-data location","decision":"approved","validationStatus":"passed","validationFileCount":12,"validationTruncated":false}]',
          '/api/v1/organizations/org-id/venues/venue-id/subnet-proposals' =>
            '[{"id":"proposal-id","agentName":"Control Agent","network":"192.168.10.0","prefixLength":24,"interfaceType":"Ethernet","evidence":"No hosts contacted","decision":"pending"}]',
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
    expect(history.candidates.single.productName, 'Resolume Arena');
    expect(history.candidates.single.validationStatus, 'passed');
    expect(history.candidates.single.validationFileCount, 12);
    expect(history.subnetProposals.single.network, '192.168.10.0');
    expect(requestedPaths, [
      '/api/v1/organizations',
      '/api/v1/organizations/org-id/venues',
      '/api/v1/organizations/org-id/venues/venue-id/agents',
      '/api/v1/organizations/org-id/venues/venue-id/recovery-runs',
      '/api/v1/organizations/org-id/venues/venue-id/recovery-candidates',
      '/api/v1/organizations/org-id/venues/venue-id/subnet-proposals',
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
      candidates: [],
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

  test('records a recovery candidate decision', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response('', 204);
      }),
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Main Stage',
      agents: [],
      candidates: [],
      runs: [],
    );

    await api.decideRecoveryCandidate(
      accessToken: 'access-token',
      history: history,
      candidateId: 'candidate-id',
      approved: true,
    );

    expect(captured.method, 'PUT');
    expect(
      captured.url.path,
      '/api/v1/organizations/org-id/venues/venue-id/recovery-candidates/candidate-id/decision',
    );
    expect(captured.body, '{"approved":true}');
  });

  test('records a subnet proposal decision without starting discovery', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response('', 204);
      }),
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Main Stage',
      agents: [],
      candidates: [],
      runs: [],
    );

    await api.decideSubnetProposal(
      accessToken: 'access-token',
      history: history,
      proposalId: 'proposal-id',
      approved: true,
    );

    expect(captured.method, 'PUT');
    expect(
      captured.url.path,
      '/api/v1/organizations/org-id/venues/venue-id/subnet-proposals/proposal-id/decision',
    );
    expect(captured.body, '{"approved":true}');
  });

  test('separately authorizes bounded subnet discovery', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response('{"payload":{"commandId":"subnet-command"}}', 202);
      }),
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Main Stage',
      agents: [],
      candidates: [],
      runs: [],
    );

    final commandId = await api.discoverSubnet(
      accessToken: 'access-token',
      history: history,
      proposalId: 'proposal-id',
    );

    expect(commandId, 'subnet-command');
    expect(captured.method, 'POST');
    expect(
      captured.url.path,
      '/api/v1/organizations/org-id/venues/venue-id/subnet-proposals/proposal-id/discover',
    );
    expect(captured.body, '{"maxHosts":32,"timeoutMilliseconds":500}');
  });

  test('separately authorizes path-free grandMA3 identification', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response(
          '{"payload":{"commandId":"identify-command"}}',
          202,
        );
      }),
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Main Stage',
      agents: [],
      candidates: [],
      runs: [],
    );

    final commandId = await api.identifyMaLighting(
      accessToken: 'access-token',
      history: history,
      proposalId: 'proposal-id',
    );

    expect(commandId, 'identify-command');
    expect(captured.method, 'POST');
    expect(
      captured.url.path,
      '/api/v1/organizations/org-id/venues/venue-id/subnet-proposals/proposal-id/identify-ma-lighting',
    );
    expect(captured.body, '{"timeoutMilliseconds":500}');
  });

  test('separately authorizes path-free Yamaha DME7 identification', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response('{"payload":{"commandId":"yamaha-command"}}', 202);
      }),
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Main Stage',
      agents: [],
      candidates: [],
      runs: [],
    );

    final commandId = await api.identifyYamahaDme(
      accessToken: 'access-token',
      history: history,
      proposalId: 'proposal-id',
    );

    expect(commandId, 'yamaha-command');
    expect(captured.method, 'POST');
    expect(
      captured.url.path,
      '/api/v1/organizations/org-id/venues/venue-id/subnet-proposals/proposal-id/identify-yamaha-dme',
    );
    expect(captured.body, '{"timeoutMilliseconds":500}');
  });

  test('separately authorizes path-free grandMA2 identification', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response(
          '{"payload":{"commandId":"grandma2-command"}}',
          202,
        );
      }),
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Main Stage',
      agents: [],
      candidates: [],
      runs: [],
    );

    final commandId = await api.identifyGrandMa2(
      accessToken: 'access-token',
      history: history,
      proposalId: 'proposal-id',
    );

    expect(commandId, 'grandma2-command');
    expect(captured.method, 'POST');
    expect(
      captured.url.path,
      '/api/v1/organizations/org-id/venues/venue-id/subnet-proposals/proposal-id/identify-grandma2',
    );
    expect(captured.body, '{"timeoutMilliseconds":500}');
  });

  test('queues path-free approved candidate validation', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response('{"payload":{"commandId":"validation-id"}}', 202);
      }),
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Main Stage',
      agents: [],
      candidates: [],
      runs: [],
    );

    final commandId = await api.validateRecoveryCandidate(
      accessToken: 'access-token',
      history: history,
      candidateId: 'candidate-id',
    );

    expect(commandId, 'validation-id');
    expect(captured.method, 'POST');
    expect(
      captured.url.path,
      '/api/v1/organizations/org-id/venues/venue-id/recovery-candidates/candidate-id/validate',
    );
    expect(captured.body, '{"maxFiles":1000}');
    expect(captured.body, isNot(contains('path')));
  });

  test('queues candidate backup without a path', () async {
    late http.Request captured;
    final api = ShowVaultApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response('{"payload":{"commandId":"backup-id"}}', 202);
      }),
    );
    const history = RecoveryHistory(
      organizationId: 'org-id',
      organizationName: 'ShowVault',
      venueId: 'venue-id',
      venueName: 'Main Stage',
      agents: [],
      candidates: [],
      runs: [],
    );

    final commandId = await api.backupRecoveryCandidate(
      accessToken: 'access-token',
      history: history,
      candidateId: 'candidate-id',
    );

    expect(commandId, 'backup-id');
    expect(captured.method, 'POST');
    expect(
      captured.url.path,
      '/api/v1/organizations/org-id/venues/venue-id/recovery-candidates/candidate-id/backup',
    );
    expect(captured.body, isNot(contains('path')));
  });
}
