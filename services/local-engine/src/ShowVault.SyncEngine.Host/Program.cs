using System.Text.Json;
using System.Text.Json.Serialization;
using ShowVault.LocalEngine;

return await SyncEngineHost.RunAsync();

internal static class SyncEngineHost
{
    private const int MaximumRequestCharacters = 32_768;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync()
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        try
        {
            var line = await Console.In.ReadLineAsync(cancellation.Token);
            if (line is null || line.Length is < 2 or > MaximumRequestCharacters)
                return Error("invalid_request");
            HostRequest? request;
            try { request = JsonSerializer.Deserialize<HostRequest>(line, JsonOptions); }
            catch (JsonException) { return Error("invalid_request"); }
            if (!Valid(request)) return Error("invalid_request");
            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation.Token);
            var cancelMonitor = MonitorCancelAsync(operationCancellation);
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                var progress = new SynchronousProgress<LocalSyncProgress>(value =>
                    Write(new HostEnvelope("progress", value, null)));
                var result = await new LocalSyncEngine(client).SynchronizeAsync(
                    new(request!.SelectedVault!, request.OrganizationId!.Value,
                        request.VenueId!.Value, request.AccessToken!,
                        new Uri(request.ApiBaseUrl!, UriKind.Absolute)),
                    progress, operationCancellation.Token);
                Write(new HostEnvelope("result", result, null));
                return 0;
            }
            finally
            {
                operationCancellation.Cancel();
                try { await cancelMonitor; } catch (OperationCanceledException) { }
            }
        }
        catch (OperationCanceledException) { return Error("cancelled"); }
        catch (LocalEngineException) { return Error("sync_rejected"); }
        catch (Exception exception) when (exception is IOException or JsonException or UriFormatException)
        {
            return Error("sync_failed");
        }
        catch (Exception)
        {
            return Error("sync_failed");
        }
    }

    private static bool Valid(HostRequest? request) =>
        request is { Operation: "synchronize", OrganizationId: not null, VenueId: not null } &&
        request.OrganizationId != Guid.Empty && request.VenueId != Guid.Empty &&
        request.SelectedVault is { Length: > 0 and <= 8_192 } &&
        Path.IsPathFullyQualified(request.SelectedVault) &&
        request.AccessToken is { Length: > 0 and <= 16_384 } &&
        request.ApiBaseUrl is { Length: > 0 and <= 2_048 };

    private static async Task MonitorCancelAsync(CancellationTokenSource cancellation)
    {
        var line = await Console.In.ReadLineAsync(cancellation.Token);
        if (line is null || line.Length > MaximumRequestCharacters) return;
        try
        {
            var request = JsonSerializer.Deserialize<CancelRequest>(line, JsonOptions);
            if (request?.Operation == "cancel") cancellation.Cancel();
        }
        catch (JsonException) { }
    }

    private static int Error(string code)
    {
        Write(new HostEnvelope("error", null, code));
        return 2;
    }

    private static void Write(HostEnvelope envelope)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(envelope, JsonOptions));
        Console.Out.Flush();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record HostRequest(
        string? Operation, string? SelectedVault, Guid? OrganizationId,
        Guid? VenueId, string? AccessToken, string? ApiBaseUrl);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CancelRequest(string? Operation);
    private sealed record HostEnvelope(string Type, object? Payload, string? Code);
    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
