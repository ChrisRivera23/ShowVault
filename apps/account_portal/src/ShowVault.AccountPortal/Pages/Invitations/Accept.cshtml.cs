using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShowVault.AccountPortal.Clients;

namespace ShowVault.AccountPortal.Pages.Invitations;

[Authorize]
public sealed class AcceptModel(ShowVaultAccountClient client) : PageModel
{
    [BindProperty]
    public string InvitationCode { get; set; } = "";
    public bool Accepted { get; private set; }

    public void OnGet(bool accepted = false, bool unavailable = false)
    {
        Accepted = accepted;
        if (unavailable) ModelState.AddModelError("", "The invitation is unavailable.");
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken token)
    {
        var code = InvitationCode;
        InvitationCode = "";
        try
        {
            await client.AcceptAsync(code, token);
            return RedirectToPage(new { accepted = true });
        }
        catch (HttpRequestException)
        {
            return RedirectToPage(new { unavailable = true });
        }
    }
}
