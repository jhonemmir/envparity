# Security policy

## Supported versions

| Version | Supported |
|---|---|
| 1.x | Yes |
| 0.x (alpha) | No |

## Reporting a vulnerability

Please do not open a public issue for a security problem. Use GitHub's private
vulnerability reporting:

<https://github.com/jhonemmir/envparity/security/advisories/new>

Include, when possible:

- The affected version (`envparity --version`).
- Operating system and .NET runtime version.
- A minimal reproduction, with **synthetic** values. Never send a real `.env`
  file or real credentials.
- The impact you expect and any suggested fix.

You will receive an acknowledgement as soon as the report is reviewed. Fixes
are released as a patch version on NuGet, with a GitHub Security Advisory when
the issue affects users.

## Security guarantees

`envparity` is designed to be safe in shared and public CI logs:

- Variable values are never printed, logged or serialized. Only variable names
  appear in results.
- Issue messages are fixed templates with the file name and line number. They
  never quote the offending line, key or value.
- Decoding and I/O exceptions are mapped to fixed messages; no stack trace or
  exception text reaches the user.
- `fileLabel` is the path exactly as typed by the user, never content read from
  a file.
- The tool reads the two files it is given. It makes no network requests, starts
  no subprocesses, reads no environment variables other than `NO_COLOR`, and
  writes nothing to disk.

## Scope

In scope:

- The tool, its parser and its output contract.
- The NuGet package and its build/release workflow.
- Any case where a value or file content leaks into output, logs or errors.

Out of scope:

- Secrets stored in your own `.env`, `.env.example` or CI configuration.
- Vulnerabilities in third-party services (NuGet, GitHub) outside this
  repository.
- Reports that require running the tool on a machine already controlled by an
  attacker.

## Dependencies

The runtime has a single dependency, `System.CommandLine`, pinned to a verified
version. Test dependencies are not shipped in the package. Dependency updates go
through the same CI matrix as code changes.
