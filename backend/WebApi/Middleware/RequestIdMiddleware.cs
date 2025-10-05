using System.Diagnostics;

namespace WebApi.Middleware;

public class RequestIdMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        var reqId = ctx.Request.Headers["X-Request-ID"].FirstOrDefault() ?? Activity.Current?.Id ?? Guid.NewGuid().ToString("n");
        ctx.Response.Headers["X-Request-ID"] = reqId;
        ctx.Items["RequestId"] = reqId;
        await next(ctx);
    }
}
