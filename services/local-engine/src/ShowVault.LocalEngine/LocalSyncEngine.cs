using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ShowVault.Agent.Recovery;

namespace ShowVault.LocalEngine;

public sealed class LocalSyncEngine
{
    private const int MaximumChunkBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;
    private readonly LocalEngineLimits _limits;
    private readonly TimeProvider _timeProvider;

    public LocalSyncEngine(
        HttpClient? client = null,
        LocalEngineLimits? limits = null,
        TimeProvider? timeProvider = null)
    {
        _client = client ?? new HttpClient();
        _limits = limits ?? new LocalEngineLimits();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LocalSyncResult> SynchronizeAsync(
        LocalSyncRequest request,
        IProgress<LocalSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        using var vault = LocalVaultLayout.OpenOrCreate(request.SelectedVault);
        var queue = new LocalVaultQueueStore(vault.QueueDatabasePath);
        await queue.InitializeAsync(cancellationToken);
        var records = await queue.ListQueuedAsync(
            Math.Clamp(request.MaximumRecoveryPoints, 1, 25), cancellationToken);
        var candidates = records.Where(record => record.CloudStatus != "synchronized")
            .ToArray();
        var synchronized = 0;
        var retry = 0;
        var attention = 0;
        long bytes = 0;

        for (var index = 0; index < candidates.Length; index++)
        {
            var record = candidates[index];
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new("verifying", index, candidates.Length));
            var attemptCount = 1;
            var syncStateStarted = false;
            try
            {
                var packagePath = ResolveContained(vault.RootPath, record.PackageRelativePath);
                using var packageRoot = StableDirectoryTree.OpenReadOnlyNoFollowPath(packagePath);
                var retained = await LocalRecoveryVerifier.RetainVerifiedContentAsync(
                    packageRoot, record.RecoveryPointId, _timeProvider.GetUtcNow(),
                    _limits, cancellationToken);
                await using var snapshot = retained.Snapshot;
                var verification = retained.Evidence;
                var localManifest = retained.Manifest;
                if (verification.VerifiedFileCount != record.FileCount ||
                    verification.VerifiedBytes != record.TotalBytes)
                    throw new HostedSyncIntegrityException();

                var remote = new HostedSyncManifest(
                    "1.0", record.RecoveryPointId, record.RecoveryPointId,
                    localManifest.CandidateKey, localManifest.PluginId,
                    localManifest.CreatedAt, record.FileCount, record.TotalBytes,
                    localManifest.Files.Select(file => new HostedSyncFile(
                        file.RelativePath, file.Size, file.Sha256)).ToArray());
                var remoteBytes = JsonSerializer.SerializeToUtf8Bytes(remote, JsonOptions);
                var remoteDigest = Convert.ToHexStringLower(SHA256.HashData(remoteBytes));
                attemptCount = await queue.BeginSyncAsync(record.RecoveryPointId, request.OrganizationId,
                    request.VenueId, remoteDigest, _timeProvider.GetUtcNow(), cancellationToken);
                syncStateStarted = true;

                var upload = await UploadAsync(request, snapshot, remote, remoteDigest,
                    async sessionId => await queue.RecordSyncSessionAsync(
                        record.RecoveryPointId, sessionId, _timeProvider.GetUtcNow(),
                        cancellationToken), progress, index, candidates.Length,
                    cancellationToken);
                var receipt = upload.Receipt;
                ValidateReceipt(receipt, request, remote, remoteDigest);
                var receiptBytes = JsonSerializer.SerializeToUtf8Bytes(receipt, JsonOptions);
                await queue.CompleteSyncAsync(record.RecoveryPointId,
                    Convert.ToHexStringLower(SHA256.HashData(receiptBytes)),
                    receipt.CompletedAt, CancellationToken.None);
                synchronized++;
                bytes += record.TotalBytes;
            }
            catch (OperationCanceledException)
            {
                await queue.RecordSyncRetryAsync(record.RecoveryPointId, "cancelled",
                    _timeProvider.GetUtcNow(), _timeProvider.GetUtcNow(), CancellationToken.None);
                throw;
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                retry++;
                var delaySeconds = Math.Min(1_800,
                    30 * Math.Pow(2, Math.Min(attemptCount - 1, 6)));
                await queue.RecordSyncRetryAsync(record.RecoveryPointId, "hosted_unavailable",
                    _timeProvider.GetUtcNow().AddSeconds(delaySeconds), _timeProvider.GetUtcNow(),
                    CancellationToken.None);
            }
            catch (HostedSyncPolicyException exception)
            {
                attention++;
                await queue.RecordSyncAttentionAsync(record.RecoveryPointId,
                    exception.Code, _timeProvider.GetUtcNow(), CancellationToken.None);
            }
            catch (Exception exception) when (
                exception is LocalEngineException or HostedSyncIntegrityException or
                JsonException || !syncStateStarted &&
                exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                attention++;
                var recorded = await queue.RecordSyncAttentionAsync(record.RecoveryPointId,
                    "sync_integrity_rejected", _timeProvider.GetUtcNow(), CancellationToken.None);
                if (!recorded)
                    await queue.RecordQueuedIntegrityFailureAsync(record.RecoveryPointId,
                        _timeProvider.GetUtcNow(), CancellationToken.None);
            }
            progress?.Report(new("synchronizing", index + 1, candidates.Length));
        }

        var status = attention > 0 ? "attention" : retry > 0 ? "retry_scheduled" :
            candidates.Length == 0 ? "queued" : "synchronized";
        return new(synchronized, retry, attention, bytes, status);
    }

    private async Task<(HostedSyncReceipt Receipt, string SessionId)> UploadAsync(
        LocalSyncRequest request,
        StableSourceSnapshot snapshot,
        HostedSyncManifest manifest,
        string manifestDigest,
        Func<string, Task> recordSession,
        IProgress<LocalSyncProgress>? progress,
        int itemIndex,
        int itemCount,
        CancellationToken cancellationToken)
    {
        var root = RouteRoot(request, manifest.RecoveryPointId);
        var begin = await SendAsync<HostedSyncBeginRequest, HostedSyncBeginResponse>(
            HttpMethod.Post, new Uri(root + "/begin"),
            new(manifest, manifestDigest), request.AccessToken, cancellationToken);
        if (!ValidOpaque(begin.SessionId) || begin.MaximumChunkBytes is < 1 or > MaximumChunkBytes)
            throw new HostedSyncIntegrityException();
        await recordSession(begin.SessionId);

        for (var fileIndex = 0; fileIndex < manifest.Files.Count; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = manifest.Files[fileIndex];
            var state = await GetAsync<HostedSyncFileStateResponse>(
                new Uri(root + $"/sessions/{begin.SessionId}/files?path=" +
                    Uri.EscapeDataString(file.RelativePath)),
                request.AccessToken, cancellationToken);
            if (state.NextOffset < 0 || state.NextOffset > file.Size)
                throw new HostedSyncIntegrityException();
            var stream = snapshot.GetFile(file.RelativePath);
            stream.Position = state.NextOffset;
            var offset = state.NextOffset;
            var buffer = new byte[begin.MaximumChunkBytes];
            while (offset < file.Size)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = await stream.ReadAsync(buffer.AsMemory(
                    0, (int)Math.Min(buffer.Length, file.Size - offset)), cancellationToken);
                if (count == 0) throw new HostedSyncIntegrityException();
                var chunk = buffer.AsMemory(0, count).ToArray();
                var digest = Convert.ToHexStringLower(SHA256.HashData(chunk));
                using var message = new HttpRequestMessage(HttpMethod.Put,
                    new Uri(root + $"/sessions/{begin.SessionId}/files?path=" +
                        Uri.EscapeDataString(file.RelativePath) + $"&offset={offset}"));
                Authorize(message, request.AccessToken);
                message.Headers.Add("X-ShowVault-Chunk-Sha256", digest);
                message.Content = new ByteArrayContent(chunk);
                message.Content.Headers.ContentType = new("application/octet-stream");
                using var response = await _client.SendAsync(message, cancellationToken);
                if (!response.IsSuccessStatusCode) await ThrowForResponseAsync(response,
                    cancellationToken);
                offset += count;
                progress?.Report(new("uploading", itemIndex + fileIndex, itemCount + manifest.Files.Count));
            }
        }
        var receipt = await SendAsync<object, HostedSyncReceipt>(HttpMethod.Post,
            new Uri(root + $"/sessions/{begin.SessionId}/commit"), new { },
            request.AccessToken, cancellationToken);
        return (receipt, begin.SessionId);
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        HttpMethod method, Uri uri, TRequest payload, string token,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        Authorize(message, token);
        using var response = await _client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode) await ThrowForResponseAsync(response,
            cancellationToken);
        var envelope = await response.Content.ReadFromJsonAsync<HostedSyncEnvelope<TResponse>>(
            JsonOptions, cancellationToken) ?? throw new HostedSyncIntegrityException();
        if (envelope.Status != "success" || envelope.Version != "1.0")
            throw new HostedSyncIntegrityException();
        return envelope.Payload;
    }

    private async Task<TResponse> GetAsync<TResponse>(
        Uri uri, string token, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        Authorize(message, token);
        using var response = await _client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode) await ThrowForResponseAsync(response,
            cancellationToken);
        var envelope = await response.Content.ReadFromJsonAsync<HostedSyncEnvelope<TResponse>>(
            JsonOptions, cancellationToken) ?? throw new HostedSyncIntegrityException();
        if (envelope.Status != "success" || envelope.Version != "1.0")
            throw new HostedSyncIntegrityException();
        return envelope.Payload;
    }

    private static void Authorize(HttpRequestMessage message, string token) =>
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task ThrowForResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var status = response.StatusCode;
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
            throw new HttpRequestException("Hosted synchronization is temporarily unavailable.");
        if (status == HttpStatusCode.Conflict)
        {
            try
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
                    JsonOptions, cancellationToken);
                if (problem.TryGetProperty("code", out var codeElement))
                {
                    var code = codeElement.GetString();
                    if (code is "commercial_access_required" or "quota_exceeded")
                        throw new HostedSyncPolicyException(code);
                }
            }
            catch (JsonException) { }
        }
        throw new HostedSyncIntegrityException();
    }

    private static bool IsRetryable(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException;

    private static Uri RouteRoot(LocalSyncRequest request, string recoveryPointId) =>
        new(request.ApiBaseUri,
            $"/api/v1/organizations/{request.OrganizationId:D}/venues/{request.VenueId:D}" +
            $"/hosted-sync/{recoveryPointId}");

    private static void ValidateReceipt(
        HostedSyncReceipt receipt,
        LocalSyncRequest request,
        HostedSyncManifest manifest,
        string manifestDigest)
    {
        if (receipt.FormatVersion != "1.0" ||
            receipt.OrganizationId != request.OrganizationId ||
            receipt.VenueId != request.VenueId ||
            receipt.RecoveryPointId != manifest.RecoveryPointId ||
            receipt.ManifestDigest != manifestDigest ||
            receipt.FileCount != manifest.FileCount || receipt.TotalBytes != manifest.TotalBytes ||
            receipt.Objects.Count != manifest.Files.Count)
            throw new HostedSyncIntegrityException();
        var expected = manifest.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal);
        var actual = receipt.Objects.OrderBy(file => file.RelativePath, StringComparer.Ordinal);
        if (!expected.Zip(actual).All(pair => pair.First.RelativePath == pair.Second.RelativePath &&
                pair.First.Size == pair.Second.Size && pair.First.Sha256 == pair.Second.Sha256))
            throw new HostedSyncIntegrityException();
    }

    private static string ResolveContained(string root, string relative)
    {
        var segments = relative.Split('/');
        if (segments.Length != 3 || segments.Any(segment => segment is "" or "." or ".."))
            throw new LocalEngineException("The queued package path is invalid.");
        var path = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var comparison = OperatingSystem.IsWindows() ?
            StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) +
                Path.DirectorySeparatorChar, comparison))
            throw new LocalEngineException("The queued package path escapes the vault.");
        return path;
    }

    private static void ValidateRequest(LocalSyncRequest request)
    {
        if (request.OrganizationId == Guid.Empty || request.VenueId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.AccessToken) || request.AccessToken.Length > 16_384 ||
            request.AccessToken != request.AccessToken.Trim() ||
            request.AccessToken.Any(char.IsControl) ||
            !Path.IsPathFullyQualified(request.SelectedVault) ||
            request.SelectedVault.Length > 8_192 || request.ApiBaseUri.UserInfo.Length != 0 ||
            request.ApiBaseUri.PathAndQuery != "/" || request.ApiBaseUri.Fragment.Length != 0 ||
            request.ApiBaseUri.Scheme is not ("https" or "http") ||
            request.ApiBaseUri.Scheme == "http" && !request.ApiBaseUri.IsLoopback)
            throw new LocalEngineException("The synchronization request is invalid.");
    }

    private static bool ValidOpaque(string value) =>
        value.Length is >= 16 and <= 128 && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private sealed class HostedSyncIntegrityException : Exception;
    private sealed class HostedSyncPolicyException(string code) : Exception
    {
        public string Code { get; } = code;
    }
}
