using LocalBlast.Models;
using System.Text;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LocalBlast.Windows;

public partial class AlignmentWindow : FluentWindow
{
    public AlignmentWindow(BlastHit hit)
    {
        InitializeComponent();
        AlignmentTitleBar.Title = $"BLAST hit · {hit.GenomeName}";

        if (hit.Status == "Error")
        {
            SummaryText.Text = $"{hit.GenomeName}\nError\n{hit.Error}";
            AlignmentTextBox.Text = "";
            return;
        }

        if (hit.Status == "No hit")
        {
            SummaryText.Text = $"{hit.GenomeName}\nNo significant BLAST hit.";
            AlignmentTextBox.Text = "";
            return;
        }

        SummaryText.Text =
            $"{hit.GenomeName}   ·   {hit.Status}\n" +
            $"Identity: {hit.IdentityDisplay}   ·   Query coverage: {hit.CoverageDisplay}   ·   E-value: {hit.EvalueDisplay}   ·   Bit score: {hit.BitScoreDisplay}\n" +
            $"Subject: {hit.SubjectId}   ·   Coordinates: {hit.CoordinatesDisplay}\n" +
            $"File: {hit.GenomeFile}";

        AlignmentTextBox.Text = FormatAlignment(hit);
    }

    private static string FormatAlignment(BlastHit hit, int width = 70)
    {
        if (string.IsNullOrEmpty(hit.QueryAligned) || string.IsNullOrEmpty(hit.SubjectAligned))
            return "No aligned sequence was returned by BLAST.";

        var qseq = hit.QueryAligned;
        var sseq = hit.SubjectAligned;
        var qPos = hit.QueryStart ?? 1;
        var sPos = hit.SubjectStart ?? 1;
        var sStep = (hit.SubjectEnd ?? 0) >= (hit.SubjectStart ?? 0) ? 1 : -1;
        var sb = new StringBuilder();

        for (var start = 0; start < qseq.Length; start += width)
        {
            var count = Math.Min(width, qseq.Length - start);
            var qpart = qseq.Substring(start, count);
            var spart = sseq.Substring(start, Math.Min(count, sseq.Length - start));
            var mid = new StringBuilder(Math.Min(qpart.Length, spart.Length));
            for (var i = 0; i < Math.Min(qpart.Length, spart.Length); i++)
                mid.Append(qpart[i] == spart[i] && qpart[i] != '-' ? '|' : qpart[i] == '-' || spart[i] == '-' ? ' ' : '.');

            var qCount = qpart.Count(c => c != '-');
            var sCount = spart.Count(c => c != '-');
            var qEnd = qCount > 0 ? qPos + qCount - 1 : qPos;
            var sEnd = sCount > 0 ? sPos + sStep * (sCount - 1) : sPos;

            sb.AppendLine($"Query {qPos,-8} {qpart} {qEnd}");
            sb.AppendLine($"{"",14}{mid}");
            sb.AppendLine($"Sbjct {sPos,-8} {spart} {sEnd}");
            sb.AppendLine();

            qPos = qEnd + 1;
            sPos = sEnd + sStep;
        }

        return sb.ToString();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
