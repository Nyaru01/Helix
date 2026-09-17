namespace LocalBlast.Models;

public sealed record AnalysisHistoryEntry(
    DateTimeOffset CreatedAt,
    string QueryLabel,
    string Program,
    double MinimumIdentity,
    double MinimumCoverage,
    string CollectionPath,
    List<BlastHit> Hits);
