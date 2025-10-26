using System;
using System.IO;

namespace Shared.Env;

public static class EnvFileLoader
{
    public static void LoadFromAncestors(string fileName = ".env")
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var envPath = Path.Combine(current, fileName);
            if (File.Exists(envPath))
            {
                LoadFromFile(envPath);
                return;
            }

            current = Directory.GetParent(current)?.FullName;
        }
    }

    private static void LoadFromFile(string path)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = trimmed[..separatorIndex].Trim();
            if (string.IsNullOrEmpty(key))
                continue;

            var rawValue = trimmed[(separatorIndex + 1)..].Trim();
            var value = rawValue.Trim('\'','"');

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
