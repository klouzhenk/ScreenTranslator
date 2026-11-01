using Microsoft.Extensions.Logging;
using Serilog;

namespace ScreenTranslator.Logging;

public static class LoggingServiceCollectionExtensions
{
    public static ILoggingBuilder AddFileLogging(this ILoggingBuilder logging)
    {
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenTranslator",
            "logs");
        Directory.CreateDirectory(logsDir);

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(logsDir, "screentranslator-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        logging.AddSerilog(serilogLogger, dispose: true);
        return logging;
    }
}
