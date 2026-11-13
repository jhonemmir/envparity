using System.Text;

namespace EnvParity.Parsing;

/// <summary>
/// Reads a file as bytes and decodes it as strict UTF-8. It deliberately avoids
/// <see cref="File.ReadAllText(string)"/> and <see cref="StreamReader"/>: both
/// auto-detect UTF-16/UTF-32 BOMs and would accept a UTF-16 file.
/// </summary>
public static class EnvFileReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// Returns the decoded content. Throws <see cref="DecoderFallbackException"/>
    /// for invalid UTF-8 and the usual I/O exceptions for read failures.
    /// </summary>
    public static string Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var bytes = File.ReadAllBytes(path);
        return StrictUtf8.GetString(bytes);
    }
}
