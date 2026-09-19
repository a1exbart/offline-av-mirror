# Contributing

Patches, bug reports and measurements are welcome. This is a small tool with a
narrow job, so the bar is less about process and more about evidence.

## Before you start

- Open an issue for anything bigger than a fix. The scope is deliberately small:
  download bases, lay them out, verify them. Installing or configuring an
  antivirus is out of scope.
- Nothing belonging to the vendor goes into this repository — no binaries, no
  virus bases, no zone files, no license keys. Tests use synthetic inputs and a
  fake mirror.

## Working on it

```console
$ dotnet build
$ dotnet test
$ dotnet format --verify-no-changes
```

.NET 10 SDK is the only prerequisite. Warnings are errors, and the analyzers are
part of the build.

- Code and comments in English; user-visible text in both English and Russian
  (see `Strings.cs` and `Localization.T`).
- Comments explain **why**, not what. A comment that survives is one that keeps
  the next person from re-deriving a measurement.
- A bug fix comes with a test that fails without it. Behaviour, not spelling:
  test what the program does, not which message it prints.
- Commits follow [Conventional Commits](https://www.conventionalcommits.org):
  `fix:`, `feat:`, `docs:`, `refactor:`, `test:`, `ci:`, `chore:`. The release
  notes are generated from them.

## Reporting a bug

Say what you ran, what you expected, and what happened, with the exact output.
If it involves a mirror, name the product line (Linux or Windows) — the two
behave differently at every step. Never attach a license key or a zone file:
a key is a credential, and both identify you.

Security issues go through [SECURITY.md](SECURITY.md), not the issue tracker.

## License

By contributing you agree that your work is licensed under Apache 2.0, like the
rest of the project (section 5 of the license).
