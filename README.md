# envparity

A .NET global tool that validates a `.env` file against a reference file
(`.env.example` by default) and reports variables that are **missing**,
**empty** or **extra**. Values are never printed.

> Status: parser and strict UTF-8 reader implemented, with the 43 cases from the
> specification. CLI and comparer are in progress.

## Documentation

- [Project plan](plan-cli-env-validator.md) (Spanish)
- [ADR 0001 — Design decisions](docs/adr/0001-design-decisions.md)
- [Parser test cases](docs/parser-cases.md) — the executable specification

## Build

```bash
dotnet build
dotnet test
```
