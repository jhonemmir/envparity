using EnvParity.Parsing;

namespace EnvParity.Comparison;

/// <summary>
/// Comparison of the target file (E) against the reference file (X).
/// The three name lists are disjoint by construction and sorted ordinally.
/// </summary>
public sealed record ComparisonResult(
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Empty,
    IReadOnlyList<string> Extra,
    IReadOnlyList<ParseIssue> Issues,
    int VariableCount)
{
    public int ErrorCount => Issues.Count(issue => issue.Severity == IssueSeverity.Error);

    public int WarningCount => Issues.Count(issue => issue.Severity == IssueSeverity.Warning);

    /// <summary>
    /// Single source of truth for the validity formula. Parser errors are always
    /// errors; extras and parser warnings only fail in strict mode.
    /// </summary>
    public bool IsValid(bool strict) =>
        ErrorCount == 0
        && Missing.Count == 0
        && Empty.Count == 0
        && (Extra.Count == 0 || !strict)
        && (WarningCount == 0 || !strict);
}
