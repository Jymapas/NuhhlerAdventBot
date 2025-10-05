namespace WebApi.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealth(this IEndpointRouteBuilder app)
        => app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
}
