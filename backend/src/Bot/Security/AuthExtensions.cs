using Microsoft.Extensions.Configuration;

namespace Bot.Security;

public static class AuthExtensions
{
    public static bool IsAllowedOwner(long telegramUserId, IConfiguration configuration)
    {
        var raw = configuration["OWNER_IDS"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var entries = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            if (long.TryParse(entry, out var parsed) && parsed == telegramUserId)
            {
                return true;
            }
        }

        return false;
    }
}
