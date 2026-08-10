import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:showvault_app/src/recovery/hosted_sync_object_store.dart';
import 'package:showvault_app/src/recovery/local_sync_object_store.dart';

void main() {
  const packageId =
      'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';
  const manifestDigest =
      'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb';

  test(
    'uses bearer auth and path-free tenant-scoped hosted endpoints',
    () async {
      final requests = <http.Request>[];
      final client = MockClient((request) async {
        requests.add(request);
        final path = request.url.path;
        if (path.endsWith('/receipt')) return http.Response('', 404);
        if (path.endsWith('/begin')) return http.Response('', 204);
        if (path.endsWith('/file-state')) {
          return http.Response(
            jsonEncode({
              'payload': {'uploadedLength': 4},
            }),
            200,
          );
        }
        if (path.endsWith('/chunks')) return http.Response('', 204);
        if (path.endsWith('/commit')) {
          return http.Response(
            jsonEncode({
              'payload': {
                'packageId': packageId,
                'remoteManifestSha256': manifestDigest,
                'completedAt': '2026-08-10T20:00:00Z',
              },
            }),
            200,
          );
        }
        return http.Response('', 500);
      });
      final store = HostedSyncObjectStore(
        apiBaseUrl: 'https://api.showvault.invalid/',
        accessToken: 'memory-only-token',
        organizationId: '11111111-1111-1111-1111-111111111111',
        venueId: '22222222-2222-2222-2222-222222222222',
        client: client,
      );
      const descriptor = LocalSyncFileDescriptor(
        relativePath: 'Subcrates/test.crate',
        size: 8,
        sha256: manifestDigest,
      );

      expect(await store.committedReceipt(packageId), isNull);
      expect(
        await store.beginUpload(packageId, utf8.encode('{"safe":true}'), [
          descriptor,
        ]),
        isNull,
      );
      expect(await store.uploadedLength(packageId, descriptor.relativePath), 4);
      await store.appendChunk(packageId, descriptor.relativePath, 4, [
        1,
        2,
        3,
        4,
      ]);
      final receipt = await store.verifyAndCommit(
        packageId,
        utf8.encode('{"safe":true}'),
        [descriptor],
      );

      expect(receipt.remoteManifestSha256, manifestDigest);
      expect(requests, hasLength(5));
      for (final request in requests) {
        expect(request.headers['Authorization'], 'Bearer memory-only-token');
        expect(request.url.path, contains('/organizations/11111111-'));
        expect(request.url.path, contains('/venues/22222222-'));
        expect(request.url.path, contains('/hosted-sync/$packageId/'));
        expect(request.body, isNot(contains('memory-only-token')));
        expect(request.body, isNot(contains('/Users/')));
      }
      final chunk = jsonDecode(requests[3].body) as Map<String, Object?>;
      expect(chunk['relativePath'], 'Subcrates/test.crate');
      expect(chunk['offset'], 4);
      expect(base64Decode(chunk['bytes']! as String), [1, 2, 3, 4]);
    },
  );

  test(
    'expired authentication is retryable and forbidden scope is rejected',
    () async {
      HostedSyncObjectStore storeFor(int status) => HostedSyncObjectStore(
        apiBaseUrl: 'https://api.showvault.invalid',
        accessToken: 'token',
        organizationId: 'organization',
        venueId: 'venue',
        client: MockClient((_) async => http.Response('', status)),
      );

      await expectLater(
        storeFor(401).committedReceipt(packageId),
        throwsA(isA<LocalObjectStoreUnavailableException>()),
      );
      await expectLater(
        storeFor(403).committedReceipt(packageId),
        throwsA(isA<LocalObjectStoreIntegrityException>()),
      );
    },
  );

  test('malformed hosted state cannot advance the durable queue', () async {
    final store = HostedSyncObjectStore(
      apiBaseUrl: 'https://api.showvault.invalid',
      accessToken: 'token',
      organizationId: 'organization',
      venueId: 'venue',
      client: MockClient(
        (_) async => http.Response(
          jsonEncode({
            'payload': {'uploadedLength': -1},
          }),
          200,
        ),
      ),
    );

    await expectLater(
      store.uploadedLength(packageId, 'database V2'),
      throwsA(isA<LocalObjectStoreIntegrityException>()),
    );
  });
}
