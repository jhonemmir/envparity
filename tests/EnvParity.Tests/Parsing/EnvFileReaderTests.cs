using System.Text;
using EnvParity.Parsing;

namespace EnvParity.Tests.Parsing;

public sealed class EnvFileReaderTests
{
    [Fact]
    public void Read_ValidUtf8_ReturnsContent()
    {
        var directory = Directory.CreateTempSubdirectory("envparity-reader-");

        try
        {
            var path = Path.Combine(directory.FullName, ".env");
            File.WriteAllText(path, "A=1\u00F1", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Assert.Equal("A=1\u00F1", EnvFileReader.Read(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Read_InvalidUtf8Sequence_ThrowsDecoderFallback()
    {
        var directory = Directory.CreateTempSubdirectory("envparity-reader-");

        try
        {
            var path = Path.Combine(directory.FullName, ".env");
            File.WriteAllBytes(path, [0xC3, 0x28]);

            Assert.Throws<DecoderFallbackException>(() => EnvFileReader.Read(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Read_Utf16File_ThrowsDecoderFallback()
    {
        var directory = Directory.CreateTempSubdirectory("envparity-reader-");

        try
        {
            var path = Path.Combine(directory.FullName, ".env");
            var bytes = Encoding.Unicode.GetPreamble()
                .Concat(Encoding.Unicode.GetBytes("S3CR3T-CANARY-9f2=1"))
                .ToArray();
            File.WriteAllBytes(path, bytes);

            Assert.Throws<DecoderFallbackException>(() => EnvFileReader.Read(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Read_MissingFile_ThrowsFileNotFound()
    {
        var directory = Directory.CreateTempSubdirectory("envparity-reader-");

        try
        {
            var path = Path.Combine(directory.FullName, "missing.env");

            Assert.Throws<FileNotFoundException>(() => EnvFileReader.Read(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
