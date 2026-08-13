using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ShowVault.AccountPortal.Pages;

[Authorize]
public sealed class StepUpModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid OrganizationId { get; set; }

    public IActionResult OnPost()
    {
        var destination = Url.Page("/Organizations/Members", new
        {
            organizationId = OrganizationId
        })!;
        var properties = new AuthenticationProperties { RedirectUri = destination };
        properties.Items["showvault_step_up"] = "1";
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }
}
