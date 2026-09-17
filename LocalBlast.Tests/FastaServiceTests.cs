using LocalBlast.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LocalBlast.Tests;

[TestClass]
public class FastaServiceTests
{
    [TestMethod]
    public void NormalizeQuery_FormatsSequenceAndPreservesHeader()
    {
        var result = FastaService.NormalizeQuery(">sample one\nac gt\nTA\n", SequenceType.Nucleotide);

        Assert.AreEqual(">sample one", result.Header);
        Assert.AreEqual("ACGTTA", result.Sequence);
    }

    [TestMethod]
    public void NormalizeQuery_RejectsInvalidNucleotideCharacter()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => FastaService.NormalizeQuery("ACGTQ", SequenceType.Nucleotide));

        StringAssert.Contains(exception.Message, "Unexpected characters: Q");
    }
}
