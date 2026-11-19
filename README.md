# envparity

[![CI](https://github.com/jhonemmir/envparity/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/jhonemmir/envparity/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/envparity)](https://www.nuget.org/packages/envparity)
[![NuGet downloads](https://img.shields.io/nuget/dt/envparity)](https://www.nuget.org/packages/envparity)
[![License: MIT](https://img.shields.io/github/license/jhonemmir/envparity)](LICENSE)

A .NET global tool that validates a `.env` file against a reference file
(`.env.example` by default) and reports variables that are **missing**, **empty**
or **extra**. Values are never printed, so it is safe to run in shared CI logs.

![envparity demo](docs/demo.gif)

## Why

A missing or empty environment variable is a common cause of broken deployments
and confusing startup errors. `envparity` checks the shape of a `.env` file
before the application starts, and it keeps `.env.example` honest: new keys must
be documented and obsolete ones removed. It is a one-purpose tool: no value
comparison, no interpolation, no cascading `.env.local` files.

## Install

```bash
dotnet tool install -g envparity
```

Requires the .NET 10 runtime. Make sure the global tools directory is on your
`PATH`:

| Platform | Directory |
|---|---|
| Linux / macOS | `$HOME/.dotnet/tools` |
| Windows | `%USERPROFILE%\.dotnet\tools` |

`dotnet` prints the exact command after installing. To update or remove:

```bash
dotnet tool update -g envparity
dotnet tool uninstall -g envparity
```

## Usage

From a directory containing `.env` and `.env.example`:

```bash
envparity
```

A valid file exits with code `0`:

```text
✓ .env is valid (12 variables checked against .env.example)
```

Problems are grouped and the exit code is `1`:

```text
✗ 2 missing variables:
  API_KEY
  DATABASE_URL

✗ 1 empty variable:
  SECRET_KEY

⚠ 1 extra variable (use --strict to treat it as an error):
  DEBUG

envparity: 3 problems, 1 warning
```

Parser problems include the file and line number, never the line text:

```text
✗ .env:12 invalid line (expected KEY=VALUE)
✗ .env:20 unterminated quoted value

envparity: 2 problems
```

With `--strict`, extra variables and parser warnings count as errors:

```bash
envparity --strict
```

Use different paths, JSON output, or disable colors:

```bash
envparity --env .env.ci --example .env.example
envparity --format json
envparity --no-color
```

Colors are disabled automatically when the output is redirected or when
`NO_COLOR` is set to a non-empty value
([no-color.org](https://no-color.org)).

## Options

| Option | Description |
|---|---|
| `--env <path>` | File to validate (default `.env`) |
| `--example <path>` | Reference file (default `.env.example`) |
| `--strict` | Treat extra variables and parser warnings as errors |
| `--format <text\|json>` | Output format (default `text`) |
| `--no-color` | Disable colors (also honors `NO_COLOR`) |
| `--version` | Print the version and exit `0` |
| `-h`, `--help` | Print help and exit `0` |

## Exit codes

| Code | Meaning | When |
|---|---|---|
| `0` | Valid | No problems, or warnings only in non-strict mode |
| `1` | Validation problems | Missing, empty, parse errors; plus extras and duplicates under `--strict` |
| `2` | Usage or I/O error | Invalid argument, missing or unreadable file, invalid UTF-8 |
| `130` | Interrupted | Ctrl+C |

Results go to `stdout`; usage and I/O errors go to `stderr`. With exit code `2`
nothing is written to `stdout`, so consumers can rely on *empty `stdout` + code
`2`* instead of parsing a partial document. `--help` and `--version` always exit
`0`, and a stack trace is never shown.

## Use in CI

```yaml
- name: Validate environment
  run: |
    dotnet tool install -g envparity
    envparity --env .env.ci --example .env.example --strict
```

A common pattern is to generate `.env.ci` from repository secrets or variables
in an earlier step, then let `envparity` fail the job when a required variable
is missing or empty. Add `--format json` when a later step needs machine-readable
output.

## How files are compared

Let `E` be the keys in the file under validation and `X` the keys in the
reference file:

| Bucket | Definition | Effect |
|---|---|---|
| `missing` | `X \ E` | Error |
| `empty` | `{ k ∈ E ∩ X : value(k).Length == 0 }` | Error |
| `extra` | `E \ X` | Warning; error only with `--strict` |

The three buckets are disjoint, so every variable is reported once, and each
list is sorted in ordinal order. `empty` only applies to keys present in both
files: a key that exists on one side only is `missing` or `extra`, never `empty`.

Values are opaque. The parser keeps them raw for the emptiness check and never
compares or emits them.

### Parser rules (summary)

| Rule | Behavior |
|---|---|
| Comments | `#` starts a comment at the start of a line or value, or after whitespace outside quotes |
| Keys | ASCII `[A-Za-z_][A-Za-z0-9_]*`, case-sensitive |
| `export` | Optional prefix, stripped only when followed by whitespace |
| Whitespace | Trimmed around the key and the unquoted value |
| Quotes | Single or double quotes delimit the value; the value stays raw |
| Escaped quote | In double quotes, `\"` does not close the value (odd backslash parity) |
| Duplicate keys | Last definition wins and emits a `duplicate-key` warning |
| Invalid lines | `invalid-line`, `invalid-key` and `unterminated-quote` are errors |
| Encoding | Strict UTF-8; a leading BOM is ignored, UTF-16 and invalid bytes exit `2` |
| Line endings | LF, CRLF and CR are accepted; empty files are valid |

The full executable specification is [`docs/parser-cases.md`](docs/parser-cases.md)
with 43 cases, one test per case. The rationale for each decision is in
[ADR 0001](docs/adr/0001-design-decisions.md).

## JSON output

`--format json` writes a versioned document to `stdout` (no colors, no Unicode
symbols):

```json
{
  "schemaVersion": 1,
  "toolVersion": "1.0.0",
  "valid": false,
  "strict": false,
  "files": {
    "env": ".env",
    "example": ".env.example"
  },
  "summary": {
    "variables": 10,
    "missing": 2,
    "empty": 1,
    "extra": 1,
    "errors": 0,
    "warnings": 1
  },
  "missing": [
    "API_KEY",
    "DATABASE_URL"
  ],
  "empty": [
    "SECRET_KEY"
  ],
  "extra": [
    "DEBUG"
  ],
  "issues": [
    {
      "severity": "warning",
      "code": "duplicate-key",
      "file": ".env",
      "line": 12,
      "message": "Duplicate key; the last definition wins (first defined on line 11)."
    }
  ]
}
```

Contract rules:

- Property order is fixed as shown; variable arrays are sorted in ordinal order.
- `schemaVersion` changes only for breaking changes; additive changes keep `1`.
- `summary.errors` and `summary.warnings` count `issues`, not variables.
- File names are exactly the strings passed with `--env` and `--example`.
- No value or file line text is ever serialized.

## Security

The tool never prints or serializes variable values, and issue messages are fixed
templates that only include the file name and line number. See
[SECURITY.md](SECURITY.md) for the reporting process.

## Roadmap

Out of scope for 1.0, in rough priority order:

- Value validation (URLs, ports, booleans, regex).
- Cascading files such as `.env.local` and `.env.{environment}`.
- `${VAR}` interpolation and escape interpretation.
- Multiline values.
- `envparity init` to generate `.env.example` from `.env` without values.
- A reusable GitHub Action and SARIF output for Code Scanning.
- `--ignore <pattern>` for exempt variables.

## Contributing

Build, test, formatting and release instructions are in
[CONTRIBUTING.md](CONTRIBUTING.md). The changelog follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## License

[MIT](LICENSE)

---

## Resumen en español

`envparity` es una herramienta global de .NET que valida un archivo `.env`
contra un archivo de referencia (`.env.example` por defecto) y reporta las
variables **faltantes**, **vacías** y **sobrantes**. Nunca imprime valores, así
que es segura para logs de CI.

```bash
dotnet tool install -g envparity
envparity
```

- **Salida:** `text` o `json` versionado, resultados en `stdout` y errores en
  `stderr`; nunca se muestra un stack trace.
- **Códigos:** `0` correcto, `1` problemas de validación, `2` error de uso o E/S
  (con `stdout` vacío) y `130` interrupción.
- **`--strict`** convierte los sobrantes y los warnings del parser en errores.
- **Comparación:** conjuntos disjuntos `missing`/`empty`/`extra`, ordenados
  alfabéticamente; «vacía» solo aplica a claves presentes en ambos archivos.
- **Parser:** UTF-8 estricto, comentarios solo fuera de comillas, comillas
  simples o dobles con valor crudo y `\"` que no cierra, última clave repetida
  gana con warning. Los 43 casos están en
  [`docs/parser-cases.md`](docs/parser-cases.md).
- **Decisiones:** el razonamiento completo está en
  [ADR 0001](docs/adr/0001-design-decisions.md).

Fuera de alcance en 1.0: comparar o validar valores, interpolación, archivos en
cascada y valores multilínea.
