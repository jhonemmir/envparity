namespace EnvParity.Parsing;

/// <summary>
/// Result of parsing one file. <see cref="Variables"/> has no ordering guarantee;
/// <see cref="Issues"/> is ordered by ascending line.
/// </summary>
public sealed record ParseResult(
    IReadOnlyDictionary<string, EnvVariable> Variables,
    IReadOnlyList<ParseIssue> Issues);
