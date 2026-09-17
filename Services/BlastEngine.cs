using LocalBlast.Models;
using System.IO;

namespace LocalBlast.Services;

public sealed class BlastEngine
{
    private readonly NativeBlastService _blast = new();
    private readonly BlastDatabaseService _databases;

    public BlastEngine() => _databases = new BlastDatabaseService(_blast);

    public Task<(bool Ok, string Message)> CheckEnvironmentAsync(
        string program,
        CancellationToken cancellationToken = default)
        => _blast.CheckAsync(program, cancellationToken);

    public async Task PrepareIndexesAsync(string program, IReadOnlyList<string> genomeFiles, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
    {
        var databaseType = program == "blastp" ? "prot" : "nucl";
        var completed = 0;
        foreach (var genome in genomeFiles)
        {
            await _databases.GetOrBuildAsync(genome, databaseType, cancellationToken);
            progress?.Report(new SearchProgress(++completed, genomeFiles.Count, Path.GetFileName(genome), null));
        }
    }

    public async Task SearchAsync(
        string program,
        string queryPath,
        IReadOnlyList<string> genomeFiles,
        double minIdentity,
        double minCoverage,
        double evalue,
        IProgress<SearchProgress> progress,
        CancellationToken cancellationToken)
    {
        var exe = await _blast.ResolveProgramAsync(program, cancellationToken);

        // One BLAST process per file is safe for small collections. Limit parallelism to two
        // processes to make medium-size collections faster without monopolising a workstation.
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Min(2, Math.Max(1, Environment.ProcessorCount / 2))
        };
        var completed = 0;

        await Parallel.ForEachAsync(genomeFiles, options, async (genome, token) =>
        {
            BlastHit hit;

            try
            {
                try
                {
                    var databaseType = program == "blastp" ? "prot" : "nucl";
                    var database = await _databases.GetOrBuildAsync(genome, databaseType, token);
                    var output = await _blast.RunBlastDatabaseAsync(exe, queryPath, database, evalue, token);
                    hit = BlastResultParser.ParseBestHit(genome, output, minIdentity, minCoverage);
                }
                catch (Exception indexException) when (indexException is not OperationCanceledException)
                {
                    // Indexing is an acceleration only. Preserve the original, reliable
                    // per-FASTA BLAST path when a source file cannot be indexed.
                    AppLogger.Error($"Index unavailable for '{Path.GetFileName(genome)}'; using direct FASTA mode.", indexException);
                    var output = await _blast.RunBlastAsync(exe, queryPath, genome, evalue, token);
                    hit = BlastResultParser.ParseBestHit(genome, output, minIdentity, minCoverage);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"BLAST failed for '{Path.GetFileName(genome)}'.", ex);
                hit = new BlastHit
                {
                    GenomeFile = genome,
                    GenomeName = Path.GetFileNameWithoutExtension(genome),
                    Status = "Error",
                    Error = ex.Message
                };
            }

            progress.Report(new SearchProgress(
                Interlocked.Increment(ref completed),
                genomeFiles.Count,
                Path.GetFileName(genome),
                hit));
        });
    }

}
