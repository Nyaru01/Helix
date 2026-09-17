using System.Security.Cryptography;
using System.Text;

namespace LocalBlast.Services;

/// <summary>Maintains per-file BLAST indexes so repeated local searches do not reparse FASTA files.</summary>
public sealed class BlastDatabaseService
{
    private readonly NativeBlastService _native;
    public BlastDatabaseService(NativeBlastService native) => _native = native;

    public async Task<string> GetOrBuildAsync(string fastaPath, string databaseType, CancellationToken cancellationToken)
    {
        var info = new FileInfo(fastaPath);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{databaseType}"))).ToLowerInvariant()[..20];
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HelixBlast", "databases", key);
        var database = Path.Combine(root, "subject");
        var marker = databaseType == "nucl" ? database + ".nin" : database + ".pin";
        if (File.Exists(marker)) return database;
        Directory.CreateDirectory(root);
        await _native.BuildDatabaseAsync(fastaPath, database, databaseType, cancellationToken);
        return database;
    }

    public static string RootPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HelixBlast", "databases");
}
