using System.Text.Json;
using LocalBlast.Models;

namespace LocalBlast.Services;

public static class AnalysisHistoryService
{
    private const int MaximumEntries = 30;
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HelixBlast", "history.json");

    public static async Task SaveAsync(AnalysisHistoryEntry entry)
    {
        var entries = Load().Prepend(entry).Take(MaximumEntries).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static IReadOnlyList<AnalysisHistoryEntry> Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<List<AnalysisHistoryEntry>>(File.ReadAllText(FilePath)) ?? []
                : [];
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not read the analysis history.", ex);
            return [];
        }
    }

    public static string GetStoragePath() => FilePath;
}
