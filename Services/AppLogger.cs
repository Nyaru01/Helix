using System.Globalization;
using System.Text;

namespace LocalBlast.Services;

public static class AppLogger
{
    private static readonly object Gate = new();

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HelixBlast", "logs");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var file = Path.Combine(LogDirectory, $"helixblast-{DateTime.UtcNow:yyyyMMdd}.log");
            var detail = exception is null ? "" : $"{Environment.NewLine}{exception}";
            var entry = $"{DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)} [{level}] {message}{detail}{Environment.NewLine}";
            lock (Gate)
                File.AppendAllText(file, entry, new UTF8Encoding(false));
        }
        catch
        {
            // Logging must never prevent a local search from running.
        }
    }
}
