using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShowVault.AccountPortal.Clients;

namespace ShowVault.AccountPortal.Pages.Organizations;

[Authorize]
public sealed class SelectModel(ShowVaultAccountClient client) : PageModel
{
    public IReadOnlyList<OrganizationView> Organizations { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken token) =>
        Organizations = await client.OrganizationsAsync(token);
}
