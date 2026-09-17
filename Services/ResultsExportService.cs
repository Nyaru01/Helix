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
        var detectionRate = rows.Count == 0 ? 0 : present * 100.0 / rows.Count;
        var generated = DateTime.Now;
        var rowMarkup = new StringBuilder();
        foreach (var hit in rows)
        {
            var css = hit.Status switch { "Present" => "present", "Below thresholds" => "below", "No hit" => "nohit", _ => "error" };
            var detail = string.IsNullOrWhiteSpace(hit.Error) ? hit.GenomeFile : hit.Error;
            rowMarkup.AppendLine($$"""
                <tr class="result-row {{css}}" data-status="{{Html(hit.Status)}}" data-search="{{Html($"{hit.GenomeName} {hit.GenomeFile} {hit.SubjectId} {hit.Status}")}}">
                  <td data-sort="{{Html(hit.GenomeName)}}"><strong>{{Html(hit.GenomeName)}}</strong><small title="{{Html(detail)}}">{{Html(detail)}}</small></td>
                  <td data-sort="{{Html(hit.Status)}}"><span class="pill"><span class="status-dot"></span>{{Html(hit.Status)}}</span></td>
                  <td data-sort="{{Number(hit.Identity)}}">{{PercentMetric(hit.Identity, hit.IdentityDisplay)}}</td>
                  <td data-sort="{{Number(hit.Coverage)}}">{{PercentMetric(hit.Coverage, hit.CoverageDisplay)}}</td>
                  <td data-sort="{{Html(hit.SubjectId)}}">{{Html(hit.SubjectId)}}</td>
                  <td data-sort="{{hit.AlignmentLength?.ToString(CultureInfo.InvariantCulture) ?? ""}}">{{hit.AlignmentLength?.ToString("N0", CultureInfo.InvariantCulture) ?? "—"}}</td>
                  <td data-sort="{{Html(hit.CoordinatesDisplay)}}">{{Html(hit.CoordinatesDisplay)}}</td>
                  <td data-sort="{{Number(hit.Evalue)}}">{{Html(hit.EvalueDisplay)}}</td>
                  <td data-sort="{{Number(hit.BitScore)}}">{{Html(hit.BitScoreDisplay)}}</td>
                </tr>
                """);
        }

        var report = $$$"""
            <!doctype html>
            <html lang="en" data-theme="dark">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="color-scheme" content="dark light">
              <title>Helix Blast report · {{{Html(queryLabel)}}}</title>
              <style>
                :root{--bg:#07101e;--panel:#0d192a;--panel-2:#122239;--line:#233852;--text:#eef7ff;--muted:#91a9ba;--accent:#35ded4;--accent-2:#408ee8;--good:#42d6a4;--warn:#f4ba5a;--quiet:#8798aa;--bad:#f07883;--shadow:0 18px 55px #0006;color-scheme:dark}
                html[data-theme="light"]{--bg:#f3f7fa;--panel:#fff;--panel-2:#eaf2f7;--line:#d3e0e8;--text:#142638;--muted:#607487;--accent:#087f84;--accent-2:#2168ba;--shadow:0 14px 40px #18334a1a;color-scheme:light}
                *{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.5 "Segoe UI",Arial,sans-serif}.hero{position:relative;overflow:hidden;padding:48px max(5vw,28px) 74px;background:radial-gradient(circle at 78% -20%,#157c8466,transparent 42%),linear-gradient(135deg,#11213b,#07101e 68%)}.hero:after{content:"";position:absolute;right:9%;top:-115px;width:320px;height:320px;border:1px solid #48e3d433;border-radius:50%;box-shadow:0 0 0 34px #48e3d411,0 0 0 75px #408ee80c}.brand{display:flex;align-items:center;gap:12px;color:#eafaff;font-size:12px;font-weight:700;letter-spacing:.16em}.brand-mark{display:grid;place-items:center;width:36px;height:36px;border:1px solid #4ce8dd88;border-radius:11px;background:#0e3041;color:#6afff4;font-size:20px}.hero h1{position:relative;margin:24px 0 8px;max-width:900px;font-size:clamp(28px,4vw,48px);line-height:1.08;letter-spacing:-.03em}.accent{color:var(--accent)}.muted{color:var(--muted)}.hero-actions{position:absolute;z-index:2;right:max(5vw,28px);top:45px;display:flex;gap:8px}.button{border:1px solid #59ded677;border-radius:10px;padding:9px 13px;color:#eafffd;background:#123448;cursor:pointer;font:inherit;font-size:13px;font-weight:600}.button:hover{background:#185064}.wrap{max-width:1500px;margin:auto;padding:0 28px 34px}.cards{position:relative;display:grid;grid-template-columns:repeat(5,minmax(130px,1fr));gap:12px;margin-top:-38px}.card{padding:17px 18px;border:1px solid var(--line);border-radius:15px;background:var(--panel);box-shadow:var(--shadow)}.metric{font-size:29px;font-weight:750;line-height:1.1}.label{margin-top:5px;color:var(--muted);font-size:12px}.details{display:grid;grid-template-columns:repeat(4,1fr);gap:1px;margin:22px 0;border:1px solid var(--line);border-radius:14px;overflow:hidden;background:var(--line)}.detail{min-width:0;padding:14px 16px;background:var(--panel)}.detail b{display:block;margin-bottom:3px;color:var(--accent);font-size:10px;letter-spacing:.09em;text-transform:uppercase}.detail span{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.toolbar{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin:0 0 14px;padding:12px;border:1px solid var(--line);border-radius:14px;background:var(--panel)}.search{min-width:240px;flex:1;border:1px solid var(--line);border-radius:9px;padding:10px 12px;background:var(--bg);color:var(--text);outline:none}.search:focus{border-color:var(--accent);box-shadow:0 0 0 3px #35ded422}.filters{display:flex;gap:6px;flex-wrap:wrap}.filter{border:1px solid var(--line);border-radius:99px;padding:7px 10px;background:transparent;color:var(--muted);cursor:pointer}.filter:hover,.filter.active{border-color:var(--accent);background:#25bcb51d;color:var(--text)}.count{min-width:92px;text-align:right;color:var(--muted);font-size:12px}.table-shell{overflow:auto;border:1px solid var(--line);border-radius:15px;background:var(--panel);box-shadow:var(--shadow)}table{width:100%;min-width:1120px;border-collapse:collapse}thead{position:sticky;top:0;z-index:1}th{padding:0;background:var(--panel-2);text-align:left}.sort-button{width:100%;border:0;padding:12px;color:var(--muted);background:transparent;text-align:left;font:inherit;font-size:10px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;cursor:pointer;white-space:nowrap}.sort-button:after{content:" ↕"}.sort-button:hover{color:var(--accent)}.sort-button.sorted{color:var(--accent)}.sort-button[aria-sort="ascending"]:after{content:" ↑"}.sort-button[aria-sort="descending"]:after{content:" ↓"}td{padding:12px;border-top:1px solid var(--line);vertical-align:middle}tbody tr:hover{background:#3adbd708}.result-row td:first-child{border-left:4px solid var(--quiet)}.result-row.present td:first-child{border-left-color:var(--good)}.result-row.below td:first-child{border-left-color:var(--warn)}.result-row.error td:first-child{border-left-color:var(--bad)}td strong{display:block;max-width:250px;overflow:hidden;text-overflow:ellipsis}td small{display:block;max-width:250px;margin-top:2px;overflow:hidden;color:var(--muted);font-size:10px;text-overflow:ellipsis;white-space:nowrap}.pill{display:inline-flex;align-items:center;gap:7px;border-radius:99px;padding:4px 8px;background:#71839722;font-size:11px;font-weight:700;white-space:nowrap}.status-dot{width:7px;height:7px;border-radius:50%;background:var(--quiet)}.present .pill{color:var(--good)}.present .status-dot{background:var(--good)}.below .pill{color:var(--warn)}.below .status-dot{background:var(--warn)}.error .pill{color:var(--bad)}.error .status-dot{background:var(--bad)}.bar-value{display:flex;align-items:center;gap:8px;min-width:110px}.bar-value span{min-width:48px;font-variant-numeric:tabular-nums}.bar{width:58px;height:5px;overflow:hidden;border-radius:9px;background:var(--line)}.bar i{display:block;height:100%;border-radius:inherit;background:linear-gradient(90deg,var(--accent-2),var(--accent))}.empty{padding:42px;text-align:center;color:var(--muted)}.notes{display:grid;grid-template-columns:1fr auto;gap:18px;margin-top:18px;color:var(--muted);font-size:12px}.privacy{color:var(--accent)}
                @media(max-width:900px){.hero{padding-bottom:64px}.hero-actions{position:relative;right:auto;top:auto;margin-top:22px}.cards{grid-template-columns:repeat(2,1fr)}.cards .card:first-child{grid-column:span 2}.details{grid-template-columns:repeat(2,1fr)}.wrap{padding-inline:15px}.count{width:100%;text-align:left}}
                @media print{html{color-scheme:light}.hero{padding:20px 0;background:#fff;color:#122537}.hero:after,.hero-actions,.toolbar{display:none}.wrap{max-width:none;padding:0}.cards{margin:0;grid-template-columns:repeat(5,1fr)}.card,.table-shell{box-shadow:none}.details{margin:12px 0}.table-shell{overflow:visible}table{min-width:0;font-size:9px}.sort-button,td{padding:6px}.bar{display:none}.notes{break-inside:avoid}body{background:#fff;color:#122537;print-color-adjust:exact;-webkit-print-color-adjust:exact}}
              </style>
            </head>
            <body>
              <header class="hero">
                <div class="brand"><span class="brand-mark">⌁</span> HELIX BLAST · LOCAL SEQUENCE ANALYSIS</div>
                <h1>Analysis report <span class="accent">{{{Html(queryLabel)}}}</span></h1>
                <div class="muted">Generated {{{generated:yyyy-MM-dd HH:mm}}} · All sequence data processed locally</div>
                <div class="hero-actions"><button class="button" id="themeButton" type="button">Light theme</button><button class="button" type="button" onclick="window.print()">Print / Save PDF</button></div>
              </header>
              <main class="wrap">
                <section class="cards" aria-label="Analysis summary">
                  <div class="card"><div class="metric">{{{rows.Count}}}</div><div class="label">Samples analysed</div></div>
                  <div class="card"><div class="metric accent">{{{present}}}</div><div class="label">Present</div></div>
                  <div class="card"><div class="metric">{{{detectionRate.ToString("F0", CultureInfo.InvariantCulture)}}}%</div><div class="label">Detection rate</div></div>
                  <div class="card"><div class="metric">{{{below}}}</div><div class="label">Below thresholds</div></div>
                  <div class="card"><div class="metric">{{{noHit + errors}}}</div><div class="label">No hit / errors</div></div>
                </section>
                <section class="details" aria-label="Analysis settings">
                  <div class="detail"><b>Program</b><span>{{{Html(program)}}}</span></div>
                  <div class="detail"><b>Minimum identity</b><span>{{{minimumIdentity.ToString("g", CultureInfo.InvariantCulture)}}}%</span></div>
                  <div class="detail"><b>Minimum HSP coverage</b><span>{{{minimumCoverage.ToString("g", CultureInfo.InvariantCulture)}}}%</span></div>
                  <div class="detail"><b>Query</b><span title="{{{Html(queryLabel)}}}">{{{Html(queryLabel)}}}</span></div>
                </section>
                <section class="toolbar" aria-label="Result filters">
                  <input class="search" id="search" type="search" placeholder="Search sample, status or subject…" aria-label="Search results">
                  <div class="filters">
                    <button class="filter active" type="button" data-filter="all">All · {{{rows.Count}}}</button>
                    <button class="filter" type="button" data-filter="Present">Present · {{{present}}}</button>
                    <button class="filter" type="button" data-filter="Below thresholds">Below · {{{below}}}</button>
                    <button class="filter" type="button" data-filter="No hit">No hit · {{{noHit}}}</button>
                    <button class="filter" type="button" data-filter="Error">Errors · {{{errors}}}</button>
                  </div>
                  <div class="count" id="visibleCount"></div>
                </section>
                <div class="table-shell">
                  <table id="resultsTable">
                    <thead><tr>
                      <th><button class="sort-button" data-column="0">Sample</button></th>
                      <th><button class="sort-button" data-column="1">Status</button></th>
                      <th><button class="sort-button" data-column="2">Identity</button></th>
                      <th><button class="sort-button" data-column="3">Coverage</button></th>
                      <th><button class="sort-button" data-column="4">Subject / contig</button></th>
                      <th><button class="sort-button" data-column="5">Alignment</button></th>
                      <th><button class="sort-button" data-column="6">Coordinates</button></th>
                      <th><button class="sort-button" data-column="7">E-value</button></th>
                      <th><button class="sort-button" data-column="8">Bit score</button></th>
                    </tr></thead>
                    <tbody>{{{rowMarkup}}}</tbody>
                  </table>
                  <div class="empty" id="emptyState" hidden>No result matches the current filters.</div>
                </div>
                <footer class="notes"><span>Coverage is the best HSP coverage returned by BLAST. Select a column heading to sort the table.</span><span class="privacy">Self-contained offline report · Helix Blast</span></footer>
              </main>
              <script>
                (()=>{const rows=[...document.querySelectorAll('.result-row')],search=document.getElementById('search'),count=document.getElementById('visibleCount'),empty=document.getElementById('emptyState');let status='all',sortColumn=-1,ascending=true;
                  function filter(){const term=search.value.trim().toLocaleLowerCase();let visible=0;rows.forEach(row=>{const show=(status==='all'||row.dataset.status===status)&&(!term||row.dataset.search.toLocaleLowerCase().includes(term));row.hidden=!show;if(show)visible++});count.textContent=`${visible} of ${rows.length} results`;empty.hidden=visible!==0;document.getElementById('resultsTable').hidden=visible===0}
                  search.addEventListener('input',filter);document.querySelectorAll('.filter').forEach(button=>button.addEventListener('click',()=>{document.querySelector('.filter.active')?.classList.remove('active');button.classList.add('active');status=button.dataset.filter;filter()}));
                  document.querySelectorAll('.sort-button').forEach(button=>button.addEventListener('click',()=>{const column=Number(button.dataset.column);ascending=sortColumn===column?!ascending:true;sortColumn=column;document.querySelectorAll('.sort-button').forEach(x=>{x.classList.remove('sorted');x.removeAttribute('aria-sort')});button.classList.add('sorted');button.setAttribute('aria-sort',ascending?'ascending':'descending');rows.sort((a,b)=>{const av=a.cells[column].dataset.sort??'',bv=b.cells[column].dataset.sort??'',an=Number(av),bn=Number(bv),numeric=av!==''&&bv!==''&&Number.isFinite(an)&&Number.isFinite(bn),comparison=numeric?an-bn:av.localeCompare(bv,undefined,{numeric:true,sensitivity:'base'});return ascending?comparison:-comparison});const body=document.querySelector('tbody');rows.forEach(row=>body.append(row))}));
                  document.getElementById('themeButton').addEventListener('click',event=>{const root=document.documentElement,light=root.dataset.theme!=='light';root.dataset.theme=light?'light':'dark';event.currentTarget.textContent=light?'Dark theme':'Light theme'});filter();
                })();
              </script>
            </body>
            </html>
            """;
        await File.WriteAllTextAsync(path, report, new UTF8Encoding(false));
    }

    private static string Number(double? value) => value?.ToString("G17", CultureInfo.InvariantCulture) ?? "";
    private static string PercentMetric(double? value, string display) => value is null
        ? "—"
        : $"<div class=\"bar-value\"><span>{Html(display)}</span><span class=\"bar\"><i style=\"width:{Math.Clamp(value.Value, 0, 100).ToString("F2", CultureInfo.InvariantCulture)}%\"></i></span></div>";
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
