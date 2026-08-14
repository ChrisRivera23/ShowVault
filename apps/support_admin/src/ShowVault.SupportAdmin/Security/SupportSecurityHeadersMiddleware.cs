namespace ShowVault.SupportAdmin.Security;

public sealed class SupportSecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Permissions-Policy"] =
                "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";
            return Task.CompletedTask;
        });
        await next(context);
    }
}
