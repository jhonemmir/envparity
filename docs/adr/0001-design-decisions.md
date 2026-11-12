# ADR 0001 — Design decisions for envcheck 1.0

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-11-11 |
| Revised | 2026-11-11 — review pass 2: 43 cases, escaped delimiters, no-content messages, `exit 2` stdout, OIDC first publish |
| Owners | Project maintainer |
| Scope | v1.0 |
| Related | [Parser test cases](../parser-cases.md) |

## Context

`envcheck` is a .NET global tool that compares a `.env` file against a reference file (`.env.example` by default) and reports variables that are **missing**, **empty** or **extra**. It is a portfolio project with a deliberately small scope, but it must behave like production software: deterministic output, safe defaults, tests for every ambiguity and a package installable from NuGet.

The difficult part is not the comparison, it is the `.env` format. There is no single specification: comments, quoting, `export`, inline `#`, duplicate keys and line endings are handled differently by different tools. A validator that guesses silently is worse than one that fails loudly.

This ADR records the decisions that shape v1.0. They are binding for the implementation, the tests, the CLI contract and the README. Anything not listed here is out of scope or belongs in the roadmap.

## Decision drivers

1. A small scope that can be finished and polished in one to two weeks.
2. CI-friendly behavior: stable exit codes and machine-readable output.
3. Never leak secret values.
4. Every ambiguous parser case has a documented decision and a test.
5. Minimal runtime dependencies and identical behavior on Linux, macOS and Windows.

## Decisions

| ID | Decision | Choice | Rationale |
|---|---|---|---|
| D1 | Scope | Compare variable names and detect empty values **only for variables present in both files** (`empty` = `{ k ∈ E ∩ X : value(k).Length == 0 }`). Values are never compared or validated. | Keeps the tool small, explainable and useful; a key missing from one side cannot be "empty". |
| D2 | Language | Code, CLI messages, README, CHANGELOG and engineering docs in English; optional `README.es.md`. | The NuGet audience is international; a Spanish summary stays available on request. |
| D3 | Case sensitivity | Variable names are always **case-sensitive**. | Files are text; OS environment semantics do not apply. |
| D4 | Classification | `missing`, `empty` and `extra` are **disjoint sets by construction**. | Every variable appears exactly once in the report. |
| D5 | Duplicate keys | Last definition wins; emit a `duplicate-key` warning that mentions both lines. | Matches common dotenv behavior while keeping the anomaly visible. |
| D6 | Invalid lines | Always an **error** (`exit 1`); report every invalid line with file and line number. | Silent misconfiguration is the failure mode we are preventing. |
| D7 | Encoding | UTF-8 only, strict. Read **bytes** and decode with `UTF8Encoding(false, true)`; never auto-detect BOMs. A leading BOM is ignored; invalid UTF-8 (including UTF-16 files) is a usage/I-O error (`exit 2`). | Avoids silent corruption and rejects UTF-16 files that only look valid. |
| D8 | Multiline values | Not supported in v1.0; reported as `unterminated-quote` or `invalid-line`. | High complexity, low value for a key-oriented validator. |
| D9 | Escapes | Not interpreted in the value. Values are **opaque**; backslashes are preserved. Delimiter scanning is escape-aware: `\"` does not close a double-quoted value (odd/even backslash parity). | JSON credentials such as `A="{\"a\":1}"` are common; ignoring this creates false positives in CI. |
| D10 | Secrets | No value is ever printed or serialized, only variable names. Issues never contain text from the file: `message` is a fixed template, and the only input-derived data are `file` and line numbers. Exceptions are mapped to fixed messages. | CI logs are shared and often public; quoting the offending line is the most likely leak. |
| D11 | Target framework | `net10.0`. | .NET 8 reached end of support on 2026-11-10; .NET 10 is LTS until 2028. |
| D12 | Dependencies | `System.CommandLine` **3.0.0** pinned (verified on NuGet 2026-11-11); xUnit + `coverlet.collector` for tests. | Official parser, stable, minimal surface. |
| D13 | CLI contract | Results on `stdout`, errors on `stderr`, versioned JSON (`schemaVersion`), exit codes `0`/`1`/`2`/`130`; `--help` and `--version` exit `0`. With `exit 2`, `stdout` is **empty**. | Scriptable, stable and safe for CI. |
| D14 | `--strict` | Escalates `extra` variables and parser warnings to errors. | One switch with one meaning. |

### Detail: exit code contract (D13)

| Code | Meaning | When |
|---|---|---|
| `0` | Valid | No problems, or warnings only in non-strict mode |
| `1` | Validation problems | Missing, empty, parse errors; plus extras and duplicates under `--strict` |
| `2` | Usage or I/O error | Invalid argument, missing/unreadable file, invalid UTF-8 |
| `130` | Interrupted | Ctrl+C |

`--help` and `--version` always exit `0`. A stack trace is never shown to the user.

`exit 2` means the tool never produced a result, so **nothing is written to `stdout`**: the diagnostic goes only to `stderr`. Consumers can rely on the invariant *empty stdout + code 2* and must not try to parse it as JSON. A partially written JSON document is never emitted.

### Detail: classification sets (D1, D4)

Let `E` be the keys of the file under validation and `X` the keys of the reference file:

| Bucket | Definition | Effect |
|---|---|---|
| `missing` | `X \ E` | Error |
| `empty` | `{ k ∈ E ∩ X : value(k).Length == 0 }` | Error |
| `extra` | `E \ X` | Warning; error only with `--strict` |

`empty` is computed **only on the intersection**, so a key that exists on one side only is reported as `missing` or `extra`, never as `empty`. The three buckets are disjoint by construction and their union is `E ∪ X`.

### Detail: no content in messages (D10)

An `Issue` exposes a stable `code`, a `severity`, a `file` label, a 1-based `line` and a human-readable `message`. The only values derived from the input are `file` and line numbers: `message` is a **fixed template** that may interpolate only those.

- `invalid-line`, `invalid-key` and `unterminated-quote` never quote the offending line, the key candidate or the value fragment.
- `duplicate-key` mentions the first line number, never the line text or the value.
- `DecoderFallbackException` is caught and replaced by a fixed message such as `"<file>: file is not valid UTF-8"`. The exception text is never forwarded: it can include the untranslatable bytes.
- `file` is the path exactly as the user typed it. The CLI never derives it from anything read inside the file.
- The same rule applies to `text` and `json` output.

This is verified by canary assertions: cases 20, 21, 24, 27 and 33 seed a unique string in the input and assert that it appears neither in `message` nor in the serialized output.

### Detail: validity rule (D4, D14)

```
valid = errors == 0
     && missing.Count == 0
     && empty.Count == 0
     && (extra.Count == 0 || !strict)
     && (warnings == 0 || !strict)
```

This rule lives in exactly one place (`ComparisonResult.IsValid(strict)`). The CLI never re-implements it.

### Detail: file reading (D7)

`EnvFileReader` reads bytes and decodes them explicitly:

```csharp
var bytes = File.ReadAllBytes(path);
var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
    .GetString(bytes);
```

`File.ReadAllText` and `StreamReader` auto-detect UTF-16/UTF-32 BOMs, so a UTF-16 file would be accepted. The reader must reject it. The UTF-8 BOM is preserved as `U+FEFF` and removed by the parser.

### Detail: quoted values (D9)

- Inside double quotes, a `"` preceded by an **odd** number of consecutive `\` does not close the value.
- Inside single quotes there is no escaping; the first `'` closes.
- The value is the raw text between the outer quotes. Backslashes are not removed.
- After the closing quote, `#` starts a comment with or without preceding whitespace. For unquoted values, `#` requires a preceding space/tab or the start of the value.

### Detail: first publish and OIDC (D12, release auth)

The alternatives table rejects long-lived API keys in favour of OIDC trusted publishing, and the risk table asks for an early `0.1.0-alpha.1` to reserve the package ID. These are compatible: NuGet trusted publishing policies have a scope for **publishing new packages** (with a package glob), so the **first** publication of a brand-new ID can use OIDC; no bootstrap API key is required. Verified in the [NuGet trusted publishing documentation](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) on 2026-11-11.

Operational consequences:

- The policy must be created on nuget.org **before** the first push and is owned by an individual user or an organization.
- The first push assigns package ownership to the nuget.org account whose username is passed to `NuGet/login` (`user`). Store that profile name as `NUGET_USER`; it is not the email address.
- Temporary API keys last **1 hour** and one OIDC token yields exactly **one** key, so the login step must run immediately before the push.
- For some repositories (notably private GitHub repos) a new policy is only **temporarily active for 7 days** and becomes permanent after a successful publish. The alpha must be pushed inside that window, or the window restarted.

Only if trusted publishing were unavailable for a brand-new ID would the fallback be a scoped, short-expiry API key for the first push, migrated to OIDC immediately afterwards. That fallback is documented but not the plan.

## Consequences

### Positive

- The parser is pure and exhaustively specified, so tests are fast and trustworthy.
- Output is deterministic and sorted, which makes snapshots and CI diffs stable.
- The tool is safe to run in shared CI logs because values never leave the file.
- The exit code contract makes it usable as a pipeline gate without parsing text.
- Low dependency surface and cross-platform behavior reduce supply-chain and support risk.

### Negative / trade-offs

- No typed validation: `PORT=abc` is not detected. Deferred to the roadmap on purpose.
- No interpolation or cascading files: teams that rely on those patterns need another tool.
- Values are opaque and returned raw, so consumers expecting unescaped values are surprised; the CLI never prints values, so this only affects parser tests and future features.
- "Empty" means length zero after removing the outer quotes; `VAR=" "` is not empty. This is documented and tested.
- Duplicates are accepted with a warning; teams that want them rejected must use `--strict`.
- Consumers need the .NET 10 runtime; the tool is not self-contained.
- Canonical docs are in English; the Spanish summaries are shorter by design.

### Risks

| Risk | Mitigation |
|---|---|
| Package ID `envcheck` was free on 2026-11-11 but is not reserved | Publish `0.1.0-alpha.1` as soon as the CLI works, using OIDC (which supports first-time publishing of a new ID) |
| `System.CommandLine` 3.0.0 is recent and most online examples target 2.0-beta | Pin the exact version and isolate parsing in `CliApplication` |
| Parser ambiguity regressions | The 43-case table is the executable spec; no case is handled without a row |
| Output contract drift | Snapshot tests for `text` and `json` |
| Secret leakage through a future feature | D10 is enforced by tests and by the security policy |

## Alternatives considered

| Area | Alternative | Why it was rejected |
|---|---|---|
| Language/runtime | Go, Python or Rust | The portfolio goal is C#/.NET and distribution through NuGet |
| Argument parsing | Hand-rolled parser | Error-prone, worse help output, more tests for less value |
| Argument parsing | Spectre.Console.Cli | Heavier dependency focused on rich UI; unnecessary for three flags |
| Scope | Compare values and validate formats | Turns a small tool into a type system and increases secret exposure |
| Duplicate keys | Reject the file | Breaks many real-world `.env` files; a warning is more useful |
| Duplicate keys | First definition wins | Differs from common dotenv implementations |
| Escaped quotes | Treat `\"` as the closing quote (no escape scanning) | Produces false positives on JSON credentials in real `.env` files; the limitation would be documented instead of fixed |
| Invalid lines | Ignore or warn | Hides real misconfiguration, which is the exact problem the tool exists to catch |
| Output | Human-readable only | Not composable; CI needs a machine contract |
| Output | SARIF | Valuable but overkill for v1.0; it is in the roadmap |
| Tests | Fixture files on disk | Fragile because of line endings and `.gitignore` rules |
| Coverage | Codecov | External dependency and permission noise for a small project |
| Release auth | Long-lived NuGet API key | OIDC trusted publishing avoids a rotatable secret; verified 2026-11-11 that its "publish new packages" scope covers the first push of a new ID |
| Release auth | API key for the first alpha, OIDC afterwards | Unnecessary: OIDC can perform the first publish, so there is no bootstrap exception to maintain |
| Distribution | Docker image or standalone binary | Extra surface that does not demonstrate the .NET tooling goal |

## Compliance

Every decision is enforced by something concrete:

| Decision | Enforced by |
|---|---|
| D1 | README scope + roadmap; `EnvComparer` computes `empty` on `E ∩ X` only; no value comparison in code |
| D2 | Code review and docs checklist |
| D3 | `EnvComparer` uses ordinal comparison; `Compare_CaseDifferingKeys_AreDistinct` |
| D4 | `EnvComparerTests` and the validity rule in `ComparisonResult` |
| D5 | `Parse_DuplicateKey_LastWinsWithWarning` |
| D6 | Parser cases 20, 21, 23–27, 34, 38–40, 43 |
| D7 | `Run_NonUtf8File_Returns2`, UTF-8 BOM case 29 and `EnvFileReader` |
| D8 | Parser cases 21 and 40; out-of-scope section |
| D9 | Parser cases 36–38 (delimiter scanning) and 41–42 (comment after the closing quote) |
| D10 | Canary assertions in parser cases 20, 21, 24, 27 and CLI case 33; output snapshot tests assert that no value appears; `SECURITY.md` |
| D11 | `global.json`, `Directory.Build.props`, CI matrix |
| D12 | Pinned `<PackageReference>`; CI restore |
| D13 | Formatter tests, CLI tests, JSON contract snapshot and an `exit 2` test asserting empty `stdout` |
| D14 | `EnvComparer` strict tests and CLI integration tests |

## Follow-ups and revisit triggers

- **Typed validation:** add it behind a new command or flag in v2, never by changing comparison semantics.
- **`System.CommandLine`:** if a newer 3.0.x or 4.x appears, read the changelog and update this ADR before upgrading.
- **Strict semantics:** if users ask to separate "extras" from "warnings", split the flag instead of overloading `--strict`.
- **Package ID:** if `envcheck` becomes unavailable, rename `PackageId` before 1.0; `ToolCommandName` can stay `envcheck`.
- **JSON schema:** bump `schemaVersion` only for breaking changes; additive changes keep version 1.

---

## Resumen en español

Este ADR fija las 14 decisiones que definen la v1.0 de `envcheck`:

- **Alcance mínimo:** solo se comparan nombres y se detectan valores vacíos **en la intersección `E ∩ X`**; nunca se comparan ni imprimen valores.
- **Clasificación disjunta:** faltante, vacía y sobrante; cada variable aparece en una sola lista. Una clave presente solo en un lado nunca es «vacía».
- **Parser explícito:** claves ASCII case-sensitive, `export` opcional, comentarios `#` solo fuera de comillas (en valores sin comillas, precedidos de espacio), duplicados con «gana la última» y warning, líneas inválidas como error.
- **UTF-8 estricto**, BOM inicial ignorado, valores multilínea fuera de alcance. `\"` no cierra una comilla doble, pero el valor se devuelve crudo (sin desescapar).
- **Sin fugas (D10):** un issue solo lleva código, archivo y línea; los mensajes son plantillas fijas y nunca citan texto del archivo. Las excepciones de decodificación se traducen a un mensaje fijo.
- **Contrato de CLI:** resultado en `stdout`, errores en `stderr`, JSON versionado y exit codes `0/1/2/130`. Con `exit 2` no se escribe nada en `stdout`.
- **`--strict`** escala sobrantes y warnings a error.
- **`net10.0`** y una sola dependencia de runtime, `System.CommandLine 3.0.0`.
- **Publicación:** OIDC trusted publishing cubre la primera publicación de un ID nuevo (scope «publish new packages»), así que no hace falta una API key de arranque.

La tabla de casos del parser es la especificación ejecutable: **43 casos**, cada decisión ambigua con su fila y su test. Cualquier comportamiento que no esté aquí está fuera de alcance o en el roadmap.
