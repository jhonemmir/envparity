# envparity

A .NET global tool that validates a `.env` file against a reference file
(`.env.example` by default) and reports variables that are **missing**,
**empty** or **extra**. Values are never printed.

> Status: the CLI, `text`/`json` output, strict mode and exit codes are implemented.
> Packaging, CI and release are pending.

## Documentation

- [Project plan](plan-cli-env-validator.md) (Spanish)
- [ADR 0001 — Design decisions](docs/adr/0001-design-decisions.md)
- [Parser test cases](docs/parser-cases.md) — the executable specification

## Build

```bash
dotnet build
dotnet test
```
