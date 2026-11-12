# Parser test cases — envparity 1.0

This document is the **executable specification** of the `.env` parser. Every numbered case maps to exactly one xUnit test. The parser is not considered correct until all 43 cases pass.

Related: [ADR 0001 — Design decisions](adr/0001-design-decisions.md).

## 1. Parser contract

```csharp
ParseResult EnvFileParser.Parse(string content, string fileLabel);
```

- **Input:** the full file content as a string, plus a label used in issues (**the path exactly as the user typed it**; it is never derived from file content).
- **Output:** `ParseResult` with the unique variables and every issue found.
- **Pure:** no filesystem, no environment variables, no clock, no culture-sensitive operations.
- **Never throws for content problems:** malformed input becomes an `issue`, not an exception.
- **Never stops early:** every line is processed so the report is complete.

The `fileLabel` is display metadata only. The CLI passes the `--env`/`--example` argument string verbatim and never builds the label from anything read inside the file.

Reading the file, decoding UTF-8 and mapping I/O failures to `exit 2` are the responsibility of `EnvFileReader` and `CliApplication`, not the parser.

## 2. Notation

| Symbol | Meaning |
|---|---|
| `·` | One literal space |
| `⇥` | One literal tab |
| `⏎` | Line break (`\n` unless stated otherwise) |
| `∅` | Empty value (length 0) |
| `\uFEFF` | UTF-8 BOM |
| `—` | Nothing: no variables or no issues |

Inputs are single lines unless the case shows `⏎`. Leading and trailing `·`/`⇥` are significant.

## 3. Normative algorithm

1. Read the file as **bytes** and decode them as strict UTF-8 with `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes)`. Do **not** use `File.ReadAllText` or `StreamReader`: they auto-detect UTF-16/UTF-32 BOMs and would accept a UTF-16 file. Any decode failure is a CLI-level `exit 2`.
2. Remove a single leading BOM (`U+FEFF`) if present. `GetString` preserves it as a character.
3. Split into lines on `\r\n`, `\n` or `\r`, keeping 1-based line numbers (`StringReader.ReadLine()` handles all three).
4. For each line:
   1. Trim **only spaces and tabs** at the start (`TrimStart(' ', '\t')`); never `Trim()`.
   2. If the line is empty or starts with `#`, ignore it.
   3. If it starts with `export` followed by at least one space or tab, remove that prefix and trim leading spaces/tabs again.
   4. Find the **first** `=`. If it does not exist, emit `invalid-line` and continue with the next line.
   5. The key is the text before `=`, right-trimmed with `TrimEnd(' ', '\t')`. It must match `\A[A-Za-z_][A-Za-z0-9_]*\z`; otherwise emit `invalid-key` and continue.
   6. The value text is the text after `=`, left-trimmed with `TrimStart(' ', '\t')`.
   7. If the value text starts with `"` or `'`:
      - scan for the closing quote of the **same** type on the same line;
      - inside **double** quotes, a `"` preceded by an **odd** number of consecutive `\` does not close the value; inside single quotes there is no escaping;
      - if there is no closing quote, emit `unterminated-quote` and continue;
      - the value is the raw text between the outer quotes. Backslashes are **not** removed (D9); they only affect delimiter scanning;
      - the text after the closing quote, left-trimmed, must be empty or start with `#` (with or without preceding whitespace); otherwise emit `invalid-line` and continue.
   8. Otherwise (unquoted value):
      - cut at the first `#` that is at index 0 or preceded by a space or tab;
      - right-trim the resulting value with `TrimEnd(' ', '\t')`.
   9. `IsEmpty` is `value.Length == 0`.
   10. If the key already exists, emit a `duplicate-key` **warning** on the current line (the message mentions the first line) and replace the value. Otherwise, add the variable.

## 4. Invariants

1. Same input + same label → identical output.
2. No content problem throws; every problem is an issue.
3. All lines are processed; the report is never truncated at the first error.
4. Exactly one variable per key; duplicates collapse and the last definition wins.
5. `duplicate-key` is the only warning. Every other issue is an error.
6. Line numbers are 1-based; issues are ordered by ascending line.
7. Unquoted values are right-trimmed only. Quoted values are never trimmed.
8. `IsEmpty` means length 0. A quoted space (`" "`) is **not** empty.
9. Keys are ASCII (`[A-Za-z_][A-Za-z0-9_]*`) and case-sensitive. Validate with `\A…\z` or a character loop, never with `$`.
10. Values are opaque: no unescaping, no `${VAR}` interpolation, no variable expansion. Backslashes only affect where a double-quoted value ends; they are preserved in the raw value.
11. A BOM is ignored only at the very start of the file. A BOM anywhere else is a normal character and makes the key invalid.
12. `ParseResult.Variables` has no ordering guarantee; the comparer is responsible for sorting (ordinal).
13. Single-quoted values cannot contain a single quote; `A='it\'s'` closes at the quote after the backslash and the rest is `invalid-line`.
14. No issue message contains file content. Issues carry only `code`, `file` and `line`; `message` is a fixed template. This includes invalid keys, unparseable lines and decoder errors.

## 5. Issue catalog

| Code | Severity | Meaning |
|---|---|---|
| `duplicate-key` | Warning | The key was already defined; the last definition wins |
| `invalid-line` | Error | No `=`, or trailing content after a quoted value |
| `invalid-key` | Error | The key does not match the key grammar |
| `unterminated-quote` | Error | A quoted value has no closing quote on the same line |

`code` is stable and machine-readable. `message` is human-readable and may change between minor versions, but it **never contains any part of the file**. Specifically:

- `invalid-line`, `invalid-key` and `unterminated-quote` never quote the offending line, the key candidate or the value fragment.
- `duplicate-key` mentions the first line number, never the line text or the value.

A message may interpolate only the file label and line numbers. See [leakage assertions](#leakage-assertions-d10).

## 6. Cases

Cases 20, 21, 24, 27 and 33 also carry a canary assertion for D10 (see *Leakage assertions* below).

### A. Lines and full-line comments

Blank lines, whitespace-only lines and lines whose first non-space character is `#` are ignored.

| ID | Input | Variables | Issues | Test |
|---|---|---|---|---|
| 1 | `·` (or empty line) | — | — | `Parse_BlankLine_Ignored` |
| 2 | `# comment` | — | — | `Parse_Comment_Ignored` |
| 3 | `··# comment` | — | — | `Parse_IndentedComment_Ignored` |

### B. Inline comments

Outside quotes, `#` starts a comment only when it is at the start of the value or preceded by a space/tab. Inside quotes it is literal.

| ID | Input | Variables | Issues | Test |
|---|---|---|---|---|
| 4 | `A=a#b` | `A` → `a#b` | — | `Parse_HashWithoutSpace_KeptInValue` |
| 5 | `A=a·#·b` | `A` → `a` | — | `Parse_HashAfterSpace_StartsComment` |
| 6 | `A=#·b` | `A` → `∅` | — | `Parse_HashAtValueStart_EmptyValue` |
| 7 | `A="a·#·b"` | `A` → `a # b` | — | `Parse_HashInsideQuotes_Literal` |
| 8 | `A='a·#·b'` | `A` → `a # b` | — | `Parse_HashInsideSingleQuotes_Literal` |

### C. Optional `export` prefix

`export` is stripped only when followed by at least one space or tab. Otherwise it is part of the key.

| ID | Input | Variables | Issues | Test |
|---|---|---|---|---|
| 9 | `export·A=1` | `A` → `1` | — | `Parse_ExportPrefix_Stripped` |
| 10 | `export····A=1` | `A` → `1` | — | `Parse_ExportWithManySpaces_Stripped` |
| 11 | `exportA=1` | `exportA` → `1` | — | `Parse_ExportWithoutSpace_IsKey` |
| 34 | `export` | — | `invalid-line @1` (Error) | `Parse_ExportOnly_Invalid` |

### D. Keys and separator

The key is right-trimmed and must match `\A[A-Za-z_][A-Za-z0-9_]*\z`. Whitespace around `=` is not part of either side.

| ID | Input | Variables | Issues | Test |
|---|---|---|---|---|
| 18 | `A·=·x` | `A` → `x` | — | `Parse_SpacesAroundEquals_Trimmed` |
| 19 | `··A=x` | `A` → `x` | — | `Parse_LeadingSpaces_Trimmed` |
| 23 | `=x` | — | `invalid-key @1` (Error) | `Parse_EmptyKey_Invalid` |
| 24 | `1A=x` | — | `invalid-key @1` (Error) | `Parse_KeyStartingWithDigit_Invalid` |
| 25 | `A.B=x` | — | `invalid-key @1` (Error) | `Parse_KeyWithDot_Invalid` |
| 26 | `A·B=x` | — | `invalid-key @1` (Error) | `Parse_KeyWithSpace_Invalid` |
| 27 | `A` | — | `invalid-line @1` (Error) | `Parse_NoEquals_Invalid` |

**D10 canary (case 24):** the theory includes `1S3CR3T-CANARY-9f2=x`, which is an `invalid-key`. The test asserts that the canary appears neither in `message` nor in any serialized form of the result: the invalid key candidate is never echoed.

**D10 canary (case 27):** the same test also parses `S3CR3T-CANARY-9f2` (no `=`) and asserts that the canary appears neither in `message` nor in any serialized form of the result. The invalid line is never echoed.

### E. Values, quotes and emptiness

A quoted value starts with the quote as its first non-space character. Inside double quotes, `\"` does **not** terminate the value (a quote preceded by an odd number of consecutive backslashes is escaped); inside single quotes, a backslash has no special meaning. The value is returned **raw**: backslashes are not removed. After the closing quote, `#` starts a comment with or without preceding whitespace.

| ID | Input | Variables | Issues | Test |
|---|---|---|---|---|
| 12 | `A="·x·"` | `A` → `·x·` (3 chars, not empty) | — | `Parse_QuotedWhitespace_NotEmpty` |
| 13 | `A=""` | `A` → `∅` | — | `Parse_EmptyDoubleQuotes_Empty` |
| 14 | `A=''` | `A` → `∅` | — | `Parse_EmptySingleQuotes_Empty` |
| 15 | `A=` | `A` → `∅` | — | `Parse_MissingValue_Empty` |
| 16 | `A=···` | `A` → `∅` | — | `Parse_WhitespaceValue_Empty` |
| 17 | `A=x=y` | `A` → `x=y` | — | `Parse_EqualsInValue_Kept` |
| 20 | `A="x"·extra` | — | `invalid-line @1` (Error) | `Parse_TrailingGarbageAfterQuote_Invalid` |
| 21 | `A="x` | — | `unterminated-quote @1` (Error) | `Parse_UnterminatedQuote_Invalid` |
| 22 | `A=x"y` | `A` → `x"y` | — | `Parse_QuoteInsideUnquotedValue_Literal` |
| 35 | `A=🚀` | `A` → `🚀` | — | `Parse_UnicodeValue_Kept` |

**D10 canary (cases 20 and 21):** each test also uses an input containing `S3CR3T-CANARY-9f2` (`A="x"·S3CR3T-CANARY-9f2` and `A="S3CR3T-CANARY-9f2`) and asserts that the canary appears neither in `message` nor in any serialized form of the result. The offending line is never echoed.

### F. File-level behavior

| ID | Input | Variables | Issues | Test |
|---|---|---|---|---|
| 28 | `A=1⏎A=2` | `A` → `2` | `duplicate-key @2` (Warning) | `Parse_DuplicateKey_LastWinsWithWarning` |
| 29 | `\uFEFFA=x` | `A` → `x` | — | `Parse_LeadingBom_Ignored` |
| 30 | `A=1⏎B=2` with CRLF, LF and CR variants | `A` → `1`, `B` → `2` | — | `Parse_AllNewlineStyles_Accepted` |
| 31 | `∅` (empty file) | — | — | `Parse_EmptyFile_NoIssues` |
| 32 | `A=x` with no trailing newline | `A` → `x` | — | `Parse_NoTrailingNewline_Accepted` |
| 33 | Invalid UTF-8 (`0xC3 0x28`) or a UTF-16 LE file (`0xFF 0xFE …`) | Parser is not reached | CLI: `exit 2` + stderr message | `Run_NonUtf8File_Returns2` |

Case 33 is a CLI-level test because the failure happens before parsing: `EnvFileReader` throws `DecoderFallbackException` and `CliApplication` returns `2`. The reader must use `File.ReadAllBytes` + `new UTF8Encoding(false, true).GetString(bytes)`; `File.ReadAllText`/`StreamReader` auto-detect the UTF-16 BOM and would accept the file.

**D10 requirements for case 33:** the `stderr` message is a **fixed template** (for example `<file>: file is not valid UTF-8`); the text of `DecoderFallbackException` is never forwarded, because it can contain the bytes that failed to decode. The test also seeds `S3CR3T-CANARY-9f2` in the UTF-16 content and asserts that it appears neither in `stderr` nor in `stdout`; `stdout` must be empty.

### G. Cases added during review

Cases 36–38 fix a real-world false positive: JSON credentials such as `A="{\"a\":1}"` are common in `.env` files and must parse. Values remain opaque (D9): backslashes are preserved, they only affect delimiter scanning.

| ID | Input | Variables | Issues | Test |
|---|---|---|---|---|
| 36 | `A="{\"a\":1}"` | `A` → `{\"a\":1}` (raw) | — | `Parse_EscapedDoubleQuote_DoesNotCloseValue` |
| 37 | `A="a\\"` | `A` → `a\\` (raw) | — | `Parse_EvenBackslashes_BeforeQuote_Closes` |
| 38 | `A="x\"` | — | `unterminated-quote @1` (Error) | `Parse_EscapedQuoteAtEnd_Unterminated` |
| 39 | `A=1⏎\uFEFFB=2` | `A` → `1` | `invalid-key @2` (Error) | `Parse_BomAfterFirstCharacter_InvalidKey` |
| 40 | `A="x⏎B=1` | `B` → `1` | `unterminated-quote @1` (Error) | `Parse_UnterminatedQuote_DoesNotStopParsing` |
| 41 | `A="x"·#·nota` | `A` → `x` | — | `Parse_CommentAfterClosingQuote_Allowed` |
| 42 | `A="x"#nota` | `A` → `x` | — | `Parse_HashImmediatelyAfterClosingQuote_Comment` |
| 43 | `export·=1` | — | `invalid-key @1` (Error) | `Parse_ExportBeforeEmptyKey_InvalidKey` |

Remaining edge behavior covered by theory data: duplicate warnings are emitted once per redefinition (case 28), `export=1` is the key `export` (case 11), and `A=a⇥#b` treats the tab-preceded hash as a comment (case 5).

### Leakage assertions (D10)

These are **extra assertions inside existing tests**, not new case IDs, so the total stays 43. Each test seeds the canary `S3CR3T-CANARY-9f2` in the input and asserts that the string appears **neither in `message` nor in the serialized output**.

| Case | Test | Seeded input | Assertions |
|---|---|---|---|
| 20 | `Parse_TrailingGarbageAfterQuote_Invalid` | `A="x"·S3CR3T-CANARY-9f2` | `message` and serialized output contain no canary |
| 21 | `Parse_UnterminatedQuote_Invalid` | `A="S3CR3T-CANARY-9f2` | `message` and serialized output contain no canary |
| 24 | `Parse_KeyStartingWithDigit_Invalid` | `1S3CR3T-CANARY-9f2=x` | `message` and serialized output contain no canary |
| 27 | `Parse_NoEquals_Invalid` | `S3CR3T-CANARY-9f2` | `message` and serialized output contain no canary |
| 33 | `Run_NonUtf8File_Returns2` | UTF-16 LE file whose text contains `S3CR3T-CANARY-9f2` | `stderr` is a fixed message with no canary; `stdout` is empty; exit code `2` |

Three consequences for the implementation:

- `invalid-key` must **not** echo the invalid key. `1S3CR3T-CANARY-9f2=x` is the regression test: the message says only that the key is invalid, with file and line.
- `unterminated-quote` and `invalid-line` must **not** echo the line. A trailing-garbage or unterminated-quote line is exactly where a value lives.
- `fileLabel` is the user-supplied path, never anything read from the file. It is display metadata, not content.

The CLI-level JSON check uses the same canary: a file with a secret value and a parse error, run with `--format json`, must serialize only names, codes, `file` and `line`. On `exit 2` there is no JSON at all: `stdout` is empty and the fixed error goes to `stderr`.

## 7. Additional theory data

The 43 test methods above can cover more inputs through `[Theory]` without adding new case IDs. Recommended additions:

| Case | Additional inputs | Expected |
|---|---|---|
| 4 | `URL=https://example.com?a=b#frag` | value keeps `#frag` |
| 5 | `A=a⇥#b` | value `a` |
| 6 | `A=#` | empty value |
| 9 | `export⇥A=1` | key `A`, value `1` |
| 10 | `export⇥⇥A=1` | key `A`, value `1` |
| 11 | `export=1` | key `export`, value `1` |
| 12 | `A='··'` | value of two spaces, not empty |
| 17 | `DATABASE_URL=postgres://u:p@h:5432/db?sslmode=require` | value kept verbatim |
| 18 | `A⇥=⇥x` | key `A`, value `x` |
| 19 | `⇥A=x` | key `A`, value `x` |
| 20 | `A="x"y`, `A='x'z`, `A='it\'s'` | `invalid-line` |
| 21 | `A='x` | `unterminated-quote` |
| 22 | `A='x"y'` | value `x"y` |
| 23 | `·=x` | `invalid-key` |
| 24 | `9=1`, `1_A=1`, `1S3CR3T-CANARY-9f2=x` | `invalid-key`; the message contains no canary |
| 25 | `A-B=1` | `invalid-key` |
| 26 | `A⇥B=1` | `invalid-key` |
| 27 | `A·`, `export·A` | `invalid-line` |
| 28 | `A=1⏎A=⏎A=3` | value `3`, warnings on lines 2 and 3 |
| 29 | `\uFEFF# comment⏎A=1` | key `A`, value `1` |
| 30 | Mixed endings: `A=1⏎B=2\r\nC=3\rD=4` | four variables |
| 32 | `A=1⏎B=2` without trailing newline | both variables |
| 35 | `A=ñandú`, `A=日本` | values kept verbatim |
| 36 | `A="x\""` | value `x\"` (raw) |

## 8. Out of scope for v1.0

- Multiline quoted values; the closing quote must be on the same line.
- Escape **interpretation**: `\n`, `\t` and `\\` are not converted and stay literal. The only escape-aware behavior is delimiter scanning, where `\"` does not close a double-quoted value (cases 36–38).
- Variable interpolation (`${VAR}`) and expansion.
- NUL bytes and binary content; non-UTF-8 input fails with `exit 2`.
- Keys containing dots, dashes or non-ASCII characters; they are `invalid-key`.
- Cascading files (`.env.local`, `.env.production`, …); out of scope for the whole tool.

## 9. Test skeleton

```csharp
public class EnvFileParserTests
{
    private static ParseResult Parse(string text) => EnvFileParser.Parse(text, ".env");

    [Theory]
    [InlineData("A=a#b", "a#b")]
    [InlineData("URL=https://example.com?a=b#frag", "https://example.com?a=b#frag")]
    public void Parse_HashWithoutSpace_KeptInValue(string input, string expected)
    {
        var result = Parse(input);

        Assert.Empty(result.Issues);
        Assert.Equal(expected, Assert.Single(result.Variables).Value.Value);
    }

    [Fact]
    public void Parse_NoEquals_Invalid()
    {
        var result = Parse("A");

        Assert.Empty(result.Variables);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("invalid-line", issue.Code);
        Assert.Equal(1, issue.Line);
    }
}
```

Rules for adding cases:

1. Add a row with the next ID and a unique test name.
2. Never add a case without a test, or a test without a row.
3. Update the total (currently **43**) in this document and in the project plan.
4. If a new case changes a decision, update [ADR 0001](adr/0001-design-decisions.md) first.

## 10. Implementation notes

- **Read bytes, not text.** `File.ReadAllBytes(path)` + `new UTF8Encoding(false, true).GetString(bytes)`. Never `File.ReadAllText`/`StreamReader`: they detect UTF-16 BOMs. The UTF-8 BOM survives as `U+FEFF` and is removed by the parser.
- **Trim the right characters.** Use `TrimStart(' ', '\t')` and `TrimEnd(' ', '\t')`. `Trim()` also removes Unicode whitespace such as NBSP, which contradicts the spec.
- **Validate keys precisely.** Use `\A[A-Za-z_][A-Za-z0-9_]*\z` or a character loop. In .NET, `$` also matches before a trailing `\n`.
- **Split lines simply.** `StringReader.ReadLine()` handles `\r\n`, `\n` and `\r` and gives you a 1-based counter.
- **Scan escaped quotes correctly.** Inside double quotes, count the consecutive `\` immediately before a `"`. An odd count means the quote is escaped; an even count means it closes the value. Do not remove the backslashes from the raw value.
- **Never echo input in messages.** Build `message` from fixed templates plus `file` and `line` only. In particular, do not include the invalid-key candidate or the offending line.
- **Catch decoder failures.** Wrap `GetString` in `try/catch (DecoderFallbackException)` and emit a fixed message. The exception text can contain the bytes that failed to decode.

---

## Resumen en español

Esta es la especificación ejecutable del parser: **43 casos, un test por caso**.

- **Contrato:** función pura `Parse(content, fileLabel)` que nunca lanza por errores de contenido y procesa todas las líneas.
- **Reglas clave:** claves ASCII case-sensitive, `export` opcional solo con espacio/tab, `#` inicia comentario únicamente fuera de comillas y al inicio del valor o tras un espacio, valores opacos y `IsEmpty` = longitud cero.
- **Comillas dobles:** `\"` no cierra el valor (se cuenta la paridad de `\`); el valor se devuelve crudo, sin desescapar.
- **Errores:** `invalid-line`, `invalid-key` y `unterminated-quote` son errores; `duplicate-key` es el único warning.
- **Nivel archivo:** BOM inicial ignorado, CRLF/LF/CR aceptados, archivo vacío válido, cualquier archivo no UTF-8 (incluido UTF-16) sale con `2`.
- **Sin fugas:** los mensajes son plantillas fijas con archivo y línea; nunca citan la línea, la clave inválida ni el valor. El `fileLabel` es la ruta tal como la escribió el usuario, nunca algo leído del archivo. Las excepciones de decodificación se sustituyen por un mensaje fijo. Los casos 20, 21, 24, 27 y 33 siembran una canaria y comprueban que no aparece.
- **Mantenimiento:** cada caso nuevo exige una fila, un test y actualizar el total en este documento y en el plan.
