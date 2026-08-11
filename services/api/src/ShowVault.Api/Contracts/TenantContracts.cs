using ShowVault.Platform.Organizations;

namespace ShowVault.Api.Contracts;

public sealed record CreateOrganizationRequest(string Name, string Slug);
public sealed record OrganizationSummary(Guid Id, string Name, string Slug, OrganizationRole Role);
public sealed record CreateVenueRequest(string Name, string TimeZoneId);
public sealed record VenueSummary(Guid Id, Guid OrganizationId, string Name, string TimeZoneId);
