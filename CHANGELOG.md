# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Documentation: final README with badges, a reproducible terminal demo,
  installation, CI usage and the JSON contract.

## [1.0.0] - 2026-11-18

### Added

- `.env` parser with an explicit grammar: comments, optional `export`, single
  and double quotes, opaque values, escaped double quotes, duplicate keys,
  strict UTF-8 and LF/CRLF/CR line endings. The 43 cases in
  [`docs/parser-cases.md`](docs/parser-cases.md) are the executable
  specification, one test per case.
- Comparison of variable names between `.env` and a reference file with
  disjoint `missing`, `empty` and `extra` buckets, ordinal ordering and no value
  comparison.
- `text` output with groups and a summary, and versioned `json` output
  (`schemaVersion` 1) designed for CI consumption.
- `--strict` to treat extra variables and parser warnings as errors.
- Stable CLI contract: results on `stdout`, usage and I/O errors on `stderr`,
  exit codes `0`/`1`/`2`/`130`, empty `stdout` on `2`, `--help`/`--version`
  exit `0`.
- Global tool packaging (`envparity`) for `net10.0`, published on NuGet.
- Cross-platform CI on Ubuntu, Windows and macOS with build, 151 tests,
  coverage collection and `dotnet format --verify-no-changes`.
- Tag-driven release workflow with tag/version verification, OIDC trusted
  publishing and a GitHub Release.

### Security

- Values are never printed or serialized. Issue messages are fixed templates
  containing only the file name and line number.

## [0.1.0-alpha.1] - 2026-11-18

### Added

- First public alpha: parser, comparator, `text`/`json` formatters, strict mode,
  exit codes and global tool packaging.
- NuGet trusted publishing (OIDC) without long-lived API keys.

[Unreleased]: https://github.com/jhonemmir/envparity/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/jhonemmir/envparity/releases/tag/v1.0.0
[0.1.0-alpha.1]: https://www.nuget.org/packages/envparity/0.1.0-alpha.1
