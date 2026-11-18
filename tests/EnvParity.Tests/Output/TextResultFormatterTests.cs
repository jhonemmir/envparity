using EnvParity.Comparison;
using EnvParity.Cli;
using EnvParity.Output;
using EnvParity.Parsing;

namespace EnvParity.Tests.Output;

public sealed class TextResultFormatterTests
{
    private static readonly CliOptions Default = new(
        ".env", ".env.example", Strict: false, OutputFormat.Text, NoColor: false);

    [Fact]
    public void Format_NoProblems_ValidHeader()
    {
        var text = Format(Result(variables: 12));

        Assert.Equal(
            "✓ .env is valid (12 variables checked against .env.example)",
            text);
    }

    [Fact]
    public void Format_MissingEmptyExtra_MatchesPlanExample()
    {
        var result = Result(
            missing: ["API_KEY", "DATABASE_URL"],
            empty: ["SECRET_KEY"],
            extra: ["DEBUG"],
            variables: 10);

        var text = Format(result);

        Assert.Equal(
            """
            ✗ 2 missing variables:
              API_KEY
              DATABASE_URL

            ✗ 1 empty variable:
              SECRET_KEY

            ⚠ 1 extra variable (use --strict to treat it as an error):
              DEBUG

            envparity: 3 problems, 1 warning
            """,
            text);
    }

    [Fact]
    public void Format_ParseErrors_MatchesPlanExample()
    {
        var result = Result(
            issues:
            [
                Error("invalid-line", ".env", 12),
                Error("unterminated-quote", ".env", 20),
            ]);

        var text = Format(result);

        Assert.Equal(
            """
            ✗ .env:12 invalid line (expected KEY=VALUE)
            ✗ .env:20 unterminated quoted value

            envparity: 2 problems
            """,
            text);
    }

    [Fact]
    public void Format_AsciiFallback_UsesOkXAndBang()
    {
        var result = Result(
            missing: ["A"],
            extra: ["B"],
            issues: [Warning("duplicate-key", ".env", 4)]);

        var text = Format(result, useUnicode: false);

        Assert.Contains("X 1 missing variable:", text, StringComparison.Ordinal);
        Assert.Contains("! 1 extra variable", text, StringComparison.Ordinal);
        Assert.Contains("! .env:4", text, StringComparison.Ordinal);
        Assert.DoesNotContain("✓", text, StringComparison.Ordinal);
        Assert.DoesNotContain("✗", text, StringComparison.Ordinal);
        Assert.DoesNotContain("⚠", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_Colors_WhenEnabled()
    {
        var valid = Format(Result(variables: 1), useColor: true);
        Assert.Contains("\u001B[32m✓\u001B[0m", valid, StringComparison.Ordinal);
        Assert.Contains("\u001B[36m.env\u001B[0m", valid, StringComparison.Ordinal);

        var invalid = Format(
            Result(missing: ["A"], issues: [Warning("duplicate-key", ".env", 2)]),
            useColor: true);

        Assert.Contains("\u001B[31m✗\u001B[0m 1 missing variable:", invalid, StringComparison.Ordinal);
        Assert.Contains("\u001B[33m⚠\u001B[0m \u001B[36m.env\u001B[0m:2", invalid, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_StrictExtra_IsReportedAsError()
    {
        var options = Default with { Strict = true };
        var result = Result(extra: ["DEBUG", "LOCAL_ONLY"], variables: 2);

        var text = Format(result, options: options);

        Assert.Equal(
            """
            ✗ 2 extra variables:
              DEBUG
              LOCAL_ONLY

            envparity: 2 problems
            """,
            text);
    }

    [Fact]
    public void Format_MultipleExtrasNonStrict_PluralMessage()
    {
        var text = Format(Result(extra: ["DEBUG", "LOCAL_ONLY"], variables: 2));

        Assert.Contains(
            "⚠ 2 extra variables (use --strict to treat them as errors):",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UnknownIssueCode_UsesFixedMessage()
    {
        var result = Result(
            issues: [new ParseIssue(IssueSeverity.Error, "future-code", ".env", 5, "fixed message")]);

        var text = Format(result);

        Assert.Contains("✗ .env:5 fixed message", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_WarningsOnly_KeepsValidHeaderAndAddsSummary()
    {
        var result = Result(issues: [Warning("duplicate-key", ".env", 7)], variables: 1);

        var text = Format(result);

        Assert.Equal(
            """
            ✓ .env is valid (1 variable checked against .env.example)

            ⚠ .env:7 Duplicate key; the last definition wins (first defined on line 1).

            envparity: 1 warning
            """,
            text);
    }

    private static string Format(
        ComparisonResult result,
        bool useUnicode = true,
        bool useColor = false,
        CliOptions? options = null) =>
        new TextResultFormatter().Format(result, options ?? Default, useUnicode, useColor);

    private static ComparisonResult Result(
        string[]? missing = null,
        string[]? empty = null,
        string[]? extra = null,
        ParseIssue[]? issues = null,
        int variables = 0) =>
        new(missing ?? [], empty ?? [], extra ?? [], issues ?? [], variables);

    private static ParseIssue Error(string code, string file, int line) =>
        new(IssueSeverity.Error, code, file, line, "fixed message");

    private static ParseIssue Warning(string code, string file, int line) =>
        new(IssueSeverity.Warning, code, file, line,
            "Duplicate key; the last definition wins (first defined on line 1).");
}
