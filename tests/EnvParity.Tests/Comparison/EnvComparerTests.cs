using EnvParity.Comparison;
using EnvParity.Parsing;

namespace EnvParity.Tests.Comparison;

public sealed class EnvComparerTests
{
    [Fact]
    public void Compare_NoDifferences_Valid()
    {
        var result = Compare("A=1\nB=2", "A=\nB=");

        Assert.Empty(result.Missing);
        Assert.Empty(result.Empty);
        Assert.Empty(result.Extra);
        Assert.Empty(result.Issues);
        Assert.Equal(2, result.VariableCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.True(result.IsValid(strict: false));
        Assert.True(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_OnlyMissing_Invalid()
    {
        var result = Compare("A=1", "A=\nB=\nC=");

        Assert.Collection(
            result.Missing,
            name => Assert.Equal("B", name),
            name => Assert.Equal("C", name));
        Assert.Empty(result.Empty);
        Assert.Empty(result.Extra);
        Assert.Equal(1, result.VariableCount);
        Assert.False(result.IsValid(strict: false));
        Assert.False(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_OnlyExtra_ValidUnlessStrict()
    {
        var result = Compare("A=1\nB=2", "A=");

        Assert.Empty(result.Missing);
        Assert.Empty(result.Empty);
        Assert.Equal("B", Assert.Single(result.Extra));
        Assert.Equal(2, result.VariableCount);
        Assert.True(result.IsValid(strict: false));
        Assert.False(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_OnlyEmpty_Invalid()
    {
        var result = Compare("A=\nB=\"\"", "A=\nB=");

        Assert.Empty(result.Missing);
        Assert.Collection(
            result.Empty,
            name => Assert.Equal("A", name),
            name => Assert.Equal("B", name));
        Assert.Empty(result.Extra);
        Assert.False(result.IsValid(strict: false));
        Assert.False(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_AllCategories_AreDisjoint()
    {
        var result = Compare("PRESENT=1\nEMPTY=\nDEBUG=1", "PRESENT=\nEMPTY=\nAPI_KEY=");

        Assert.Equal("API_KEY", Assert.Single(result.Missing));
        Assert.Equal("EMPTY", Assert.Single(result.Empty));
        Assert.Equal("DEBUG", Assert.Single(result.Extra));
        Assert.Equal(3, result.VariableCount);

        Assert.DoesNotContain("DEBUG", result.Empty);
        Assert.DoesNotContain("DEBUG", result.Missing);
        Assert.DoesNotContain("EMPTY", result.Extra);
        Assert.False(result.IsValid(strict: false));
    }

    [Fact]
    public void Compare_EmptyExtra_ReportedOnlyAsExtra()
    {
        var result = Compare("A=1\nDEBUG=", "A=");

        Assert.Empty(result.Missing);
        Assert.Empty(result.Empty);
        Assert.Equal("DEBUG", Assert.Single(result.Extra));
        Assert.True(result.IsValid(strict: false));
        Assert.False(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_KeysAreCaseSensitive()
    {
        var result = Compare("A=1", "a=");

        Assert.Equal("a", Assert.Single(result.Missing));
        Assert.Equal("A", Assert.Single(result.Extra));
        Assert.Empty(result.Empty);
        Assert.False(result.IsValid(strict: false));
    }

    [Fact]
    public void Compare_EmptyOnlyInIntersection_ExampleEmptyDoesNotCount()
    {
        var result = Compare("A=1", "A=");

        Assert.Empty(result.Missing);
        Assert.Empty(result.Empty);
        Assert.Empty(result.Extra);
        Assert.True(result.IsValid(strict: false));
        Assert.True(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_QuotedSpace_IsNotEmpty()
    {
        var result = Compare("A=\" \"", "A=");

        Assert.Empty(result.Empty);
        Assert.True(result.IsValid(strict: false));
    }

    [Fact]
    public void Compare_StrictWithDuplicateWarnings_Invalid()
    {
        var result = Compare("A=1\nA=2", "A=");

        Assert.Equal(1, result.WarningCount);
        Assert.Equal(0, result.ErrorCount);

        var warning = Assert.Single(result.Issues);
        Assert.Equal("duplicate-key", warning.Code);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
        Assert.Equal(2, warning.Line);

        Assert.True(result.IsValid(strict: false));
        Assert.False(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_StrictWithWarningInExample_Invalid()
    {
        var result = Compare("A=1", "A=\nA=2");

        var warning = Assert.Single(result.Issues);
        Assert.Equal("duplicate-key", warning.Code);
        Assert.Equal(".env.example", warning.File);
        Assert.True(result.IsValid(strict: false));
        Assert.False(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_ParserErrorInEnv_AlwaysInvalid()
    {
        var result = Compare("A=1\nB", "A=");

        Assert.Empty(result.Missing);
        Assert.Empty(result.Empty);
        Assert.Empty(result.Extra);
        Assert.Equal(1, result.ErrorCount);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("invalid-line", issue.Code);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.False(result.IsValid(strict: false));
        Assert.False(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_ParserErrorInExample_AlwaysInvalid()
    {
        var result = Compare("A=1", "A=\nB");

        Assert.Empty(result.Missing);
        Assert.Empty(result.Empty);
        Assert.Empty(result.Extra);
        Assert.Equal(1, result.ErrorCount);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(".env.example", issue.File);
        Assert.False(result.IsValid(strict: false));
        Assert.False(result.IsValid(strict: true));
    }

    [Fact]
    public void Compare_ListsAreSortedOrdinal()
    {
        var result = Compare("Z=1\na=1\nM=", "M=\ny=\nB=");

        Assert.Collection(
            result.Missing,
            name => Assert.Equal("B", name),
            name => Assert.Equal("y", name));
        Assert.Collection(
            result.Empty,
            name => Assert.Equal("M", name));
        Assert.Collection(
            result.Extra,
            name => Assert.Equal("Z", name),
            name => Assert.Equal("a", name));
    }

    [Fact]
    public void Compare_IssuesAreSortedByFileThenLine()
    {
        var result = Compare("A\nB", "C");

        Assert.Collection(
            result.Issues,
            issue =>
            {
                Assert.Equal(".env", issue.File);
                Assert.Equal(1, issue.Line);
            },
            issue =>
            {
                Assert.Equal(".env", issue.File);
                Assert.Equal(2, issue.Line);
            },
            issue =>
            {
                Assert.Equal(".env.example", issue.File);
                Assert.Equal(1, issue.Line);
            });
    }

    [Fact]
    public void Compare_VariableCount_IsUniqueKeysInEnv()
    {
        var result = Compare("A=1\nA=2\nB=1", "A=\nB=");

        Assert.Equal(2, result.VariableCount);
        Assert.Equal(1, result.WarningCount);
        Assert.True(result.IsValid(strict: false));
        Assert.False(result.IsValid(strict: true));
    }

    private static ComparisonResult Compare(string env, string example) =>
        EnvComparer.Compare(
            EnvFileParser.Parse(env, ".env"),
            EnvFileParser.Parse(example, ".env.example"));
}
