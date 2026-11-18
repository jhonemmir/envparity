namespace EnvParity.Cli;

/// <summary>Pure policy for ANSI colors. Kept separate so it can be tested directly.</summary>
public static class OutputSettings
{
    public static bool ShouldUseColor(bool noColorOption, string? noColorEnvironment, bool redirected) =>
        !noColorOption
        && string.IsNullOrEmpty(noColorEnvironment)
        && !redirected;
}
