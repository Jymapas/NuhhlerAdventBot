using NuhhlerAdventBot.BotApi;

namespace NuhhlerAdventBot;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var connect = new Connect();
        await connect.StartBot();
    }
}