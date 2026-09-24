namespace ITBees.LedDisplay.Protocol;

/// <summary>One protocol line, without the terminating line feed.</summary>
public sealed record LedCommand(string Line)
{
    /// <summary>Bytes put on the wire: the line in Windows-1250 followed by <c>\n</c>.</summary>
    public byte[] ToBytes()
    {
        var length = ElitelTextEncoding.Encoding.GetByteCount(Line);
        var bytes = new byte[length + 1];
        ElitelTextEncoding.Encoding.GetBytes(Line, 0, Line.Length, bytes, 0);
        bytes[length] = (byte)'\n';
        return bytes;
    }

    public override string ToString() => Line;
}
