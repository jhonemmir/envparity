using System.Text;
using EnvParity.Cli;

namespace EnvParity.Tests.Cli;

public sealed class CliApplicationTests
{
    private const string Canary = "S3CR3T-CANARY-9f2";

    // Case 33
    [Fact]
    public void Run_NonUtf8File_Returns2()
    {
        var directory = Directory.CreateTempSubdirectory("envparity-cli-");

        try
        {
            var path = Path.Combine(directory.FullName, ".env");
            var bytes = Encoding.Unicode.GetPreamble()
                .Concat(Encoding.Unicode.GetBytes($"{Canary}=1"))
                .ToArray();
            File.WriteAllBytes(path, bytes);

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = CliApplication.Run(["--env", path], stdout, stderr);

            Assert.Equal(2, exitCode);
            Assert.Empty(stdout.ToString());
            Assert.Equal(
                $"envparity: {path}: file is not valid UTF-8{Environment.NewLine}",
                stderr.ToString());
            Assert.DoesNotContain(Canary, stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
