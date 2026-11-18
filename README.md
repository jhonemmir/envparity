# envparity

[![CI](https://github.com/jhonemmir/envparity/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/jhonemmir/envparity/actions/workflows/ci.yml)

A .NET global tool that validates a `.env` file against a reference file
(`.env.example` by default) and reports variables that are **missing**,
**empty** or **extra**. Values are never printed.

> Status: the CLI, `text`/`json` output, strict mode, exit codes and NuGet tool
> packaging are implemented and verified. The `0.1.0-alpha.1` alpha is published
> on NuGet and installable globally. CI and the final release are pending.

## Install the alpha

```bash
dotnet tool install -g envparity --version 0.1.0-alpha.1
```

## Documentation

- [Project plan](plan-cli-env-validator.md) (Spanish)
- [ADR 0001 — Design decisions](docs/adr/0001-design-decisions.md)
- [Parser test cases](docs/parser-cases.md) — the executable specification

## Build

```bash
dotnet build
dotnet test
```
