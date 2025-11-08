using System.Collections.Generic;
using Serilog.Context;

namespace Shared.Logging;

public static class LogScopes
{
    public static IDisposable WithUpdate(long? updateId, long? userId, string? username)
    {
        var d1 = LogContext.PushProperty("updateId", updateId ?? 0);
        var d2 = LogContext.PushProperty("userId", userId ?? 0);
        var d3 = LogContext.PushProperty("username", username ?? string.Empty);
        return new CompositeDisposable(d1, d2, d3);
    }

    public static IDisposable WithCampaign(long? campaignId, string? dateIso)
    {
        var d1 = LogContext.PushProperty("campaignId", campaignId ?? 0);
        var d2 = LogContext.PushProperty("dateIso", dateIso ?? string.Empty);
        return new CompositeDisposable(d1, d2);
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> _items;

        public CompositeDisposable(params IDisposable[] items)
        {
            _items = items;
        }

        public void Dispose()
        {
            foreach (var item in _items)
            {
                item.Dispose();
            }
        }
    }
}
