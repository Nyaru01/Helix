using LocalBlast.Models;
using LocalBlast.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LocalBlast.Tests;

[TestClass]
public class ResultsExportServiceTests
{
    [TestMethod]
    public async Task ExportHtmlAsync_CreatesInteractiveSelfContainedReportAndEncodesValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"HelixBlast-report-{Guid.NewGuid():N}.html");
        try
        {
            var hits = new[]
            {
                new BlastHit
                {
                    GenomeName = "sample <one>", GenomeFile = @"C:\data\sample-one.fna", Status = "Present",
                    SubjectId = "contig&1", Identity = 98.5, Coverage = 91.2, AlignmentLength = 123,
                    SubjectStart = 10, SubjectEnd = 132, Evalue = 1e-20, BitScore = 240
                },
                new BlastHit { GenomeName = "sample two", Status = "No hit" }
            };

            await ResultsExportService.ExportHtmlAsync(path, "query <alpha>", "blastn", 80, 75, hits);
            var html = await File.ReadAllTextAsync(path);

            StringAssert.Contains(html, "query &lt;alpha&gt;");
            StringAssert.Contains(html, "sample &lt;one&gt;");
            StringAssert.Contains(html, "contig&amp;1");
            StringAssert.Contains(html, "data-filter=\"Present\"");
            StringAssert.Contains(html, "id=\"themeButton\"");
            StringAssert.Contains(html, "class=\"bar-value\"");
            Assert.IsFalse(html.Contains("https://", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
