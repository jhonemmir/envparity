using System.Text.Json;
using EnvParity.Comparison;
using EnvParity.Cli;
using EnvParity.Output;
using EnvParity.Parsing;

namespace EnvParity.Tests.Output;

public sealed class JsonResultFormatterTests
{
    private static readonly CliOptions Default = new(
        ".env", ".env.example", Strict: false, OutputFormat.Json, NoColor: true);

    [Fact]
    public void Format_FixedPropertyOrder_MatchesContract()
    {
        var json = Format(Result());

        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(
            [
                "schemaVersion", "toolVersion", "valid", "strict", "files",
                "summary", "missing", "empty", "extra", "issues",
            ],
            names);
    }

    [Fact]
    public void Format_ContractValues_MatchPlanExample()
    {
        var result = Result(
            missing: ["API_KEY", "DATABASE_URL"],
            empty: ["SECRET_KEY"],
            extra: ["DEBUG"],
            issues:
            [
                new ParseIssue(
                    IssueSeverity.Warning,
                    "duplicate-key",
                    ".env",
                    12,
                    "Duplicate key; the last definition wins (first defined on line 11)."),
            ],
            variables: 10);

        using var document = JsonDocument.Parse(Format(result));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("1.0.1", root.GetProperty("toolVersion").GetString());
        Assert.False(root.GetProperty("valid").GetBoolean());
        Assert.False(root.GetProperty("strict").GetBoolean());
        Assert.Equal(".env", root.GetProperty("files").GetProperty("env").GetString());
        Assert.Equal(".env.example", root.GetProperty("files").GetProperty("example").GetString());

        var summary = root.GetProperty("summary");
        Assert.Equal(10, summary.GetProperty("variables").GetInt32());
        Assert.Equal(2, summary.GetProperty("missing").GetInt32());
        Assert.Equal(1, summary.GetProperty("empty").GetInt32());
        Assert.Equal(1, summary.GetProperty("extra").GetInt32());
        Assert.Equal(0, summary.GetProperty("errors").GetInt32());
        Assert.Equal(1, summary.GetProperty("warnings").GetInt32());

        Assert.Equal(
            ["API_KEY", "DATABASE_URL"],
            root.GetProperty("missing").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            ["SECRET_KEY"],
            root.GetProperty("empty").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            ["DEBUG"],
            root.GetProperty("extra").EnumerateArray().Select(item => item.GetString()!).ToArray());

        var issue = Assert.Single(root.GetProperty("issues").EnumerateArray());
        Assert.Equal("warning", issue.GetProperty("severity").GetString());
        Assert.Equal("duplicate-key", issue.GetProperty("code").GetString());
        Assert.Equal(".env", issue.GetProperty("file").GetString());
        Assert.Equal(12, issue.GetProperty("line").GetInt32());
        Assert.Equal(
            "Duplicate key; the last definition wins (first defined on line 11).",
            issue.GetProperty("message").GetString());
    }

    [Fact]
    public void Format_ValidResult_HasEmptyArrays()
    {
        using var document = JsonDocument.Parse(Format(Result(variables: 3)));

        Assert.True(document.RootElement.GetProperty("valid").GetBoolean());
        foreach (var name in new[] { "missing", "empty", "extra", "issues" })
        {
            Assert.Empty(document.RootElement.GetProperty(name).EnumerateArray());
        }
    }

    [Fact]
    public void Format_Severity_IsLowercase()
    {
        var result = Result(
            issues:
            [
                new ParseIssue(IssueSeverity.Error, "invalid-line", ".env", 3, "fixed message"),
            ]);

        using var document = JsonDocument.Parse(Format(result));
        var issue = Assert.Single(document.RootElement.GetProperty("issues").EnumerateArray());

        Assert.Equal("error", issue.GetProperty("severity").GetString());
    }

    [Fact]
    public void Format_NeverEmitsAValueProperty()
    {
        var json = Format(Result(missing: ["API_KEY"], extra: ["DEBUG"], variables: 2));

        Assert.DoesNotContain("value", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("line", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_IgnoresColorAndUnicodeFlags()
    {
        var result = Result(missing: ["A"]);
        var formatter = new JsonResultFormatter();

        var plain = formatter.Format(result, Default, useUnicode: true, useColor: true);
        var other = formatter.Format(result, Default, useUnicode: false, useColor: false);

        Assert.Equal(plain, other);
        Assert.DoesNotContain("\u001B", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("✓", plain, StringComparison.Ordinal);
    }

    private static string Format(ComparisonResult result) =>
        new JsonResultFormatter().Format(result, Default, useUnicode: false, useColor: false);

    private static ComparisonResult Result(
        string[]? missing = null,
        string[]? empty = null,
        string[]? extra = null,
        ParseIssue[]? issues = null,
        int variables = 0) =>
        new(missing ?? [], empty ?? [], extra ?? [], issues ?? [], variables);
}
