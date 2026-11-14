using EnvParity.Parsing;

namespace EnvParity.Comparison;

/// <summary>
/// Pure comparison of two parsed files. It does not format or decide the exit code.
/// </summary>
public static class EnvComparer
{
    public static ComparisonResult Compare(ParseResult env, ParseResult example)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(example);

        var missing = example.Variables.Keys
            .Where(key => !env.Variables.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var extra = env.Variables.Keys
            .Where(key => !example.Variables.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        // D1: "empty" only applies to keys present in both files. An empty extra
        // is reported as extra, never duplicated here.
        var empty = env.Variables
            .Where(pair => pair.Value.IsEmpty && example.Variables.ContainsKey(pair.Key))
            .Select(pair => pair.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var issues = env.Issues
            .Concat(example.Issues)
            .OrderBy(issue => issue.File, StringComparer.Ordinal)
            .ThenBy(issue => issue.Line)
            .ToArray();

        return new ComparisonResult(missing, empty, extra, issues, env.Variables.Count);
    }
}
