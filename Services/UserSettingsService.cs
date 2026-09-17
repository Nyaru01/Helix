using System.Text.Json;

namespace LocalBlast.Services;

public sealed record UserSettings(
    string FolderPath = "",
    string Program = "blastn",
    string MinimumIdentity = "80",
    string MinimumCoverage = "80",
    string Evalue = "1e-10");

public static class UserSettingsService
{
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HelixBlast", "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new UserSettings();

            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath)) ?? new UserSettings();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not load user settings.", ex);
            return new UserSettings();
        }
    }

    public static void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not save user settings.", ex);
        }
    }
}
