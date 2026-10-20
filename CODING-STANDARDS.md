# Coding standards

JBX is a tool people reach for during incidents, so the order of priorities is: **never hang > read-only safety > correctness > readability > brevity**. Someone who has never seen a file should understand it from its names alone. Each rule lists what enforces it, so you get feedback before review.

## 1. Naming

| Rule | Enforced by |
|---|---|
| Names reveal intent: `ProbeAsync`, `ResolvePathTemplate`, `FavouritesPaneViewModel` | review |
| Methods are verbs, booleans are questions (`IsReachable`, `HasValidationErrors`), collections are plurals | review |
| Async methods end in `Async` | review |
| No abbreviations except `Id`, `Url`, `Unc`, `Io`, `Ui`, `Json`, `Yaml`. Not `cfg`, `mgr`, `tmp`, `res`, `ctx`, `env` | review |
| `Environment` the domain concept is `DeploymentEnvironment` in code, to avoid `System.Environment` | review |
| No dumping-ground types or files: no `Utils`, `Helpers`, `Common`, `Misc`, `Manager` | source hygiene script |
| Units in names: `ProbeTimeout` is a `TimeSpan`; integers carry the unit (`MaxListingEntries`, `MaxConfigSizeBytes`) | review |
| Named constants for numbers other than -1, 0 and 1: `MaxConcurrentProbes = 16` | review |

**Casing** (all enforced by `.editorconfig` in the build)

| Kind | Style |
|---|---|
| Types, methods, properties, constants, static readonly fields | `PascalCase` |
| Locals and parameters | `camelCase` |
| Private instance fields | `_camelCase` |
| Interfaces | `IPascalCase` |
| Resource keys in `Strings.resx` | `Feature_Area_Purpose` |
| Files | named after the one public type they contain |

## 2. Comments

**Default: no comment.** If code needs a comment to be understood, rename something or extract a method.

A comment is allowed only for a *why* the code can't express:

```csharp
// Directory.Exists returns false for access denied, so enumerate and classify the exception instead.
```

A one-line `///` summary is fine on a public interface member when the name alone isn't enough.

Not allowed:

- comments that restate the code (`// check if reachable`)
- banners and regions (`// ===== Helpers =====`, `#region`)
- commented-out code
- `TODO` without an issue number (`// TODO(#42): ...` is fine)
- authorship or change history (git has it)
- em dashes; write `-` or end the sentence

Enforced by: the source hygiene script (TODOs, banners, regions, commented-out code, em dashes) and review. Reviewers ask for comments to be removed as often as for code to be changed.

## 3. Methods and types

| Rule | Enforced by |
|---|---|
| One method does one thing; at most 40 lines, 3 parameters (an options record beyond that), nesting depth 3 | review |
| No boolean flag parameters that switch behaviour; write two methods | review |
| Nullable reference types on; no `!` null-forgiving operator outside tests | compiler (warnings are errors), review |
| Classes `sealed` unless designed for inheritance; immutable data as `sealed record` with read-only collections | review |
| One public type per file; file-scoped namespaces matching the folder | `.editorconfig`, review |
| Constructor injection only; no service locator, static mutable state or singletons outside the composition root | review |
| Braces on every block; no `this.` qualification; explicit accessibility modifiers | `.editorconfig` |
| Write the obvious code: named locals over nested expressions, explicit branches over clever LINQ chains | review |

## 4. Structure and layering

```
src/Jbx.Core/<Feature>/             domain only: config, templating, diagnostics, favourites
src/Jbx.Platform/                   interfaces for the file system, shell and known folders
src/Jbx.Platform.Windows/           their Windows implementations
src/Jbx.Services/<Feature>/         reachability, listing, rows, search, config session, settings
src/Jbx.Desktop/Features/<Feature>/ <Feature>View.axaml, <Feature>ViewModel.cs
```

| Rule | Enforced by |
|---|---|
| Dependencies point one way: `Desktop -> Services -> Core, Platform`; `Platform.Windows -> Platform, Core`; only `Desktop` references `Platform.Windows` | architecture tests |
| Only `Desktop` references Avalonia or other interface packages | architecture tests |
| No file-system access (`File`, `Directory`, `FileInfo`, `FileStream`, ...) outside `Platform.Windows` | architecture tests |
| No native calls, `Microsoft.Win32`, or `Process` outside `Platform.Windows` | architecture tests |
| No networking type (`System.Net.*`) in any product assembly | architecture tests |
| Views hold no logic: code-behind is `InitializeComponent()` only; decisions live in `Services`, state in view models | architecture tests, review |
| No hard-coded path separators, drive letters, `%LOCALAPPDATA%` or case-insensitivity assumptions outside `Platform.Windows`; paths are compared only as canonical path values, never as strings | review |

## 5. Async, threading and I/O

| Rule | Enforced by |
|---|---|
| Every file-system call goes through the platform interfaces, takes a timeout or cancellation token, and returns a typed outcome (reachable, not found, denied, timed out, unknown) | review, tests |
| Never block a thread: no `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` or `Thread.Sleep` | source hygiene script (`Thread.Sleep`, `GetResult()`), review |
| `ConfigureAwait(false)` in `Core`, `Services` and `Platform` projects; not in `Desktop` | analyzer CA2007 |
| Bound properties change only on the UI thread; late results for a row that has moved on are discarded | review, tests |
| Time comes from `TimeProvider`, never `DateTime.Now` | review |

## 6. Errors

| Rule | Enforced by |
|---|---|
| Catch the specific exception you can handle. A `catch` either handles it, shows it to the user with a remedy, or rethrows | analyzer CA1031, review |
| No empty `catch` blocks | source hygiene script |
| Every user-facing error says what happened and what to do, from `Strings.resx` | review |
| Exception messages are never shown as user text and never contain paths that could leave the machine | review |

## 7. Tests

**Every feature ships with tests. A PR without tests isn't mergeable.**

| Layer | Tool | Location |
|---|---|---|
| Unit | xUnit v3 | `tests/Jbx.<Project>.Tests/<Feature>/<Type>Tests.cs` |
| Fakes | hand-written, no mocking libraries | `tests/Jbx.Testing/` |
| Time | `FakeTimeProvider` | |
| UI | Avalonia.Headless.XUnit (`[AvaloniaFact]`) | `tests/Jbx.Desktop.Tests/` |
| Architecture | `System.Reflection.Metadata` and the dependency manifest | `tests/Jbx.Architecture.Tests/` |
| Integration | real temp folders and an unroutable share, `[Trait("Category", "Integration")]` | `tests/Jbx.Platform.Windows.Tests/` |
| Fixtures | synthetic data only | `tests/fixtures/` |

Rules:

1. **Names describe behaviour**: `Reports_denied_rather_than_not_found_when_access_is_refused`.
2. **Arrange, act, assert**, separated by blank lines; one behaviour per test.
3. **Fakes, not mocks.** An in-memory file system that can mark a folder missing, denied or never-answering catches more than an expectation list.
4. **Deterministic**: no network, a fake clock, a fixed culture, a temporary folder per test, no order dependence, no sleeps.
5. **Synthetic data only**: hosts like `fs01.contoso.test`, never a real host name, share or log.
6. **UI tests select by automation id, name or role**, never by position in the visual tree.
7. **Boundary tests are seen failing first**: when adding an architecture or safety test, introduce the violation, watch it fail, then remove it, and say so in the PR.
8. **Readable over DRY.** Repetition in tests is fine when it makes each test stand alone.

Flaky tests are bugs: mark them `[Trait("Flaky", "#issue")]`, open an issue the same day, and fix within a week. Never hide flakiness with retries.

## 8. Security and privacy

| Rule | Enforced by |
|---|---|
| Read-only: no write API is reachable from a configured folder or favourite | review, tests |
| No network calls, telemetry, analytics or update checks | architecture tests |
| Programs are launched only through the shell interface with an argument list, never a command string, `cmd.exe` or `powershell.exe` | architecture tests (`Process` confined), review |
| Config and settings are untrusted input: size caps, validation of every value, no YAML type tags | tests, review |
| No new package without a reason (size, license, maintenance) | review |
| Allowed licenses: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, Zlib, MPL-2.0, Unicode-3.0 | license check script |
| No known vulnerable packages | NuGet audit in the build, `dotnet list package --vulnerable` in CI |
| No secrets, internal paths, host names or real logs in the repo | secret scan in CI, review |

## 9. User interface

| Rule | Enforced by |
|---|---|
| Compiled bindings everywhere; every view and data template sets `x:DataType` | build (`AvaloniaUseCompiledBindingsByDefault`) |
| No hard-coded user-visible text; use `Strings.resx` | review |
| Colours, spacing, type sizes, corner radii and durations only from resource tokens | review |
| Keyboard-operable with visible focus; automation name on every control; status never shown by colour alone | headless tests, review |
| Windows shortcut conventions (`Ctrl+T`, `Ctrl+W`, `Ctrl+Tab`, `F5`, `F6`, ...), every shortcut defined in one command catalog | review |
| Light, dark and Windows contrast themes all work; motion respects the system animation setting | review |

## 10. Formatting

Formatting is automatic; don't argue with the formatter.

- C#: `.editorconfig`, checked by `dotnet format --verify-no-changes` and by the build.
- UTF-8, final newline, line endings normalised by `.gitattributes`.

`dotnet format` fixes most issues; the build reports the rest.
