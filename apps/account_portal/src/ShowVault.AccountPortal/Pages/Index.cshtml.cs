using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ShowVault.AccountPortal.Pages;

public sealed class IndexModel : PageModel
{
    public IActionResult OnPost() => Challenge(new AuthenticationProperties
    {
        RedirectUri = Url.Page("/Organizations/Select")
    }, OpenIdConnectDefaults.AuthenticationScheme);
}
