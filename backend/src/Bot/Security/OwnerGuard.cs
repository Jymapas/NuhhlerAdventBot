using Microsoft.Extensions.Configuration;

namespace Bot.Security;

public static class OwnerGuard
{
    public static bool IsOwner(long userId, IConfiguration configuration)
    {
        var raw = configuration["OWNER_IDS"];
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (long.TryParse(part, out var parsed) && parsed == userId)
            {
                return true;
            }
        }

        return false;
    }
}
