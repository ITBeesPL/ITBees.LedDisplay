namespace ITBees.LedDisplay.Protocol;

/// <summary>One protocol line, without the terminating line feed.</summary>
public sealed record LedCommand(string Line)
{
    /// <summary>Bytes put on the wire: the line in Windows-1250 followed by <c>\n</c>.</summary>
    public byte[] ToBytes() => ToBytes(LedTextCharset.Windows1250);

    /// <summary>Bytes put on the wire: the line in the board's character set followed by <c>\n</c>.</summary>
    public byte[] ToBytes(LedTextCharset charset)
    {
        var line = charset == LedTextCharset.Ascii ? ElitelTextEncoding.ToAscii(Line) : Line;
        var encoding = ElitelTextEncoding.ForCharset(charset);
        var length = encoding.GetByteCount(line);
        var bytes = new byte[length + 1];
        encoding.GetBytes(line, 0, line.Length, bytes, 0);
        bytes[length] = (byte)'\n';
        return bytes;
    }

    public override string ToString() => Line;
}
