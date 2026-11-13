using System.Text.Json;
using EnvParity.Parsing;

namespace EnvParity.Tests.Parsing;

public sealed class EnvFileParserTests
{
    private const string Canary = "S3CR3T-CANARY-9f2";
    private const string FileLabel = ".env";

    private static ParseResult Parse(string content) => EnvFileParser.Parse(content, FileLabel);

    // Case 1
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData(" \t ")]
    public void Parse_BlankLine_Ignored(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        Assert.Empty(result.Issues);
    }

    // Case 2
    [Theory]
    [InlineData("# comment")]
    [InlineData("#")]
    [InlineData("# comment with = and \"quotes\"")]
    public void Parse_Comment_Ignored(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        Assert.Empty(result.Issues);
    }

    // Case 3
    [Theory]
    [InlineData("  # comment")]
    [InlineData("\t# comment")]
    public void Parse_IndentedComment_Ignored(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        Assert.Empty(result.Issues);
    }

    // Case 4
    [Theory]
    [InlineData("A=a#b", "A", "a#b")]
    [InlineData("URL=https://example.com?a=b#frag", "URL", "https://example.com?a=b#frag")]
    public void Parse_HashWithoutSpace_KeptInValue(string input, string name, string expected)
    {
        AssertSingleVariable(Parse(input), name, expected);
    }

    // Case 5
    [Theory]
    [InlineData("A=a # b")]
    [InlineData("A=a\t#b")]
    public void Parse_HashAfterSpace_StartsComment(string input)
    {
        AssertSingleVariable(Parse(input), "A", "a");
    }

    // Case 6
    [Theory]
    [InlineData("A=# b")]
    [InlineData("A=#")]
    public void Parse_HashAtValueStart_EmptyValue(string input)
    {
        AssertSingleVariable(Parse(input), "A", "");
    }

    // Case 7
    [Fact]
    public void Parse_HashInsideQuotes_Literal()
    {
        AssertSingleVariable(Parse("A=\"a # b\""), "A", "a # b");
    }

    // Case 8
    [Fact]
    public void Parse_HashInsideSingleQuotes_Literal()
    {
        AssertSingleVariable(Parse("A='a # b'"), "A", "a # b");
    }

    // Case 9
    [Theory]
    [InlineData("export A=1")]
    [InlineData("export\tA=1")]
    public void Parse_ExportPrefix_Stripped(string input)
    {
        AssertSingleVariable(Parse(input), "A", "1");
    }

    // Case 10
    [Theory]
    [InlineData("export    A=1")]
    [InlineData("export\t\tA=1")]
    public void Parse_ExportWithManySpaces_Stripped(string input)
    {
        AssertSingleVariable(Parse(input), "A", "1");
    }

    // Case 11
    [Theory]
    [InlineData("exportA=1", "exportA")]
    [InlineData("export=1", "export")]
    public void Parse_ExportWithoutSpace_IsKey(string input, string name)
    {
        AssertSingleVariable(Parse(input), name, "1");
    }

    // Case 12
    [Theory]
    [InlineData("A=\" x \"", " x ")]
    [InlineData("A='  '", "  ")]
    public void Parse_QuotedWhitespace_NotEmpty(string input, string expected)
    {
        AssertSingleVariable(Parse(input), "A", expected);
    }

    // Case 13
    [Fact]
    public void Parse_EmptyDoubleQuotes_Empty()
    {
        AssertSingleVariable(Parse("A=\"\""), "A", "");
    }

    // Case 14
    [Fact]
    public void Parse_EmptySingleQuotes_Empty()
    {
        AssertSingleVariable(Parse("A=''"), "A", "");
    }

    // Case 15
    [Fact]
    public void Parse_MissingValue_Empty()
    {
        AssertSingleVariable(Parse("A="), "A", "");
    }

    // Case 16
    [Theory]
    [InlineData("A=   ")]
    [InlineData("A=\t")]
    public void Parse_WhitespaceValue_Empty(string input)
    {
        AssertSingleVariable(Parse(input), "A", "");
    }

    // Case 17
    [Theory]
    [InlineData("A=x=y", "A", "x=y")]
    [InlineData(
        "DATABASE_URL=postgres://u:p@h:5432/db?sslmode=require",
        "DATABASE_URL",
        "postgres://u:p@h:5432/db?sslmode=require")]
    public void Parse_EqualsInValue_Kept(string input, string name, string expected)
    {
        AssertSingleVariable(Parse(input), name, expected);
    }

    // Case 18
    [Theory]
    [InlineData("A = x")]
    [InlineData("A\t=\tx")]
    public void Parse_SpacesAroundEquals_Trimmed(string input)
    {
        AssertSingleVariable(Parse(input), "A", "x");
    }

    // Case 19
    [Theory]
    [InlineData("  A=x")]
    [InlineData("\tA=x")]
    public void Parse_LeadingSpaces_Trimmed(string input)
    {
        AssertSingleVariable(Parse(input), "A", "x");
    }

    // Case 20
    [Theory]
    [InlineData("A=\"x\" extra")]
    [InlineData("A=\"x\"y")]
    [InlineData("A='x'z")]
    [InlineData("A='it\\'s'")]
    [InlineData("A=\"x\" S3CR3T-CANARY-9f2")]
    public void Parse_TrailingGarbageAfterQuote_Invalid(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "invalid-line", IssueSeverity.Error, 1);
        AssertNoCanary(result);
    }

    // Case 21
    [Theory]
    [InlineData("A=\"x")]
    [InlineData("A='x")]
    [InlineData("A=\"S3CR3T-CANARY-9f2")]
    public void Parse_UnterminatedQuote_Invalid(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "unterminated-quote", IssueSeverity.Error, 1);
        AssertNoCanary(result);
    }

    // Case 22
    [Theory]
    [InlineData("A=x\"y")]
    [InlineData("A='x\"y'")]
    public void Parse_QuoteInsideUnquotedValue_Literal(string input)
    {
        AssertSingleVariable(Parse(input), "A", "x\"y");
    }

    // Case 23
    [Theory]
    [InlineData("=x")]
    [InlineData(" =x")]
    [InlineData("\t=x")]
    public void Parse_EmptyKey_Invalid(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "invalid-key", IssueSeverity.Error, 1);
    }

    // Case 24
    [Theory]
    [InlineData("1A=x")]
    [InlineData("9=1")]
    [InlineData("1_A=1")]
    [InlineData("1S3CR3T-CANARY-9f2=x")]
    public void Parse_KeyStartingWithDigit_Invalid(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "invalid-key", IssueSeverity.Error, 1);
        AssertNoCanary(result);
    }

    // Case 25
    [Theory]
    [InlineData("A.B=x")]
    [InlineData("A-B=1")]
    public void Parse_KeyWithDot_Invalid(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "invalid-key", IssueSeverity.Error, 1);
    }

    // Case 26
    [Theory]
    [InlineData("A B=x")]
    [InlineData("A\tB=1")]
    public void Parse_KeyWithSpace_Invalid(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "invalid-key", IssueSeverity.Error, 1);
    }

    // Case 27
    [Theory]
    [InlineData("A")]
    [InlineData("A ")]
    [InlineData("export A")]
    [InlineData("S3CR3T-CANARY-9f2")]
    public void Parse_NoEquals_Invalid(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "invalid-line", IssueSeverity.Error, 1);
        AssertNoCanary(result);
    }

    // Case 28
    [Fact]
    public void Parse_DuplicateKey_LastWinsWithWarning()
    {
        var result = Parse("A=1\nA=2");

        var variable = Assert.Single(result.Variables).Value;
        Assert.Equal("A", variable.Name);
        Assert.Equal("2", variable.Value);
        Assert.Equal(1, variable.Line);

        var issue = AssertSingleIssue(result, "duplicate-key", IssueSeverity.Warning, 2);
        Assert.Contains("line 1", issue.Message, StringComparison.Ordinal);

        var repeated = Parse("A=1\nA=\nA=3");
        Assert.Equal("3", repeated.Variables["A"].Value);
        Assert.Equal(2, repeated.Issues.Count);
        Assert.Equal(2, repeated.Issues[0].Line);
        Assert.Equal(3, repeated.Issues[1].Line);
        Assert.All(repeated.Issues, warning =>
        {
            Assert.Equal("duplicate-key", warning.Code);
            Assert.Equal(IssueSeverity.Warning, warning.Severity);
            Assert.Contains("line 1", warning.Message, StringComparison.Ordinal);
        });
    }

    // Case 29
    [Theory]
    [InlineData("\uFEFFA=x", "x")]
    [InlineData("\uFEFF# comment\nA=1", "1")]
    public void Parse_LeadingBom_Ignored(string input, string expected)
    {
        AssertSingleVariable(Parse(input), "A", expected);
    }

    // Case 30
    [Theory]
    [InlineData("A=1\nB=2")]
    [InlineData("A=1\r\nB=2")]
    [InlineData("A=1\rB=2")]
    public void Parse_AllNewlineStyles_Accepted(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Issues);
        Assert.Equal(2, result.Variables.Count);
        Assert.Equal("1", result.Variables["A"].Value);
        Assert.Equal("2", result.Variables["B"].Value);
    }

    [Fact]
    public void Parse_MixedNewlineStyles_Accepted()
    {
        var result = Parse("A=1\nB=2\r\nC=3\rD=4");

        Assert.Empty(result.Issues);
        Assert.Equal(4, result.Variables.Count);
        Assert.Equal("1", result.Variables["A"].Value);
        Assert.Equal("2", result.Variables["B"].Value);
        Assert.Equal("3", result.Variables["C"].Value);
        Assert.Equal("4", result.Variables["D"].Value);
    }

    // Case 31
    [Fact]
    public void Parse_EmptyFile_NoIssues()
    {
        var result = Parse("");

        Assert.Empty(result.Variables);
        Assert.Empty(result.Issues);
    }

    // Case 32
    [Fact]
    public void Parse_NoTrailingNewline_Accepted()
    {
        var single = Parse("A=x");
        Assert.Empty(single.Issues);
        Assert.Single(single.Variables);
        Assert.Equal("x", single.Variables["A"].Value);

        var multiple = Parse("A=1\nB=2");
        Assert.Empty(multiple.Issues);
        Assert.Equal(2, multiple.Variables.Count);
        Assert.Equal("1", multiple.Variables["A"].Value);
        Assert.Equal("2", multiple.Variables["B"].Value);
    }

    // Case 34
    [Theory]
    [InlineData("export")]
    [InlineData("export ")]
    public void Parse_ExportOnly_Invalid(string input)
    {
        var result = Parse(input);

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "invalid-line", IssueSeverity.Error, 1);
    }

    // Case 35
    [Theory]
    [InlineData("A=🚀", "🚀")]
    [InlineData("A=ñandú", "ñandú")]
    [InlineData("A=日本", "日本")]
    public void Parse_UnicodeValue_Kept(string input, string expected)
    {
        AssertSingleVariable(Parse(input), "A", expected);
    }

    // Case 36
    [Theory]
    [InlineData("A=\"{\\\"a\\\":1}\"", "{\\\"a\\\":1}")]
    [InlineData("A=\"x\\\"\"", "x\\\"")]
    public void Parse_EscapedDoubleQuote_DoesNotCloseValue(string input, string expected)
    {
        AssertSingleVariable(Parse(input), "A", expected);
    }

    // Case 37
    [Fact]
    public void Parse_EvenBackslashes_BeforeQuote_Closes()
    {
        AssertSingleVariable(Parse("A=\"a\\\\\""), "A", "a\\\\");
    }

    // Case 38
    [Fact]
    public void Parse_EscapedQuoteAtEnd_Unterminated()
    {
        var result = Parse("A=\"x\\\"");

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "unterminated-quote", IssueSeverity.Error, 1);
    }

    // Case 39
    [Fact]
    public void Parse_BomAfterFirstCharacter_InvalidKey()
    {
        var result = Parse("A=1\n\uFEFFB=2");

        Assert.Single(result.Variables);
        Assert.Equal("1", result.Variables["A"].Value);
        AssertSingleIssue(result, "invalid-key", IssueSeverity.Error, 2);
    }

    // Case 40
    [Fact]
    public void Parse_UnterminatedQuote_DoesNotStopParsing()
    {
        var result = Parse("A=\"x\nB=1");

        Assert.Single(result.Variables);
        Assert.Equal("1", result.Variables["B"].Value);
        AssertSingleIssue(result, "unterminated-quote", IssueSeverity.Error, 1);
    }

    // Case 41
    [Fact]
    public void Parse_CommentAfterClosingQuote_Allowed()
    {
        AssertSingleVariable(Parse("A=\"x\" # nota"), "A", "x");
    }

    // Case 42
    [Fact]
    public void Parse_HashImmediatelyAfterClosingQuote_Comment()
    {
        AssertSingleVariable(Parse("A=\"x\"#nota"), "A", "x");
    }

    // Case 43
    [Fact]
    public void Parse_ExportBeforeEmptyKey_InvalidKey()
    {
        var result = Parse("export =1");

        Assert.Empty(result.Variables);
        AssertSingleIssue(result, "invalid-key", IssueSeverity.Error, 1);
    }

    private static void AssertSingleVariable(ParseResult result, string name, string value)
    {
        Assert.Empty(result.Issues);
        Assert.Single(result.Variables);
        AssertVariable(result, name, value);
    }

    private static void AssertVariable(ParseResult result, string name, string value)
    {
        Assert.True(result.Variables.ContainsKey(name));

        var variable = result.Variables[name];
        Assert.Equal(name, variable.Name);
        Assert.Equal(value, variable.Value);
        Assert.Equal(value.Length == 0, variable.IsEmpty);
    }

    private static ParseIssue AssertSingleIssue(
        ParseResult result,
        string code,
        IssueSeverity severity,
        int line)
    {
        var issue = Assert.Single(result.Issues);
        Assert.Equal(severity, issue.Severity);
        Assert.Equal(code, issue.Code);
        Assert.Equal(FileLabel, issue.File);
        Assert.Equal(line, issue.Line);
        Assert.DoesNotContain(Canary, issue.Message, StringComparison.Ordinal);
        return issue;
    }

    private static void AssertNoCanary(ParseResult result)
    {
        var serialized = JsonSerializer.Serialize(result);

        Assert.DoesNotContain(Canary, serialized, StringComparison.Ordinal);
        Assert.All(result.Issues, issue =>
            Assert.DoesNotContain(Canary, issue.Message, StringComparison.Ordinal));
    }
}
