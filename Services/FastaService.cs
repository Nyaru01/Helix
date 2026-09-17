using System.Text;
using System.IO;

namespace LocalBlast.Services;

public enum SequenceType
{
    Nucleotide,
    Protein,
    Unknown
}

public static class FastaService
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".fa", ".fasta", ".fna", ".ffn", ".faa", ".fas", ".fsa"
    };

    private const string NucleotideAlphabet = "ACGTUNRYKMSWBDHVX-.*";
    private const string ProteinAlphabet = "ABCDEFGHIKLMNPQRSTVWXYZJUO*-.";

    public static (string Header, string Sequence) NormalizeQuery(string text, SequenceType expected)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("The query sequence is empty.");

        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0)
                        .ToList();

        var header = ">query";
        IEnumerable<string> sequenceLines = lines;
        if (lines.Count > 0 && lines[0].StartsWith('>'))
        {
            header = lines[0];
            sequenceLines = lines.Skip(1);
        }

        var sequence = string.Concat(sequenceLines)
                             .Replace(" ", "")
                             .Replace("\t", "")
                             .ToUpperInvariant();

        if (sequence.Length == 0)
            throw new InvalidOperationException("No sequence was found in the query.");

        var alphabet = expected == SequenceType.Nucleotide ? NucleotideAlphabet : ProteinAlphabet;
        var invalid = sequence.Where(c => !alphabet.Contains(c)).Distinct().Take(12).ToArray();
        if (invalid.Length > 0)
        {
            var label = expected == SequenceType.Nucleotide ? "nucleotide" : "protein";
            throw new InvalidOperationException($"The query does not look like a valid {label} sequence. Unexpected characters: {string.Join(", ", invalid)}");
        }

        return (header, sequence);
    }

    public static async Task WriteQueryAsync(string path, string header, string sequence)
    {
        await using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        await writer.WriteLineAsync(header.TrimEnd());
        for (var i = 0; i < sequence.Length; i += 80)
            await writer.WriteLineAsync(sequence.Substring(i, Math.Min(80, sequence.Length - i)));
    }

    public static (List<string> Selected, List<string> Ignored) DiscoverFastaFiles(string folder, SequenceType desired)
    {
        var selected = new List<string>();
        var ignored = new List<string>();

        foreach (var file in Directory.EnumerateFiles(folder).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (!Extensions.Contains(Path.GetExtension(file)))
                continue;

            var type = GuessFastaType(file);
            if (type == desired)
                selected.Add(file);
            else
                ignored.Add(file);
        }

        return (selected, ignored);
    }

    private static SequenceType GuessFastaType(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".fna", StringComparison.OrdinalIgnoreCase) || ext.Equals(".ffn", StringComparison.OrdinalIgnoreCase))
            return SequenceType.Nucleotide;
        if (ext.Equals(".faa", StringComparison.OrdinalIgnoreCase))
            return SequenceType.Protein;

        var sample = ReadSequenceSample(path, 5000);
        if (sample.Length == 0)
            return SequenceType.Unknown;

        var letters = sample.Where(char.IsLetter).ToArray();
        if (letters.Length == 0)
            return SequenceType.Unknown;

        var nucleotideLike = letters.Count(c => NucleotideAlphabet.Contains(char.ToUpperInvariant(c))) / (double)letters.Length;
        return nucleotideLike >= 0.96 ? SequenceType.Nucleotide : SequenceType.Protein;
    }

    private static string ReadSequenceSample(string path, int maxChars)
    {
        var sb = new StringBuilder(maxChars);
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith('>'))
                    continue;

                foreach (var c in line)
                {
                    if (!char.IsWhiteSpace(c))
                        sb.Append(char.ToUpperInvariant(c));
                    if (sb.Length >= maxChars)
                        return sb.ToString();
                }
            }
        }
        catch
        {
            return "";
        }

        return sb.ToString();
    }
}
