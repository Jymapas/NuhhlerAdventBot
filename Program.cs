using Microsoft.Extensions.Configuration;

namespace NuhhlerAdventBot;

internal class Program
{
    private static void Main(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false)
            .Build();

        var botConfig = config.GetSection("BotConfig").Get<BotConfig>();

        Console.WriteLine(botConfig.BotToken);
    }
}