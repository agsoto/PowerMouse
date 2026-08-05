using PowerMouse.ClickHints.Detection;

namespace PowerMouse.ClickHints.Hints;

public sealed class HintCodeGenerator
{
    public const string DefaultAlphabet = "ASDFGHJKLQWERTYUIOPZXCVBNM";

    private readonly char[] alphabet;

    public HintCodeGenerator(string alphabet = DefaultAlphabet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alphabet);
        this.alphabet = alphabet.ToUpperInvariant().Distinct().ToArray();
        if (this.alphabet.Length < 2 || this.alphabet.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new ArgumentException("The alphabet must contain at least two unique letters or digits.", nameof(alphabet));
        }
    }

    public IReadOnlyList<ClickHint> Assign(IReadOnlyList<DetectedElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        if (elements.Count == 0)
        {
            return [];
        }

        var length = 1;
        var capacity = alphabet.Length;
        while (capacity < elements.Count)
        {
            checked
            {
                capacity *= alphabet.Length;
            }

            length++;
        }

        return elements.Select((element, index) => new ClickHint(Encode(index, length), element)).ToArray();
    }

    private string Encode(int value, int length)
    {
        var result = new char[length];
        for (var index = length - 1; index >= 0; index--)
        {
            result[index] = alphabet[value % alphabet.Length];
            value /= alphabet.Length;
        }

        return new string(result);
    }
}
