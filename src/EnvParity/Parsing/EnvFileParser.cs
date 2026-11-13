namespace EnvParity.Parsing;

/// <summary>
/// Pure parser for the <c>.env</c> grammar. It performs no I/O and never throws
/// for content problems: every problem becomes an issue in the result.
/// </summary>
public static class EnvFileParser
{
    private const char Bom = '\uFEFF';
    private const string ExportPrefix = "export";

    /// <summary>
    /// Parses <paramref name="content"/>. <paramref name="fileLabel"/> is the path
    /// exactly as the user typed it and is used only in issues.
    /// </summary>
    public static ParseResult Parse(string content, string fileLabel)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileLabel);

        var variables = new Dictionary<string, EnvVariable>(StringComparer.Ordinal);
        var issues = new List<ParseIssue>();

        // Only a BOM at the very start of the file is ignored. Anywhere else it is
        // a normal character and makes the key invalid.
        var text = content.StartsWith(Bom) ? content[1..] : content;

        using var reader = new StringReader(text);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            ParseLine(line, lineNumber, fileLabel, variables, issues);
        }

        return new ParseResult(variables, issues);
    }

    private static void ParseLine(
        string line,
        int lineNumber,
        string fileLabel,
        Dictionary<string, EnvVariable> variables,
        List<ParseIssue> issues)
    {
        var current = line.TrimStart(' ', '\t');

        if (current.Length == 0 || current[0] == '#')
        {
            return;
        }

        if (current.StartsWith(ExportPrefix, StringComparison.Ordinal)
            && current.Length > ExportPrefix.Length
            && (current[ExportPrefix.Length] == ' ' || current[ExportPrefix.Length] == '\t'))
        {
            current = current[ExportPrefix.Length..].TrimStart(' ', '\t');
        }

        var equalsIndex = current.IndexOf('=');
        if (equalsIndex < 0)
        {
            issues.Add(Error(IssueCodes.InvalidLine, fileLabel, lineNumber, IssueMessages.InvalidLine));
            return;
        }

        var key = current[..equalsIndex].TrimEnd(' ', '\t');
        if (!IsValidKey(key))
        {
            issues.Add(Error(IssueCodes.InvalidKey, fileLabel, lineNumber, IssueMessages.InvalidKey));
            return;
        }

        var valueText = current[(equalsIndex + 1)..].TrimStart(' ', '\t');
        string value;

        if (valueText.Length > 0 && (valueText[0] == '"' || valueText[0] == '\''))
        {
            var quote = valueText[0];
            var closingIndex = FindClosingQuote(valueText, quote);

            if (closingIndex < 0)
            {
                issues.Add(Error(
                    IssueCodes.UnterminatedQuote,
                    fileLabel,
                    lineNumber,
                    IssueMessages.UnterminatedQuote));
                return;
            }

            value = valueText[1..closingIndex];

            var trailing = valueText[(closingIndex + 1)..].TrimStart(' ', '\t');
            if (trailing.Length > 0 && trailing[0] != '#')
            {
                issues.Add(Error(IssueCodes.InvalidLine, fileLabel, lineNumber, IssueMessages.InvalidLine));
                return;
            }
        }
        else
        {
            value = TrimInlineComment(valueText);
        }

        var variable = new EnvVariable(key, value, lineNumber, value.Length == 0);

        if (variables.TryGetValue(key, out var existing))
        {
            issues.Add(new ParseIssue(
                IssueSeverity.Warning,
                IssueCodes.DuplicateKey,
                fileLabel,
                lineNumber,
                IssueMessages.DuplicateKey(existing.Line)));

            // The variable keeps the first line; only the value is replaced.
            variables[key] = variable with { Line = existing.Line };
        }
        else
        {
            variables.Add(key, variable);
        }
    }

    private static int FindClosingQuote(string text, char quote)
    {
        if (quote == '\'')
        {
            return text.IndexOf('\'', 1);
        }

        for (var i = 1; i < text.Length; i++)
        {
            if (text[i] != quote)
            {
                continue;
            }

            var backslashes = 0;
            for (var j = i - 1; j >= 0 && text[j] == '\\'; j--)
            {
                backslashes++;
            }

            // An odd number of backslashes escapes the quote; an even number closes it.
            if (backslashes % 2 == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static string TrimInlineComment(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '#' && (i == 0 || text[i - 1] is ' ' or '\t'))
            {
                return text[..i].TrimEnd(' ', '\t');
            }
        }

        return text.TrimEnd(' ', '\t');
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length == 0)
        {
            return false;
        }

        var first = key[0];
        if (!char.IsAsciiLetter(first) && first != '_')
        {
            return false;
        }

        for (var i = 1; i < key.Length; i++)
        {
            var c = key[i];
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static ParseIssue Error(string code, string file, int line, string message) =>
        new(IssueSeverity.Error, code, file, line, message);
}
