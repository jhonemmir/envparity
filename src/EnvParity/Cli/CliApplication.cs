using System.Text;
using EnvParity.Parsing;

namespace EnvParity.Cli;

/// <summary>
/// Testable entry point. <c>stdout</c> receives only the result; <c>stderr</c>
/// receives only usage or I/O errors. On exit code 2, <c>stdout</c> stays empty.
/// </summary>
public static class CliApplication
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var envPath = ".env";

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--env" when i + 1 < args.Length:
                    envPath = args[++i];
                    break;
                case "--env":
                    stderr.WriteLine("envparity: option '--env' requires a value");
                    return 2;
                default:
                    stderr.WriteLine($"envparity: unrecognized argument: {args[i]}");
                    return 2;
            }
        }

        string content;
        try
        {
            content = EnvFileReader.Read(envPath);
        }
        catch (DecoderFallbackException)
        {
            // The exception text can contain the bytes that failed to decode, so
            // it is never forwarded: the message is a fixed template.
            stderr.WriteLine($"envparity: {envPath}: file is not valid UTF-8");
            return 2;
        }
        catch (FileNotFoundException)
        {
            stderr.WriteLine($"envparity: file not found: {envPath}");
            return 2;
        }
        catch (DirectoryNotFoundException)
        {
            stderr.WriteLine($"envparity: file not found: {envPath}");
            return 2;
        }
        catch (UnauthorizedAccessException)
        {
            stderr.WriteLine($"envparity: access denied: {envPath}");
            return 2;
        }
        catch (IOException)
        {
            stderr.WriteLine($"envparity: could not read file: {envPath}");
            return 2;
        }

        _ = EnvFileParser.Parse(content, envPath);
        return 0;
    }
}
