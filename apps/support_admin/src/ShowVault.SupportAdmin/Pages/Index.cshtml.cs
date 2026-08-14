using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShowVault.SupportAdmin.Clients;

namespace ShowVault.SupportAdmin.Pages;

[Authorize]
public sealed class IndexModel(ShowVaultSupportClient supportClient,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    public string OrganizationId { get; set; } = string.Empty;
    public SupportOrganizationOverview? Overview { get; private set; }

    public void OnGet() { }

    public IActionResult OnPostSignOut() => SignOut(new AuthenticationProperties
    {
        RedirectUri = "/"
    }, ShowVault.SupportAdmin.Configuration.SupportAdminPortalOptions.CookieScheme);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(OrganizationId, "D", out var organizationId) ||
            organizationId == Guid.Empty)
            return Unavailable("invalid_request");
        try
        {
            Overview = await supportClient.GetOverviewAsync(organizationId, cancellationToken);
            OrganizationId = string.Empty;
            ModelState.Clear();
            logger.LogInformation("Support overview lookup {Outcome}; correlation {CorrelationId}",
                "completed", HttpContext.TraceIdentifier);
            return Page();
        }
        catch (SupportApiUnavailableException)
        {
            return Unavailable("unavailable");
        }
    }

    private PageResult Unavailable(string outcome)
    {
        OrganizationId = string.Empty;
        ModelState.Clear();
        ModelState.AddModelError(string.Empty, "The support overview is unavailable.");
        logger.LogInformation("Support overview lookup {Outcome}; correlation {CorrelationId}",
            outcome, HttpContext.TraceIdentifier);
        return Page();
    }
}
