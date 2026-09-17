using LocalBlast.Models;

namespace LocalBlast.Services;

/// <summary>Application-level workflow kept independent from WPF controls.</summary>
public sealed class LocalBlastAnalysisService
{
    private readonly BlastEngine _engine = new();

    public Task<(bool Ok, string Message)> CheckEnvironmentAsync(string program, CancellationToken cancellationToken = default)
        => _engine.CheckEnvironmentAsync(program, cancellationToken);

    public PreparedAnalysis Prepare(AnalysisRequest request)
    {
        if (!Directory.Exists(request.FolderPath))
            throw new InvalidOperationException("Choose a valid FASTA collection folder.");
        if (request.MinimumIdentity is < 0 or > 100)
            throw new InvalidOperationException("Minimum identity must be between 0 and 100.");
        if (request.MinimumCoverage is < 0 or > 100)
            throw new InvalidOperationException("Minimum query coverage must be between 0 and 100.");
        if (request.Evalue <= 0)
            throw new InvalidOperationException("Enter a valid E-value such as 1e-10.");

        var (queryType, subjectType) = request.Program switch
        {
            "tblastn" => (SequenceType.Protein, SequenceType.Nucleotide),
            "blastp" => (SequenceType.Protein, SequenceType.Protein),
            "blastn" => (SequenceType.Nucleotide, SequenceType.Nucleotide),
            _ => throw new InvalidOperationException($"Unsupported BLAST program: {request.Program}")
        };
        var (header, sequence) = FastaService.NormalizeQuery(request.QueryText, queryType);
        var (genomes, ignored) = FastaService.DiscoverFastaFiles(request.FolderPath, subjectType);
        if (genomes.Count == 0)
            throw new InvalidOperationException("No compatible FASTA files were detected in the selected folder.");

        return new PreparedAnalysis(request, header, sequence, genomes, ignored);
    }

    public async Task SearchAsync(PreparedAnalysis analysis, IProgress<SearchProgress> progress, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "HelixBlast", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var queryPath = Path.Combine(directory, "query.fasta");
            await FastaService.WriteQueryAsync(queryPath, analysis.QueryHeader, analysis.QuerySequence);
            await _engine.SearchAsync(analysis.Request.Program, queryPath, analysis.GenomeFiles,
                analysis.Request.MinimumIdentity, analysis.Request.MinimumCoverage, analysis.Request.Evalue,
                progress, cancellationToken);
        }
        finally
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Could not remove a temporary search directory.", ex);
            }
        }
    }

    public Task PrepareIndexesAsync(PreparedAnalysis analysis, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
        => _engine.PrepareIndexesAsync(analysis.Request.Program, analysis.GenomeFiles, progress, cancellationToken);
}
