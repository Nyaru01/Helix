using LocalBlast.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace LocalBlast.Services;

public sealed class NativeBlastService
{
    private const string BlastVersion = "2.17.0";
    private const string BundleResourceName = "LocalBlast.Resources.blast-win64-2.17.0.zip";
    private readonly Dictionary<string, BlastExecutable> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _extractLock = new(1, 1);

    private static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HelixBlast", "blast", BlastVersion, "bin");

    private static bool HasRequiredExecutables(string directory) =>
        new[] { "blastn.exe", "blastp.exe", "tblastn.exe", "makeblastdb.exe" }
            .All(name => File.Exists(Path.Combine(directory, name)));

    public async Task<(bool Ok, string Message)> CheckAsync(string program, CancellationToken cancellationToken = default)
    {
        try
        {
            var exe = await ResolveProgramAsync(program, cancellationToken);
            return (true,
                $"{exe.Version}\n" +
                $"Native Windows executable: {exe.Path}\n" +
                "Mode: bundled NCBI BLAST+ (no WSL required)");
        }
        catch (Win32Exception ex)
        {
            return (false,
                "Bundled BLAST could not start on this Windows installation.\n\n" +
                "NCBI documents a Microsoft Visual C++ runtime dependency for Windows BLAST+. " +
                "Install the current Microsoft Visual C++ 2015-2022 Redistributable (x64) and try again.\n\n" +
                ex.Message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<BlastExecutable> ResolveProgramAsync(string program, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(program, out var cached) && File.Exists(cached.Path))
            return cached;

        if (program is not ("blastn" or "blastp" or "tblastn"))
            throw new InvalidOperationException($"Unsupported BLAST program: {program}");

        await EnsureBundledBlastAsync(cancellationToken);

        var path = Path.Combine(InstallDirectory, program + ".exe");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Bundled {program}.exe was not found after extraction.", path);

        var result = await RunProcessAsync(path, new[] { "-version" }, cancellationToken);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.Stderr) ? result.Stdout : result.Stderr;
            throw new InvalidOperationException($"{program} could not start.\n\n{detail.Trim()}");
        }

        var version = (string.IsNullOrWhiteSpace(result.Stdout) ? result.Stderr : result.Stdout)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? $"{program} {BlastVersion}";

        var found = new BlastExecutable(path, version);
        _cache[program] = found;
        return found;
    }

    public async Task<string> RunBlastAsync(
        BlastExecutable exe,
        string queryPath,
        string subjectPath,
        double evalue,
        CancellationToken cancellationToken)
    {
        const string outfmt = "6 sseqid pident qcovhsp length qlen slen evalue bitscore qstart qend sstart send qseq sseq";

        var args = new[]
        {
            "-query", queryPath,
            "-subject", subjectPath,
            "-evalue", evalue.ToString(CultureInfo.InvariantCulture),
            "-max_target_seqs", "50",
            "-outfmt", outfmt
        };

        var result = await RunProcessAsync(exe.Path, args, cancellationToken);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.Stderr) ? result.Stdout : result.Stderr;
            throw new InvalidOperationException(detail.Trim().Length > 0 ? detail.Trim() : "BLAST failed.");
        }

        return result.Stdout;
    }

    public async Task<string> RunBlastDatabaseAsync(BlastExecutable exe, string queryPath, string databasePath,
        double evalue, CancellationToken cancellationToken)
    {
        const string outfmt = "6 sseqid pident qcovhsp length qlen slen evalue bitscore qstart qend sstart send qseq sseq";
        var result = await RunProcessAsync(exe.Path, new[] { "-query", queryPath, "-db", databasePath, "-evalue", evalue.ToString(CultureInfo.InvariantCulture), "-max_target_seqs", "50", "-outfmt", outfmt }, cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException((string.IsNullOrWhiteSpace(result.Stderr) ? result.Stdout : result.Stderr).Trim());
        return result.Stdout;
    }

    public async Task BuildDatabaseAsync(string inputFasta, string databasePath, string databaseType, CancellationToken cancellationToken)
    {
        await EnsureBundledBlastAsync(cancellationToken);
        var makeblastdb = Path.Combine(InstallDirectory, "makeblastdb.exe");
        if (!File.Exists(makeblastdb)) throw new FileNotFoundException("Bundled makeblastdb.exe was not found.", makeblastdb);
        var result = await RunProcessAsync(makeblastdb, new[] { "-in", inputFasta, "-dbtype", databaseType, "-out", databasePath, "-parse_seqids" }, cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException((string.IsNullOrWhiteSpace(result.Stderr) ? result.Stdout : result.Stderr).Trim());
    }

    private async Task EnsureBundledBlastAsync(CancellationToken cancellationToken)
    {
        if (HasRequiredExecutables(InstallDirectory))
            return;

        await _extractLock.WaitAsync(cancellationToken);
        try
        {
            if (HasRequiredExecutables(InstallDirectory))
                return;

            var stagingDirectory = $"{InstallDirectory}.staging-{Guid.NewGuid():N}";
            var backupDirectory = $"{InstallDirectory}.backup-{Guid.NewGuid():N}";
            var installed = false;
            Directory.CreateDirectory(stagingDirectory);

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                await using var resource = assembly.GetManifestResourceStream(BundleResourceName)
                    ?? throw new InvalidOperationException(
                    "The bundled NCBI BLAST+ resource is missing. Rebuild Helix Blast with build-exe.bat.");

                using var archive = new ZipArchive(resource, ZipArchiveMode.Read, leaveOpen: false);
                var stagingRoot = Path.GetFullPath(stagingDirectory) + Path.DirectorySeparatorChar;

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    var destination = Path.GetFullPath(Path.Combine(stagingDirectory, entry.FullName));
                    if (!destination.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Invalid path detected in the embedded BLAST archive.");

                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination, overwrite: true);
                }

                if (!HasRequiredExecutables(stagingDirectory))
                    throw new InvalidOperationException("The extracted BLAST bundle is incomplete.");

                Directory.CreateDirectory(Path.GetDirectoryName(InstallDirectory)!);
                if (Directory.Exists(InstallDirectory))
                    Directory.Move(InstallDirectory, backupDirectory);

                Directory.Move(stagingDirectory, InstallDirectory);
                installed = true;
                AppLogger.Info($"Installed bundled NCBI BLAST+ {BlastVersion}.");
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                    Directory.Delete(stagingDirectory, recursive: true);
                if (installed && Directory.Exists(backupDirectory))
                    Directory.Delete(backupDirectory, recursive: true);
            }
        }
        finally
        {
            _extractLock.Release();
        }
    }

    private static async Task<CommandResult> RunProcessAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory
        };

        // Disable any network usage reporting from command-line tooling.
        psi.Environment["BLAST_USAGE_REPORT"] = "false";

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start {Path.GetFileName(executable)}.");
        using var registration = cancellationToken.Register(() => TryKill(process));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new CommandResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort during cancellation.
        }
    }

    private sealed record CommandResult(int ExitCode, string Stdout, string Stderr);
}
