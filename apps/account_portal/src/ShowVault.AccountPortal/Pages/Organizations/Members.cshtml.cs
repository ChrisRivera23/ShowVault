using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShowVault.AccountPortal.Clients;
using ShowVault.AccountPortal.Security;

namespace ShowVault.AccountPortal.Pages.Organizations;

[Authorize]
public sealed class MembersModel(
    ShowVaultAccountClient client,
    OneTimeSecretStore secrets) : PageModel
{
    public Guid OrganizationId { get; private set; }
    public IReadOnlyList<MemberView> Members { get; private set; } = [];
    public IReadOnlyList<InvitationView> Invitations { get; private set; } = [];
    public string? InvitationCode { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid organizationId, string? reveal,
        CancellationToken token)
    {
        OrganizationId = organizationId;
        InvitationCode = reveal is null ? null : secrets.Take(reveal);
        Members = await client.MembersAsync(organizationId, token);
        Invitations = await client.InvitationsAsync(organizationId, token);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(Guid organizationId,
        string displayLabel, string role, CancellationToken token)
    {
        try
        {
            var invitation = await client.CreateInvitationAsync(
                organizationId, displayLabel, role, token);
            var handle = secrets.Put(invitation.InvitationCode);
            return RedirectToPage(new { organizationId, reveal = handle });
        }
        catch (HttpRequestException exception) when (
            exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return RedirectToPage("/StepUp", new { organizationId });
        }
    }

    public async Task<IActionResult> OnPostMutateAsync(Guid organizationId,
        Guid membershipId, string action, long revision, string? role,
        CancellationToken token)
    {
        try
        {
            await client.MutateAsync(organizationId, membershipId, action, revision,
                action == "change_role" ? role : null, token);
            return RedirectToPage(new { organizationId });
        }
        catch (HttpRequestException exception) when (
            exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return RedirectToPage("/StepUp", new { organizationId });
        }
    }

    public async Task<IActionResult> OnPostRevokeInvitationAsync(Guid organizationId,
        Guid invitationId, CancellationToken token)
    {
        try
        {
            await client.RevokeInvitationAsync(organizationId, invitationId, token);
            return RedirectToPage(new { organizationId });
        }
        catch (HttpRequestException exception) when (
            exception.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return RedirectToPage("/StepUp", new { organizationId });
        }
    }
}
