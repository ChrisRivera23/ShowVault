namespace ShowVault.AccountPortal.Security;

public sealed class PortalOriginMiddleware(RequestDelegate next, Uri origin)
{
    private readonly HostString _host = origin.IsDefaultPort
        ? new HostString(origin.Host)
        : new HostString(origin.Host, origin.Port);

    public Task InvokeAsync(HttpContext context)
    {
        if (!string.Equals(context.Request.Scheme, origin.Scheme,
                StringComparison.OrdinalIgnoreCase) || context.Request.Host != _host)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }
        return next(context);
    }
}
