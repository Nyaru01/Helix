using LocalBlast.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LocalBlast.Tests;

[TestClass]
public class BlastResultParserTests
{
    [TestMethod]
    public void ParseBestHit_UsesBestPassingHit()
    {
        const string output = "contig-low\t99\t70\t70\t100\t500\t1e-20\t200\t1\t70\t20\t89\tAAAA\tAAAA\n" +
                              "contig-pass\t90\t90\t90\t100\t500\t1e-10\t150\t2\t91\t500\t411\tCCCC\tCCCC";

        var hit = BlastResultParser.ParseBestHit("C:\\data\\sample.fna", output, 80, 80);

        Assert.AreEqual("Present", hit.Status);
        Assert.AreEqual("contig-pass", hit.SubjectId);
        Assert.AreEqual("500 ← 411", hit.CoordinatesDisplay);
    }

    [TestMethod]
    public void ParseBestHit_ReturnsBelowThresholdsWhenAnAlignmentExists()
    {
        const string output = "contig\t100\t55\t66\t120\t30000\t1e-30\t122\t55\t120\t1\t66\tACGT\tACGT";

        var hit = BlastResultParser.ParseBestHit("sample.fna", output, 80, 80);

        Assert.AreEqual("Below thresholds", hit.Status);
        Assert.AreEqual(100d, hit.Identity);
        Assert.AreEqual(55d, hit.Coverage);
    }

    [TestMethod]
    public void ParseBestHit_ReturnsNoHitForEmptyOutput()
    {
        var hit = BlastResultParser.ParseBestHit("sample.fna", "", 80, 80);

        Assert.AreEqual("No hit", hit.Status);
        Assert.AreEqual("sample", hit.GenomeName);
    }
}
