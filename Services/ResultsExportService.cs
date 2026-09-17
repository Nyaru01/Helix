using LocalBlast.Models;
using System.Globalization;
using System.Net;
using System.Text;

namespace LocalBlast.Services;

public static class ResultsExportService
{
    public static async Task ExportPdfAsync(string path, string queryLabel, string program, double minimumIdentity,
        double minimumCoverage, IEnumerable<BlastHit> hits)
    {
        // Lightweight offline PDF: it uses exactly the same report data as the HTML export,
        // without requiring a browser, cloud service, or additional native runtime.
        var lines = new List<string>
        {
            "HELIX BLAST — LOCAL SEQUENCE ANALYSIS",
            $"Report · {queryLabel}",
            $"Program: {program}    Minimum identity: {minimumIdentity:g}%    Minimum HSP coverage: {minimumCoverage:g}%",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}", ""
        };
        lines.AddRange(hits.Select(h => $"{h.GenomeName}  |  {h.Status}  |  Identity {h.IdentityDisplay}  |  Coverage {h.CoverageDisplay}  |  {h.SubjectId}"));
        await File.WriteAllBytesAsync(path, SimplePdf.Create(lines));
    }
    public static async Task ExportCsvAsync(string path, string queryLabel, string program, double minimumIdentity,
        double minimumCoverage, IEnumerable<BlastHit> hits)
    {
        await using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        await writer.WriteLineAsync($"# Helix Blast export; generated_utc={DateTime.UtcNow:O}; query={Escape(queryLabel)}; program={program}; minimum_identity={minimumIdentity.ToString(CultureInfo.InvariantCulture)}; minimum_hsp_coverage={minimumCoverage.ToString(CultureInfo.InvariantCulture)}");
        await writer.WriteLineAsync("Genome;Status;SubjectId;IdentityPercent;HspCoveragePercent;AlignmentLength;QueryLength;SubjectLength;Evalue;BitScore;QueryStart;QueryEnd;SubjectStart;SubjectEnd;GenomeFile;Error");

        foreach (var hit in hits)
        {
            var values = new[]
            {
                hit.GenomeName, hit.Status, hit.SubjectId, Number(hit.Identity), Number(hit.Coverage),
                hit.AlignmentLength?.ToString(CultureInfo.InvariantCulture) ?? "", hit.QueryLength?.ToString(CultureInfo.InvariantCulture) ?? "",
                hit.SubjectLength?.ToString(CultureInfo.InvariantCulture) ?? "", Number(hit.Evalue), Number(hit.BitScore),
                hit.QueryStart?.ToString(CultureInfo.InvariantCulture) ?? "", hit.QueryEnd?.ToString(CultureInfo.InvariantCulture) ?? "",
                hit.SubjectStart?.ToString(CultureInfo.InvariantCulture) ?? "", hit.SubjectEnd?.ToString(CultureInfo.InvariantCulture) ?? "",
                hit.GenomeFile, hit.Error
            };
            await writer.WriteLineAsync(string.Join(';', values.Select(Escape)));
        }
    }

    public static async Task ExportHtmlAsync(string path, string queryLabel, string program, double minimumIdentity,
        double minimumCoverage, IEnumerable<BlastHit> hits)
    {
        var rows = hits.ToList();
        var present = rows.Count(hit => hit.Status == "Present");
        var below = rows.Count(hit => hit.Status == "Below thresholds");
        var noHit = rows.Count(hit => hit.Status == "No hit");
        var errors = rows.Count(hit => hit.Status == "Error");
        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>Helix Blast report</title><style>body{margin:0;background:#070c18;color:#eaf2ff;font:15px Segoe UI,Arial,sans-serif}.hero{padding:54px max(7vw,32px);background:radial-gradient(circle at 80% 0,#105c70,#10182c 52%,#070c18)}h1{font-size:38px;margin:0 0 8px}.accent{color:#3ce1df}.muted{color:#a5b6cb}.wrap{max-width:1280px;margin:auto;padding:28px}.cards{display:grid;grid-template-columns:repeat(4,minmax(140px,1fr));gap:14px;margin:-30px 0 28px}.card{padding:18px;border:1px solid #293954;border-radius:14px;background:#101a2d;box-shadow:0 12px 30px #0004}.metric{font-size:30px;font-weight:700}.label{color:#9fb1c9;margin-top:4px}.details{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;background:#0d1627;border-radius:14px;padding:18px;margin-bottom:24px}.details b{display:block;color:#78f1e8;margin-bottom:4px}table{width:100%;border-collapse:collapse;background:#101a2d;border-radius:14px;overflow:hidden}th{text-align:left;padding:13px;background:#17243d;color:#8ff6ef;font-size:12px;text-transform:uppercase;letter-spacing:.04em}td{padding:12px;border-top:1px solid #22324d}tr.present td:first-child{border-left:4px solid #2bd3a3}tr.below td:first-child{border-left:4px solid #f4b860}tr.nohit td:first-child{border-left:4px solid #74839a}tr.error td:first-child{border-left:4px solid #ec6b77}.pill{border-radius:99px;padding:4px 8px;font-size:12px;font-weight:600;white-space:nowrap}.present .pill{background:#123d35;color:#71f0c7}.below .pill{background:#4a3517;color:#ffd287}.nohit .pill{background:#263447;color:#c5d0df}.error .pill{background:#4c1c27;color:#ff9dac}.print{position:absolute;right:7vw;top:28px;border:1px solid #5ce6de;border-radius:8px;padding:9px 14px;color:#dffffb;background:#163447;cursor:pointer}@media print{body{background:white;color:#182231}.hero,.card,table,.details{background:white;color:#182231;box-shadow:none}.print{display:none}.muted,.label{color:#536174}th{background:#eaf3f5;color:#163447}}@media(max-width:850px){.cards,.details{grid-template-columns:repeat(2,1fr)}.wrap{padding:16px}table{font-size:12px}}</style></head><body>");
        html.AppendLine($"<header class=\"hero\"><button class=\"print\" onclick=\"window.print()\">Print / Save PDF</button><div class=\"muted\">HELIX BLAST · LOCAL SEQUENCE ANALYSIS</div><h1>Analysis report <span class=\"accent\">· {Html(queryLabel)}</span></h1><div class=\"muted\">Generated {DateTime.Now:yyyy-MM-dd HH:mm} · All data processed locally</div></header>");
        html.AppendLine($"<main class=\"wrap\"><section class=\"cards\"><div class=\"card\"><div class=\"metric\">{rows.Count}</div><div class=\"label\">Samples analysed</div></div><div class=\"card\"><div class=\"metric accent\">{present}</div><div class=\"label\">Present</div></div><div class=\"card\"><div class=\"metric\">{below}</div><div class=\"label\">Below thresholds</div></div><div class=\"card\"><div class=\"metric\">{noHit + errors}</div><div class=\"label\">No hit / errors</div></div></section>");
        html.AppendLine($"<section class=\"details\"><div><b>Program</b>{Html(program)}</div><div><b>Minimum identity</b>{minimumIdentity:g}%</div><div><b>Minimum HSP coverage</b>{minimumCoverage:g}%</div><div><b>Query</b>{Html(queryLabel)}</div></section><table><thead><tr><th>Sample</th><th>Status</th><th>Identity</th><th>Coverage</th><th>Subject / contig</th><th>Coordinates</th><th>E-value</th><th>Bit score</th></tr></thead><tbody>");
        foreach (var hit in rows)
        {
            var css = hit.Status switch { "Present" => "present", "Below thresholds" => "below", "No hit" => "nohit", _ => "error" };
            html.AppendLine($"<tr class=\"{css}\"><td>{Html(hit.GenomeName)}</td><td><span class=\"pill\">{Html(hit.Status)}</span></td><td>{Html(hit.IdentityDisplay)}</td><td>{Html(hit.CoverageDisplay)}</td><td>{Html(hit.SubjectId)}</td><td>{Html(hit.CoordinatesDisplay)}</td><td>{Html(hit.EvalueDisplay)}</td><td>{Html(hit.BitScoreDisplay)}</td></tr>");
        }
        html.AppendLine("</tbody></table><p class=\"muted\">Coverage represents the best HSP coverage returned by BLAST. This report is self-contained and can be opened in any modern browser.</p></main></body></html>");
        await File.WriteAllTextAsync(path, html.ToString(), new UTF8Encoding(false));
    }

    private static string Number(double? value) => value?.ToString("G17", CultureInfo.InvariantCulture) ?? "";
    private static string Html(string value) => WebUtility.HtmlEncode(value);
    private static string Escape(string value) => value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
        ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}

internal static class SimplePdf
{
    public static byte[] Create(IReadOnlyList<string> lines)
    {
        var pages = lines.Chunk(42).ToList();
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "" };
        var pageIds = new List<int>();
        foreach (var page in pages)
        {
            var content = "BT\n/F1 10 Tf\n50 790 Td\n" + string.Join("\n", page.Select((line, i) => $"({Escape(line)}) Tj" + (i < page.Length - 1 ? "\n0 -17 Td" : ""))) + "\nET";
            var contentId = objects.Count + 2;
            pageIds.Add(objects.Count + 1);
            objects.Add($"<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 {contentId + 1} 0 R >> >> /MediaBox [0 0 595 842] /Contents {contentId} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        }
        objects[1] = $"<< /Type /Pages /Kids [{string.Join(' ', pageIds.Select(id => $"{id} 0 R"))}] /Count {pageIds.Count} >>";
        var builder = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++) { offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString())); builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var start = Encoding.ASCII.GetByteCount(builder.ToString()); builder.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) builder.Append($"{offset:D10} 00000 n \n");
        builder.Append($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{start}\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
    private static string Escape(string value) => new string(value.Where(c => c is >= ' ' and <= '~').ToArray()).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
