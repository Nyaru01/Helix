using System.IO.Compression;
using LocalBlast.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LocalBlast.Tests;

[TestClass]
public class BundledBlastTests
{
    [TestMethod]
    public void EmbeddedBundleContainsSearchAndIndexExecutables()
    {
        using var resource = typeof(NativeBlastService).Assembly.GetManifestResourceStream(
            "LocalBlast.Resources.blast-win64-2.17.0.zip");
        Assert.IsNotNull(resource, "The BLAST archive must be embedded in the application.");

        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);
        foreach (var executable in new[] { "blastn.exe", "blastp.exe", "tblastn.exe", "makeblastdb.exe" })
            Assert.IsNotNull(archive.GetEntry(executable), $"The embedded BLAST archive is missing {executable}.");
    }
}
