namespace EnvParity.Cli;

public enum OutputFormat
{
    Text = 0,
    Json = 1,
}

/// <summary>Validated command-line options. Paths are kept exactly as typed by the user.</summary>
public sealed record CliOptions(
    string EnvPath,
    string ExamplePath,
    bool Strict,
    OutputFormat Format,
    bool NoColor);
