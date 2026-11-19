# Contributing to envparity

Thanks for taking the time to contribute. This document covers the local
workflow, the test rules and the release process.

## Requirements

- .NET SDK 10. `global.json` pins the feature band with
  `rollForward: latestFeature`.
- Git. On Windows, the repository ships a `.gitattributes` that enforces LF so
  `dotnet format` behaves the same on every platform.

## Build and test

Run these commands from the repository root:

```bash
dotnet restore
dotnet build
dotnet test
```

Coverage uses `coverlet.collector`:

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory artifacts
```

The suite has 151 tests. The parser must stay above 95% branch coverage and the
global coverage above 90%.

Formatting is part of the build contract:

```bash
dotnet format --verify-no-changes
```

Use `dotnet format` (without the flag) to fix issues locally. Builds treat
warnings as errors, so a clean build is expected before opening a pull request.

## Project layout

| Path | Contents |
|---|---|
| `src/EnvParity/` | The global tool: CLI, parser, comparator, formatters |
| `tests/EnvParity.Tests/` | Unit and in-process integration tests |
| `docs/parser-cases.md` | Executable parser specification (43 cases) |
| `docs/adr/` | Architecture decision records |
| `docs/demo.tape` | Reproducible terminal recording for the README |

## Test rules

- The parser table in `docs/parser-cases.md` is the source of truth. A new parser
  behavior needs a row in that document and exactly one test.
- Name tests `Method_Scenario_ExpectedResult`.
- Build parser inputs as in-memory strings. Do not add `.env` fixtures: they are
  affected by line endings and `.gitignore`.
- Never put real values or secrets in tests, fixtures, logs or documentation.
  The security tests use a canary and assert that it never appears in output.
- Integration tests call `CliApplication.Run` in process and assert exit codes,
  `stdout` and `stderr`.

## Commits and pull requests

- Work on a branch and open a pull request against `main`.
- Use conventional commit messages: `feat:`, `fix:`, `test:`, `docs:`,
  `chore:`.
- Keep pull requests focused. CI must be green on Ubuntu, Windows and macOS
  before merging.
- Describe the behavior change and the tests that cover it. If a parser decision
  changes, update the ADR and the case table in the same pull request.

## Regenerating the demo GIF

The README animation is generated with [VHS](https://github.com/charmbracelet/vhs);
do not record it manually.

```bash
dotnet build -c Release
vhs docs/demo.tape
```

VHS needs `ttyd` and `ffmpeg` on `PATH`. The tape creates its `.env` files in a
temporary directory, so the repository never contains real environment files.
Run the command from the repository root; the output is `docs/demo.gif`.

## Release process

1. Update `<Version>` in `src/EnvParity/EnvParity.csproj`, `ToolInfo.Version`
   when the output contract version changes, and `CHANGELOG.md`.
2. Run the full local verification: build, tests, coverage and
   `dotnet format --verify-no-changes`.
3. Commit and push `main`, then wait for the CI matrix to pass.
4. Create an annotated tag that matches the project version and push it:

   ```bash
   git tag -a v1.0.0 -m "envparity 1.0.0"
   git push origin v1.0.0
   ```

5. `.github/workflows/release.yml` verifies that the tag matches `<Version>`,
   runs the tests, packs, publishes to NuGet with OIDC trusted publishing and
   creates the GitHub Release.
6. Verify `dotnet tool install -g envparity --version 1.0.0` in a clean
   environment.

Published NuGet versions are immutable. If a release is wrong, publish a new
patch version instead of trying to replace the package. Each trusted publishing
policy on nuget.org is bound to one workflow file; adding a new workflow that
publishes requires its own policy.
