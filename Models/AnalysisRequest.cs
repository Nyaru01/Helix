namespace LocalBlast.Models;

public sealed record AnalysisRequest(
    string Program,
    string QueryText,
    string FolderPath,
    double MinimumIdentity,
    double MinimumCoverage,
    double Evalue);

public sealed record PreparedAnalysis(
    AnalysisRequest Request,
    string QueryHeader,
    string QuerySequence,
    IReadOnlyList<string> GenomeFiles,
    IReadOnlyList<string> IgnoredFiles);
