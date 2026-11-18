using EnvParity.Comparison;
using EnvParity.Cli;
using EnvParity.Parsing;

namespace EnvParity.Output;

/// <summary>Human-readable contract from plan 2.6 and 4.2.</summary>
public sealed class TextResultFormatter : IResultFormatter
{
    private const string Green = "\u001B[32m";
    private const string Red = "\u001B[31m";
    private const string Yellow = "\u001B[33m";
    private const string Cyan = "\u001B[36m";
    private const string Reset = "\u001B[0m";

    public string Format(
        ComparisonResult result,
        CliOptions options,
        bool useUnicode,
        bool useColor)
    {
        var ok = useUnicode ? "✓" : "OK";
        var bad = useUnicode ? "✗" : "X";
        var warn = useUnicode ? "⚠" : "!";

        var problems = ProblemCount(result, options.Strict);
        var warnings = WarningCount(result, options.Strict);
        var valid = result.IsValid(options.Strict);

        var blocks = new List<string>();

        if (valid)
        {
            var symbol = Paint(ok, Green, useColor);
            var env = Paint(options.EnvPath, Cyan, useColor);
            var example = Paint(options.ExamplePath, Cyan, useColor);
            blocks.Add(
                $"{symbol} {env} is valid ({result.VariableCount} variable{Suffix(result.VariableCount)} checked against {example})");
        }

        var groups = new List<string>();

        if (result.Missing.Count > 0)
        {
            var lines = new List<string>
            {
                $"{Paint(bad, Red, useColor)} {result.Missing.Count} missing variable{Suffix(result.Missing.Count)}:",
            };
            lines.AddRange(result.Missing.Select(name => $"  {name}"));
            groups.Add(string.Join("\n", lines));
        }

        if (result.Empty.Count > 0)
        {
            var lines = new List<string>
            {
                $"{Paint(bad, Red, useColor)} {result.Empty.Count} empty variable{Suffix(result.Empty.Count)}:",
            };
            lines.AddRange(result.Empty.Select(name => $"  {name}"));
            groups.Add(string.Join("\n", lines));
        }

        if (result.Extra.Count > 0)
        {
            string header;
            if (options.Strict)
            {
                header = $"{Paint(bad, Red, useColor)} {result.Extra.Count} extra variable{Suffix(result.Extra.Count)}:";
            }
            else if (result.Extra.Count == 1)
            {
                header = $"{Paint(warn, Yellow, useColor)} 1 extra variable (use --strict to treat it as an error):";
            }
            else
            {
                header = $"{Paint(warn, Yellow, useColor)} {result.Extra.Count} extra variables (use --strict to treat them as errors):";
            }

            var lines = new List<string> { header };
            lines.AddRange(result.Extra.Select(name => $"  {name}"));
            groups.Add(string.Join("\n", lines));
        }

        var issueLines = result.Issues
            .Select(issue => FormatIssue(issue, bad, warn, useColor))
            .ToArray();
        if (issueLines.Length > 0)
        {
            groups.Add(string.Join("\n", issueLines));
        }

        if (groups.Count > 0)
        {
            blocks.Add(string.Join("\n\n", groups));
        }

        if (problems > 0 || warnings > 0)
        {
            var parts = new List<string>();
            if (problems > 0)
            {
                parts.Add($"{problems} problem{Suffix(problems)}");
            }

            if (warnings > 0)
            {
                parts.Add($"{warnings} warning{Suffix(warnings)}");
            }

            blocks.Add($"envparity: {string.Join(", ", parts)}");
        }

        return string.Join("\n\n", blocks);
    }

    private static string FormatIssue(
        ParseIssue issue,
        string bad,
        string warn,
        bool useColor)
    {
        var isError = issue.Severity == IssueSeverity.Error;
        var symbol = isError ? bad : warn;
        var color = isError ? Red : Yellow;
        var file = Paint(issue.File, Cyan, useColor);

        return $"{Paint(symbol, color, useColor)} {file}:{issue.Line} {Describe(issue)}";
    }

    private static string Describe(ParseIssue issue) => issue.Code switch
    {
        "invalid-line" => "invalid line (expected KEY=VALUE)",
        "invalid-key" => "invalid key (expected [A-Za-z_][A-Za-z0-9_]*)",
        "unterminated-quote" => "unterminated quoted value",
        _ => issue.Message,
    };

    private static int ProblemCount(ComparisonResult result, bool strict) =>
        result.Missing.Count
        + result.Empty.Count
        + result.ErrorCount
        + (strict ? result.Extra.Count + result.WarningCount : 0);

    private static int WarningCount(ComparisonResult result, bool strict) =>
        strict ? 0 : result.Extra.Count + result.WarningCount;

    private static string Suffix(int count) => count == 1 ? string.Empty : "s";

    private static string Paint(string text, string color, bool useColor) =>
        useColor ? $"{color}{text}{Reset}" : text;
}
