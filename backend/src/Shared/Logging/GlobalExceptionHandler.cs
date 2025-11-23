using System;
using System.Threading.Tasks;
using Serilog;

namespace Shared.Logging;

public static class GlobalExceptionHandler
{
    public static void Register(string component)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception ?? new Exception("Unhandled exception");
            Log.Fatal(exception, "{Component} AppDomain unhandled exception", component);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "{Component} unobserved task exception", component);
            args.SetObserved();
        };
    }
}
