using System.Security.Cryptography;
using System.Text;
using System.Web;
using Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Telegram;

public class TelegramAuth(IConfiguration cfg, IHostEnvironment env) : ITelegramAuth
{
    public (long, string, string) ValidateInitData(string initData)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(initData))
                return DevFallback();

            var token = cfg["TELEGRAM_BOT_TOKEN"] ?? throw new InvalidOperationException("Bot token not configured");

            var parsed = HttpUtility.ParseQueryString(initData);
            var hash = parsed["hash"] ?? throw new UnauthorizedAccessException("Missing hash");
            parsed.Remove("hash");

            var pairs = parsed.AllKeys!
                .Where(k => k is not null)
                .Select(k => $"{k}={parsed[k]}")
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
            var dataCheckString = string.Join("\n", pairs);

            // secret = SHA256(bot_token)
            using var sha = SHA256.Create();
            var secret = sha.ComputeHash(Encoding.UTF8.GetBytes(token));

            using var hmac = new HMACSHA256(secret);
            var comp = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
            var compHex = BitConverter.ToString(comp).Replace("-", "").ToLowerInvariant();

            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(compHex), Encoding.UTF8.GetBytes(hash.ToLowerInvariant())))
                throw new UnauthorizedAccessException("Invalid initData");

            var userJson = parsed["user"] ?? "{}";
            long userId = ExtractLong(userJson, "\"id\":");
            string username = ExtractString(userJson, "\"username\":\"");
            string lang = parsed["lang"] ?? "ru";

            return (userId, username, lang);
        }
        catch (UnauthorizedAccessException) when (env.IsDevelopment())
        {
            return DevFallback();
        }

        (long, string, string) DevFallback()
        {
            if (!env.IsDevelopment())
                throw new UnauthorizedAccessException("Missing initData");

            var fallbackId = cfg.GetValue<long?>("DEV_TELEGRAM_USER_ID") ?? 1;
            var fallbackUsername = cfg["DEV_TELEGRAM_USERNAME"] ?? "dev-user";
            var fallbackLang = cfg["DEV_TELEGRAM_LANG"] ?? "ru";
            return (fallbackId, fallbackUsername, fallbackLang);
        }
    }

    static long ExtractLong(string src, string key)
    {
        return long.TryParse(TakeTill(src, key, ',', '}', ' '), out var v) ? v : 0;
    }


    static string ExtractString(string src, string key)
    {
        var i = src.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return "";
        i += key.Length;
        var j = src.IndexOf('"', i);
        return j > i ? src[i..j] : "";
    }

    static string TakeTill(string src, string key, params char[] stops)
    {
        var i = src.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return "";
        i += key.Length;
        int j = src.Length;
        foreach (var s in stops)
        {
            var k = src.IndexOf(s, i);
            if (k > i) j = Math.Min(j, k);
        }
        return src[i..j];
    }
}
