using System.Globalization;

namespace LocalBlast.Models;

public sealed class BlastHit
{
    public string GenomeFile { get; init; } = "";
    public string GenomeName { get; init; } = "";
    public string Status { get; init; } = "";
    public string SubjectId { get; init; } = "";
    public double? Identity { get; init; }
    public double? Coverage { get; init; }
    public int? AlignmentLength { get; init; }
    public int? QueryLength { get; init; }
    public int? SubjectLength { get; init; }
    public double? Evalue { get; init; }
    public double? BitScore { get; init; }
    public int? QueryStart { get; init; }
    public int? QueryEnd { get; init; }
    public int? SubjectStart { get; init; }
    public int? SubjectEnd { get; init; }
    public string QueryAligned { get; init; } = "";
    public string SubjectAligned { get; init; } = "";
    public string Error { get; init; } = "";

    public string IdentityDisplay => Identity is null ? "" : $"{Identity.Value:F2}%";
    public string CoverageDisplay => Coverage is null ? "" : $"{Coverage.Value:F2}%";
    public string EvalueDisplay => Evalue is null ? "" : Evalue.Value == 0 ? "0" : Evalue.Value.ToString("0.##E+0", CultureInfo.InvariantCulture);
    public string BitScoreDisplay => BitScore is null ? "" : BitScore.Value.ToString("F1", CultureInfo.InvariantCulture);
    public string CoordinatesDisplay => SubjectStart is null || SubjectEnd is null
        ? ""
        : $"{SubjectStart} {(SubjectStart <= SubjectEnd ? "→" : "←")} {SubjectEnd}";
}

public sealed record SearchProgress(int Completed, int Total, string CurrentFile, BlastHit? Hit);
public sealed record BlastExecutable(string Path, string Version);
