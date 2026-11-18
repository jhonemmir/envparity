using System.Text.Encodings.Web;
using System.Text.Json;
using EnvParity.Comparison;
using EnvParity.Cli;

namespace EnvParity.Output;

/// <summary>
/// Versioned machine contract. JSON is always plain ASCII-safe output: no colors,
/// no Unicode symbols, regardless of the console settings (plan 4.1 and 4.3).
/// </summary>
public sealed class JsonResultFormatter : IResultFormatter
{
    internal const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Format(
        ComparisonResult result,
        CliOptions options,
        bool useUnicode,
        bool useColor)
    {
        var output = new JsonOutput(
            SchemaVersion,
            ToolInfo.Version,
            result.IsValid(options.Strict),
            options.Strict,
            new JsonFiles(options.EnvPath, options.ExamplePath),
            new JsonSummary(
                result.VariableCount,
                result.Missing.Count,
                result.Empty.Count,
                result.Extra.Count,
                result.ErrorCount,
                result.WarningCount),
            result.Missing,
            result.Empty,
            result.Extra,
            result.Issues
                .Select(issue => new JsonIssue(
                    issue.Severity.ToString().ToLowerInvariant(),
                    issue.Code,
                    issue.File,
                    issue.Line,
                    issue.Message))
                .ToArray());

        return JsonSerializer.Serialize(output, SerializerOptions);
    }
}
