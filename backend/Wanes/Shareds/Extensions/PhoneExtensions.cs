using System.Text.RegularExpressions;

namespace Wanes.Shareds.Extensions;

public static partial class PhoneExtensions
{
    /// <summary>
    /// How many trailing digits identify a phone. Riders type the same number in many shapes
    /// (+962790000000 / 00962790000000 / 0790000000 / 790000000); the national subscriber part
    /// is the only piece common to all of them, so matching is done on it.
    /// </summary>
    public const int PhoneKeyLength = 9;

    /// <summary>Normalizes a phone to a bare E.164-ish digit string (keeps a leading +).</summary>
    public static string NormalizePhone(this string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var trimmed = phone.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var digits = NonDigits().Replace(trimmed, string.Empty);
        return hasPlus ? "+" + digits : digits;
    }

    /// <summary>
    /// The lookup key for a phone: its last <see cref="PhoneKeyLength"/> digits, country code and
    /// trunk prefix stripped. Shorter numbers key on every digit they have.
    /// </summary>
    public static string PhoneKey(this string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = NonDigits().Replace(phone, string.Empty);
        return digits.Length <= PhoneKeyLength
            ? digits
            : digits[^PhoneKeyLength..];
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();
}
