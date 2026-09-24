using System.Text;

namespace ITBees.LedDisplay.Protocol;

/// <summary>
/// Text handling of the Elitel protocol. The controller reads Windows-1250 (the vendor test script
/// sends "ą" as 0xB9, "Ś" as 0x8C) and takes the text of <c>SET</c> between plain double quotes,
/// with no escape sequence documented.
/// </summary>
public static class ElitelTextEncoding
{
    public const char ReplacementCharacter = '?';

    /// <summary>
    /// Windows-1250 taken straight from the code-pages provider, so the host process does not need
    /// a global <see cref="Encoding.RegisterProvider"/> call. Characters outside the code page become "?".
    /// </summary>
    public static Encoding Encoding { get; } = CodePagesEncodingProvider.Instance.GetEncoding(
                                                   1250,
                                                   new EncoderReplacementFallback(ReplacementCharacter.ToString()),
                                                   DecoderFallback.ReplacementFallback)
                                               ?? throw new InvalidOperationException("Windows-1250 code page is not available");

    /// <summary>
    /// Makes a text safe to put between the quotes of <c>SET</c>: a double quote would end the string
    /// and a semicolon might be taken for a field separator (the protocol documents no escaping), so
    /// they become an apostrophe and a comma; line breaks and other control characters would end or
    /// corrupt the command line and are replaced with a space.
    /// </summary>
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(character switch
            {
                '"' or '“' or '”' or '„' => '\'',
                ';' => ',',
                _ when char.IsControl(character) => ' ',
                _ => character,
            });
        }

        return builder.ToString();
    }
}
