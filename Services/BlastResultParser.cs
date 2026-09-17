using LocalBlast.Models;
using System.Globalization;

namespace LocalBlast.Services;

public static class BlastResultParser
{
    public static BlastHit ParseBestHit(string genomeFile, string output, double minIdentity, double minCoverage)
    {
        var candidates = new List<ParsedHit>();

        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var c = line.Split('\t');
            if (c.Length < 14)
                continue;

            try
            {
                candidates.Add(new ParsedHit(c[0], double.Parse(c[1], CultureInfo.InvariantCulture), double.Parse(c[2], CultureInfo.InvariantCulture), int.Parse(c[3], CultureInfo.InvariantCulture), int.Parse(c[4], CultureInfo.InvariantCulture), int.Parse(c[5], CultureInfo.InvariantCulture), double.Parse(c[6], CultureInfo.InvariantCulture), double.Parse(c[7], CultureInfo.InvariantCulture), int.Parse(c[8], CultureInfo.InvariantCulture), int.Parse(c[9], CultureInfo.InvariantCulture), int.Parse(c[10], CultureInfo.InvariantCulture), int.Parse(c[11], CultureInfo.InvariantCulture), c[12], c[13]));
            }
            catch (FormatException)
            {
                AppLogger.Info("Ignored a malformed BLAST result row.");
            }
        }

        if (candidates.Count == 0)
        {
            return new BlastHit { GenomeFile = genomeFile, GenomeName = Path.GetFileNameWithoutExtension(genomeFile), Status = "No hit" };
        }

        var passing = candidates.Where(h => h.Identity >= minIdentity && h.Coverage >= minCoverage).ToList();
        var best = (passing.Count > 0 ? passing : candidates)
            .OrderByDescending(h => h.BitScore).ThenByDescending(h => h.Coverage)
            .ThenByDescending(h => h.Identity).ThenBy(h => h.Evalue).First();

        return new BlastHit
        {
            GenomeFile = genomeFile, GenomeName = Path.GetFileNameWithoutExtension(genomeFile),
            Status = passing.Count > 0 ? "Present" : "Below thresholds", SubjectId = best.SubjectId,
            Identity = best.Identity, Coverage = best.Coverage, AlignmentLength = best.AlignmentLength,
            QueryLength = best.QueryLength, SubjectLength = best.SubjectLength, Evalue = best.Evalue,
            BitScore = best.BitScore, QueryStart = best.QueryStart, QueryEnd = best.QueryEnd,
            SubjectStart = best.SubjectStart, SubjectEnd = best.SubjectEnd, QueryAligned = best.QueryAligned,
            SubjectAligned = best.SubjectAligned
        };
    }

    private sealed record ParsedHit(string SubjectId, double Identity, double Coverage, int AlignmentLength,
        int QueryLength, int SubjectLength, double Evalue, double BitScore, int QueryStart, int QueryEnd,
        int SubjectStart, int SubjectEnd, string QueryAligned, string SubjectAligned);
}
