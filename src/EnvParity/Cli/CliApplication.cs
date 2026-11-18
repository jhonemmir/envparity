using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using EnvParity.Comparison;
using EnvParity.Output;
using EnvParity.Parsing;

namespace EnvParity.Cli;

/// <summary>
/// Testable entry point. <c>stdout</c> receives only the result; <c>stderr</c>
/// receives only usage or I/O errors. On exit code 2, <c>stdout</c> stays empty.
/// </summary>
public static class CliApplication
{
    public static int Run(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        bool useUnicode = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }

        var root = BuildCommand();
        var parseResult = root.Root.Parse(args);

        // Library error messages are localized, so they are never forwarded.
        if (parseResult.Errors.Count > 0)
        {
            stderr.WriteLine($"envparity: {DescribeParseError(parseResult, args)}");
            return 2;
        }

        var optionTokens = parseResult.Tokens
            .Where(token => token.Type == TokenType.Option)
            .Select(token => token.Value)
            .ToArray();

        if (optionTokens.Contains("--help") || optionTokens.Contains("-h"))
        {
            stdout.Write(HelpText(useUnicode));
            return 0;
        }

        if (optionTokens.Contains("--version"))
        {
            stdout.WriteLine($"envparity {ToolInfo.Version}");
            return 0;
        }

        var formatValue = parseResult.GetValue(root.Format);
        OutputFormat format;
        switch (formatValue)
        {
            case "text":
                format = OutputFormat.Text;
                break;
            case "json":
                format = OutputFormat.Json;
                break;
            default:
                stderr.WriteLine("envparity: invalid value for '--format' (expected text or json)");
                return 2;
        }

        var options = new CliOptions(
            parseResult.GetValue(root.Env) ?? ".env",
            parseResult.GetValue(root.Example) ?? ".env.example",
            parseResult.GetValue(root.Strict),
            format,
            parseResult.GetValue(root.NoColor));

        // Both files are read before anything is written: exit 2 never leaves a
        // partial result (or partial JSON) on stdout (D13).
        if (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }

        if (!TryRead(options.EnvPath, stderr, out var envContent))
        {
            return 2;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }

        if (!TryRead(options.ExamplePath, stderr, out var exampleContent))
        {
            return 2;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }

        var comparison = EnvComparer.Compare(
            EnvFileParser.Parse(envContent, options.EnvPath),
            EnvFileParser.Parse(exampleContent, options.ExamplePath));

        if (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }

        var redirected = !ReferenceEquals(stdout, Console.Out) || Console.IsOutputRedirected;
        var useColor = format == OutputFormat.Text
            && OutputSettings.ShouldUseColor(
                options.NoColor,
                Environment.GetEnvironmentVariable("NO_COLOR"),
                redirected);

        IResultFormatter formatter = format == OutputFormat.Json
            ? new JsonResultFormatter()
            : new TextResultFormatter();

        stdout.WriteLine(formatter.Format(
            comparison,
            options,
            useUnicode && format == OutputFormat.Text,
            useColor));

        return comparison.IsValid(options.Strict) ? 0 : 1;
    }

    private static CommandDefinition BuildCommand()
    {
        var env = new Option<string>("--env")
        {
            Description = "File to validate (default: .env)",
            DefaultValueFactory = _ => ".env",
        };
        var example = new Option<string>("--example")
        {
            Description = "Reference file (default: .env.example)",
            DefaultValueFactory = _ => ".env.example",
        };
        var strict = new Option<bool>("--strict")
        {
            Description = "Treat extra variables and parser warnings as errors",
        };
        var format = new Option<string>("--format")
        {
            Description = "Output format (default: text)",
            DefaultValueFactory = _ => "text",
        };
        var noColor = new Option<bool>("--no-color")
        {
            Description = "Disable colors (honors NO_COLOR)",
        };

        var root = new RootCommand("validate a .env file against a reference file");
        root.Options.Add(env);
        root.Options.Add(example);
        root.Options.Add(strict);
        root.Options.Add(format);
        root.Options.Add(noColor);

        return new CommandDefinition(root, env, example, strict, format, noColor);
    }

    private static bool TryRead(string path, TextWriter stderr, out string content)
    {
        content = string.Empty;

        try
        {
            content = EnvFileReader.Read(path);
            return true;
        }
        catch (DecoderFallbackException)
        {
            // The exception text can contain undecodable bytes, so it is never forwarded.
            stderr.WriteLine($"envparity: {path}: file is not valid UTF-8");
        }
        catch (FileNotFoundException)
        {
            stderr.WriteLine($"envparity: file not found: {path}");
        }
        catch (DirectoryNotFoundException)
        {
            stderr.WriteLine($"envparity: file not found: {path}");
        }
        catch (UnauthorizedAccessException)
        {
            stderr.WriteLine($"envparity: access denied: {path}");
        }
        catch (IOException)
        {
            stderr.WriteLine($"envparity: could not read file: {path}");
        }

        return false;
    }

    private static string DescribeParseError(System.CommandLine.ParseResult parseResult, string[] args)
    {
        if (parseResult.UnmatchedTokens.Count > 0)
        {
            return $"unrecognized argument: {parseResult.UnmatchedTokens[0]}";
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is not ("--env" or "--example" or "--format"))
            {
                continue;
            }

            if (i == args.Length - 1 || args[i + 1].StartsWith('-'))
            {
                return $"option '{args[i]}' requires a value";
            }
        }

        return "invalid arguments";
    }

    private static string HelpText(bool useUnicode)
    {
        var dash = useUnicode ? "—" : "-";

        return string.Join("\n", new[]
        {
            $"envparity {ToolInfo.Version} {dash} validate a .env file against a reference file",
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
        }) + "\n";
    }

    private sealed record CommandDefinition(
        RootCommand Root,
        Option<string> Env,
        Option<string> Example,
        Option<bool> Strict,
        Option<string> Format,
        Option<bool> NoColor);
}
