using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShowVault.SupportAdmin.Configuration;

namespace ShowVault.SupportAdmin.Clients;

public sealed class ShowVaultSupportClient(HttpClient client, IHttpContextAccessor contextAccessor,
    IOptions<SupportAdminPortalOptions> options)
{
    private static readonly string[] Roles = ["owner", "admin", "operator", "technician", "viewer"];
    private static readonly string[] States = ["active", "suspended", "removed"];
    private static readonly string[] LicenseStates = ["missing", "pending", "active", "refunded", "revoked"];
    private static readonly string[] SubscriptionStates =
        ["missing", "incomplete", "trialing", "active", "past_due", "unpaid", "paused", "canceled"];
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public async Task<SupportOrganizationOverview> GetOverviewAsync(Guid organizationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = contextAccessor.HttpContext ?? throw new SupportApiUnavailableException();
            var accessToken = await context.GetTokenAsync("access_token");
            if (string.IsNullOrWhiteSpace(accessToken)) throw new SupportApiUnavailableException();
            using var request = new HttpRequestMessage(HttpMethod.Post,
                "api/v1/support/organization-overview");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new { organizationId });
            using var response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.OK ||
                response.Headers.CacheControl?.NoStore != true ||
                !string.Equals(response.Content.Headers.ContentType?.MediaType,
                    "application/json", StringComparison.OrdinalIgnoreCase) ||
                response.Content.Headers.ContentLength > options.Value.MaximumApiResponseBytes)
                throw new SupportApiUnavailableException();
            await response.Content.LoadIntoBufferAsync(options.Value.MaximumApiResponseBytes,
                cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var overview = await JsonSerializer.DeserializeAsync<SupportOrganizationOverview>(
                stream, _json, cancellationToken) ?? throw new SupportApiUnavailableException();
            Validate(overview, organizationId);
            return overview;
        }
        catch (OperationCanceledException) { throw; }
        catch (SupportApiUnavailableException) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or
            InvalidDataException or InvalidOperationException or OverflowException or
            NullReferenceException)
        {
            throw new SupportApiUnavailableException();
        }
    }

    private static void Validate(SupportOrganizationOverview value, Guid requestedId)
    {
        var expectedMembers = Roles.SelectMany(role => States.Select(state => (role, state)));
        if (value.OrganizationId != requestedId || !Bounded(value.DisplayName, 200) ||
            value.Members.Count != 15 || value.Members.Any(item => item.Count < 0) ||
            !value.Members.Select(item => (item.Role, item.State)).SequenceEqual(expectedMembers))
            throw new InvalidDataException();
        var commercial = value.Commercial;
        if (!LicenseStates.Contains(commercial.LicenseState, StringComparer.Ordinal) ||
            !SubscriptionStates.Contains(commercial.SubscriptionState, StringComparer.Ordinal) ||
            !Bounded(commercial.EligibilityReason, 80) ||
            commercial.PlanCode is not null && !Bounded(commercial.PlanCode, 80) ||
            commercial.CommittedBytes < 0 || commercial.ReservedBytes < 0 ||
            commercial.LimitBytes < 0 ||
            checked(commercial.CommittedBytes + commercial.ReservedBytes) > commercial.LimitBytes)
            throw new InvalidDataException();
        var billing = value.BillingAttention;
        if (billing.OpenCount < 0 || billing.ReasonCodes.Count > 8 ||
            billing.ReasonCodes.Distinct(StringComparer.Ordinal).Count() != billing.ReasonCodes.Count ||
            !billing.ReasonCodes.SequenceEqual(billing.ReasonCodes.Order(StringComparer.Ordinal)) ||
            billing.ReasonCodes.Any(reason => !Bounded(reason, 80)))
            throw new InvalidDataException();
        var sync = value.HostedSync.Counts;
        if (sync.Count != 2 || sync[0].Status != "uploading" || sync[1].Status != "completed" ||
            sync.Any(item => item.Count < 0))
            throw new InvalidDataException();
        if (!Utc(commercial.CurrentPeriodEndsAt) || !Utc(commercial.GraceEndsAt) ||
            !Utc(billing.OldestOpenedAt) || !Utc(value.HostedSync.LatestActivityAt) ||
            !Utc(value.Activity.LastAccountActivityAt) ||
            !Utc(value.Activity.LastCommercialActivityAt))
            throw new InvalidDataException();
    }

    private static bool Bounded(string value, int maximum) =>
        value.Length is > 0 && value.Length <= maximum && !value.Any(char.IsControl);

    private static bool Utc(DateTimeOffset? value) => value is null || value.Value.Offset == TimeSpan.Zero;
}
