namespace EnvParity.Parsing;

public enum IssueSeverity
{
    Warning = 0,
    Error = 1,
}

/// <summary>
/// A parser issue. <see cref="Message"/> is a fixed template: it never contains
/// file content, only the file label and line numbers when needed.
/// </summary>
public sealed record ParseIssue(
    IssueSeverity Severity,
    string Code,
    string File,
    int Line,
    string Message);

internal static class IssueCodes
{
    internal const string DuplicateKey = "duplicate-key";
    internal const string InvalidLine = "invalid-line";
    internal const string InvalidKey = "invalid-key";
    internal const string UnterminatedQuote = "unterminated-quote";
}

internal static class IssueMessages
{
    internal const string InvalidLine = "Invalid line (expected KEY=VALUE).";
    internal const string InvalidKey = "Invalid key (expected [A-Za-z_][A-Za-z0-9_]*).";
    internal const string UnterminatedQuote = "Unterminated quoted value.";

    internal static string DuplicateKey(int firstLine) =>
        FormattableString.Invariant(
            $"Duplicate key; the last definition wins (first defined on line {firstLine}).");
}
