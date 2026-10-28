namespace ITBees.LedDisplay.Protocol;

/// <summary>How a board reads the text of <c>SET</c>.</summary>
public enum LedTextCharset
{
    /// <summary>Windows-1250 with Polish letters - the Elitel controller.</summary>
    Windows1250 = 0,

    /// <summary>
    /// Plain ASCII for controllers without Polish glyphs: letters lose their diacritics ("ZAMKNIĘTY" is sent
    /// as "ZAMKNIETY"), any other character outside ASCII becomes "?".
    /// </summary>
    Ascii = 1,
}
