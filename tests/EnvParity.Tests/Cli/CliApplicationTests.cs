using System.Text;
using System.Text.Json;
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

    [Fact]
    public void Run_ValidFiles_Returns0AndPrintsOnlyNames()
    {
        using var fixture = new Fixture($"A={Canary}\nB=2\n", "A=\nB=\n");

        var (exit, output, error) = Run(fixture.WithPaths());

        Assert.Equal(0, exit);
        Assert.Equal(
            $"✓ {fixture.EnvPath} is valid (2 variables checked against {fixture.ExamplePath}){Environment.NewLine}",
            output);
        Assert.Empty(error);
        Assert.DoesNotContain(Canary, output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_MissingVariable_Returns1AndReportsNameOnly()
    {
        using var fixture = new Fixture("A=1\n", $"A=\nAPI_KEY={Canary}\n");

        var (exit, output, error) = Run(fixture.WithPaths());

        Assert.Equal(1, exit);
        Assert.Contains("✗ 1 missing variable:", output, StringComparison.Ordinal);
        Assert.Contains("  API_KEY", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, output, StringComparison.Ordinal);
        Assert.Empty(error);
    }

    [Fact]
    public void Run_EmptyVariable_Returns1()
    {
        using var fixture = new Fixture("A=\nB=2\n", "A=\nB=\n");

        var (exit, output, _) = Run(fixture.WithPaths());

        Assert.Equal(1, exit);
        Assert.Contains("✗ 1 empty variable:", output, StringComparison.Ordinal);
        Assert.Contains("  A", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ExtraVariable_NonStrict_Returns0WithWarning()
    {
        using var fixture = new Fixture($"A=1\nDEBUG={Canary}\n", "A=\n");

        var (exit, output, _) = Run(fixture.WithPaths());

        Assert.Equal(0, exit);
        Assert.Contains("use --strict to treat it as an error", output, StringComparison.Ordinal);
        Assert.Contains("  DEBUG", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ExtraVariable_Strict_Returns1()
    {
        using var fixture = new Fixture("A=1\nDEBUG=1\n", "A=\n");

        var (exit, output, _) = Run(fixture.WithPaths("--strict"));

        Assert.Equal(1, exit);
        Assert.Contains("✗ 1 extra variable:", output, StringComparison.Ordinal);
        Assert.DoesNotContain("use --strict", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_DuplicateInEnv_IsWarningOnlyUnlessStrict()
    {
        using var fixture = new Fixture($"A=1\nA={Canary}\n", "A=\n");

        var relaxed = Run(fixture.WithPaths());
        Assert.Equal(0, relaxed.Exit);
        Assert.Contains("Duplicate key", relaxed.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, relaxed.Output, StringComparison.Ordinal);

        var strict = Run(fixture.WithPaths("--strict"));
        Assert.Equal(1, strict.Exit);
    }

    [Fact]
    public void Run_ParserError_Returns1()
    {
        using var fixture = new Fixture("A=1\nB\n", "A=\n");

        var (exit, output, _) = Run(fixture.WithPaths());

        Assert.Equal(1, exit);
        Assert.Contains("invalid line (expected KEY=VALUE)", output, StringComparison.Ordinal);
        Assert.Contains($"{fixture.EnvPath}:2", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_Help_Returns0AndExactText()
    {
        var expected = string.Join("\n", new[]
        {
            "envparity 1.0.1 — validate a .env file against a reference file",
            string.Empty,
            "Usage:",
            "  envparity [options]",
            string.Empty,
            "Options:",
            "  --env <path>          File to validate (default: .env)",
            "  --example <path>      Reference file (default: .env.example)",
            "  --strict              Treat extra variables and parser warnings as errors",
            "  --format <text|json>  Output format (default: text)",
            "  --no-color            Disable colors (honors NO_COLOR)",
            "  --version             Show version and exit",
            "  -h, --help            Show help and exit",
            string.Empty,
            "Exit codes:",
            "  0  valid",
            "  1  validation problems",
            "  2  usage or I/O error",
            string.Empty,
        });

        var (exit, output, error) = Run(["--help"]);

        Assert.Equal(0, exit);
        Assert.Equal(expected, output);
        Assert.Empty(error);
    }

    [Fact]
    public void Run_Version_Returns0()
    {
        var (exit, output, error) = Run(["--version"]);

        Assert.Equal(0, exit);
        Assert.Equal($"envparity 1.0.1{Environment.NewLine}", output);
        Assert.Empty(error);
    }

    [Fact]
    public void Run_UnknownArgument_Returns2WithEmptyStdout()
    {
        var (exit, output, error) = Run(["--bogus"]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Equal(
            $"envparity: unrecognized argument: --bogus{Environment.NewLine}",
            error);
    }

    [Fact]
    public void Run_FormatWithoutValue_Returns2WithEmptyStdout()
    {
        var (exit, output, error) = Run(["--format"]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Equal(
            $"envparity: option '--format' requires a value{Environment.NewLine}",
            error);
    }

    [Fact]
    public void Run_InvalidFormat_Returns2WithEmptyStdout()
    {
        using var fixture = new Fixture("A=1\n", "A=\n");

        var (exit, output, error) = Run(fixture.WithPaths("--format", "yaml"));

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Equal(
            $"envparity: invalid value for '--format' (expected text or json){Environment.NewLine}",
            error);
    }

    [Fact]
    public void Run_MissingEnvFileInJsonMode_Returns2WithEmptyStdout()
    {
        var missing = Path.Combine(
            Path.GetTempPath(), $"envparity-missing-{Guid.NewGuid():N}");

        var (exit, output, error) = Run(["--format", "json", "--env", missing]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith($"envparity: file not found: {missing}", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_JsonMode_ProducesMachineContractWithoutValues()
    {
        using var fixture = new Fixture($"A={Canary}\nDEBUG={Canary}\n", $"A=\nAPI_KEY={Canary}\n");

        var (exit, output, error) = Run(fixture.WithPaths("--format", "json"));

        Assert.Equal(1, exit);
        Assert.Empty(error);
        Assert.DoesNotContain(Canary, output, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001B", output, StringComparison.Ordinal);
        Assert.DoesNotContain("✓", output, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.False(root.GetProperty("valid").GetBoolean());
        Assert.Equal(fixture.EnvPath, root.GetProperty("files").GetProperty("env").GetString());
        Assert.Equal(fixture.ExamplePath, root.GetProperty("files").GetProperty("example").GetString());
        Assert.Equal("API_KEY", Assert.Single(root.GetProperty("missing").EnumerateArray()).GetString());
        Assert.Equal("DEBUG", Assert.Single(root.GetProperty("extra").EnumerateArray()).GetString());
    }

    [Fact]
    public void Run_JsonMode_IgnoresUnicodeAndColorFlags()
    {
        using var fixture = new Fixture("A=1\n", "A=\n");

        var (exit, output, _) = Run(fixture.WithPaths("--format", "json"), useUnicode: true);

        Assert.Equal(0, exit);
        Assert.DoesNotContain("✓", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001B", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_NoColorOption_LeavesPlainText()
    {
        using var fixture = new Fixture("A=1\n", "A=\n");

        var (exit, output, _) = Run(fixture.WithPaths("--no-color"));

        Assert.Equal(0, exit);
        Assert.DoesNotContain("\u001B", output, StringComparison.Ordinal);
        Assert.Contains("is valid", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_CancelledToken_Returns130AndEmptyStdout()
    {
        using var fixture = new Fixture("A=1\n", "A=\n");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var (exit, output, error) = Run(fixture.WithPaths(), token: cancellation.Token);

        Assert.Equal(130, exit);
        Assert.Empty(output);
        Assert.Empty(error);
    }

    [Fact]
    public void Run_MissingDirectory_Returns2WithFixedMessage()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"envparity-no-dir-{Guid.NewGuid():N}",
            ".env");

        var (exit, output, error) = Run(["--env", path]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Equal(
            $"envparity: file not found: {path}{Environment.NewLine}",
            error);
    }

    [Fact]
    public void Run_DuplicateOption_Returns2WithFixedMessage()
    {
        var (exit, output, error) = Run(["--env", "a.env", "--env", "b.env"]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Equal(
            $"envparity: invalid arguments{Environment.NewLine}",
            error);
    }

    [Fact]
    public void Run_MissingValueAfterOtherOption_Returns2()
    {
        var (exit, output, error) = Run(["--strict", "--env"]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Equal(
            $"envparity: option '--env' requires a value{Environment.NewLine}",
            error);
    }

    [Fact]
    public void Run_HelpAsciiFallback_UsesHyphen()
    {
        var (exit, output, _) = Run(["--help"], useUnicode: false);

        Assert.Equal(0, exit);
        Assert.StartsWith("envparity 1.0.1 - validate", output, StringComparison.Ordinal);
        Assert.DoesNotContain("—", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_DirectoryAsExample_Returns2WithEmptyStdout()
    {
        using var fixture = new Fixture("A=1\n", "A=\n");
        var directory = Path.GetTempPath();

        var (exit, output, error) = Run(
            ["--env", fixture.EnvPath, "--example", directory]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("envparity: ", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_CanaryNeverLeaksInTextOrJson()
    {
        using var fixture = new Fixture(
            $"A=\nDEBUG={Canary}\n",
            $"A=\nAPI_KEY={Canary}\n");

        var text = Run(fixture.WithPaths());
        var json = Run(fixture.WithPaths("--format", "json"));

        Assert.DoesNotContain(Canary, text.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, text.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, json.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, json.Error, StringComparison.Ordinal);
    }

    private static (int Exit, string Output, string Error) Run(
        string[] args,
        bool useUnicode = true,
        CancellationToken token = default)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = CliApplication.Run(args, stdout, stderr, useUnicode, token);

        return (exit, stdout.ToString(), stderr.ToString());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("envparity-cli-");

        public Fixture(string envContent, string exampleContent)
        {
            EnvPath = Path.Combine(directory.FullName, "test.env");
            ExamplePath = Path.Combine(directory.FullName, "test.env.example");

            File.WriteAllText(EnvPath, envContent, new UTF8Encoding(false));
            File.WriteAllText(ExamplePath, exampleContent, new UTF8Encoding(false));
        }

        public string EnvPath { get; }

        public string ExamplePath { get; }

        public string[] WithPaths(params string[] args) =>
            [.. args, "--env", EnvPath, "--example", ExamplePath];

        public void Dispose() => directory.Delete(recursive: true);
    }
}
