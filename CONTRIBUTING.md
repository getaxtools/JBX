# Contributing to JBX

Thanks for helping. This guide covers setup, workflow, the Definition of Done and how reviews work. The code rules are in [CODING-STANDARDS.md](CODING-STANDARDS.md).

## Ground rules

- Everyone follows the [Code of Conduct](CODE_OF_CONDUCT.md).
- **Read-only and private by design.** JBX never writes to the folders it shows and makes no network connections. PRs that add telemetry, update checks, network calls or write paths won't be accepted unless agreed in an issue first.
- **Open an issue first** for features and larger changes, so we can agree on scope before you write code. Small fixes can go straight to a PR.
- The current focus is the first Windows release. Work outside that scope may wait.

## Setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) (the exact feature band is pinned in `global.json`) on Windows 10 22H2 or later.
2. Clone and build:

   ```powershell
   git clone https://github.com/getaxtools/jbx.git
   cd jbx
   dotnet build
   ```

3. Run the app with `dotnet run --project src/Jbx.Desktop`.

## Everyday commands

| Task | Command |
|---|---|
| Build (warnings are errors) | `dotnet build` |
| Run the app | `dotnet run --project src/Jbx.Desktop` |
| All tests | `dotnet test` |
| Tests for one feature | `dotnet test --filter "FullyQualifiedName~Favourites"` |
| Format check / fix | `dotnet format --verify-no-changes` / `dotnet format` |
| Source hygiene | `pwsh scripts/Test-SourceHygiene.ps1` |
| License check | `pwsh scripts/Test-PackageLicences.ps1` |
| Release build (single JBX.exe) | `dotnet publish src/Jbx.Desktop -p:PublishProfile=win-x64 -o artifacts/publish` |
| Vulnerable packages | `dotnet list package --vulnerable --include-transitive` |

`pwsh` is PowerShell 7; both scripts also run in Windows PowerShell 5.1 (`powershell -File scripts\Test-SourceHygiene.ps1`).

## Project tour

```
src/Jbx.Core/              domain types, config schema, validation, templating
src/Jbx.Platform/          interfaces for anything that touches the OS
src/Jbx.Platform.Windows/  the Windows implementation; the only place with file access or Windows APIs
src/Jbx.Services/          reachability, listing, search, config session, favourites: every decision lives here
src/Jbx.Desktop/           Avalonia views and view models (JBX.exe)
tests/                     one test project per product project, shared fakes, architecture tests, fixtures
```

Dependencies point one way: `Desktop -> Services -> Core, Platform`; `Platform.Windows -> Platform, Core`; only `Desktop` references `Platform.Windows`. Architecture tests fail the build otherwise.

## Workflow

1. Fork, then create a branch: `feat/favourite-rename`, `fix/mapped-drive-canonical-path`.
2. Make a small, focused change. Aim for under ~400 changed lines, excluding tests and fixtures.
3. Write tests alongside the code (see the Definition of Done).
4. Run the build, tests, format check and hygiene script locally.
5. Commit with a [Conventional Commit](https://www.conventionalcommits.org) message and a DCO sign-off (`git commit -s`).
6. Open a PR and fill in the template.

### Commit messages

```
fix(reachability): report denied rather than not found when access is refused

Signed-off-by: Your Name <you@example.com>
```

Types: `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `build`, `ci`, `chore`. The PR title must follow the same format, because it becomes the changelog entry when we squash-merge.

### Developer Certificate of Origin

Every commit must be signed off (`git commit -s`). That certifies you wrote the change or have the right to submit it under the project's licenses, as described at [developercertificate.org](https://developercertificate.org). There is no separate CLA.

## Definition of Done

A PR is ready for review when all of these are true:

- [ ] **Unit tests** cover the new or changed behaviour, including failure cases (access denied, not found, timed out). A bug fix includes a test that fails without the fix.
- [ ] **UI changes** have a headless test: the view renders its loading, empty and error states, the keyboard path works, and controls have automation names.
- [ ] **Nothing can hang**: every file-system call goes through the platform interfaces with a timeout, and nothing blocks the UI thread.
- [ ] **No hard-coded user-visible text.** Strings go in `src/Jbx.Desktop/Resources/Strings.resx`, and every error message says what to do.
- [ ] **Keyboard and screen reader**: everything is reachable by keyboard, every control has an automation name, and shortcuts follow Windows conventions.
- [ ] **Readable code** that follows [CODING-STANDARDS.md](CODING-STANDARDS.md): self-explanatory names, minimal comments.
- [ ] **No new network calls, process launches, write paths or packages** unless agreed in the issue.
- [ ] Build, tests, format check, hygiene and license check pass.
- [ ] Conventional Commit title and signed-off commits.

## Review process

1. **Triage**: the maintainer checks the linked issue and scope.
2. **CI**: every required check must pass. For first-time contributors, a maintainer approves the CI run first.
3. **Review**: correctness, tests, standards and, for changes to file access, process launch, config parsing, packages or CI, a security review. Expect questions; they're about the code, not about you.
4. **Merge**: squash merge with your PR title as the commit message.

Reviews aim to respond within a week. This is a personal project, so please be patient.

## AI-assisted contributions

AI coding tools are welcome, with these rules:

- **Disclose** meaningful AI assistance in the PR template.
- **You're accountable** for every line. You must understand the change and be able to explain it in review.
- **Same bar**: AI-written code meets the same tests, standards and review as any other code. Large, unreviewed generated changes will be closed.
- **No narration comments.** Delete the explanatory comments assistants tend to add; see the comment policy in CODING-STANDARDS.md.
- **Don't commit your assistant's configuration** or planning files. Common ones are already git-ignored.
- **Never paste secrets**, internal paths, host names or other people's data into prompts or test fixtures.

## Adding UI text

1. Add an entry to `src/Jbx.Desktop/Resources/Strings.resx`, named `Feature_Area_Purpose` (for example `Favourites_Pane_EmptyState`).
2. Use it through the generated `Strings` class in C#, or `{x:Static res:Strings.Favourites_Pane_EmptyState}` in XAML.
3. For values in a sentence, put placeholders in the resource (`"{0} files"`) and use the generated `Format...` method. Never build sentences by joining strings.

## README screenshots

`pwsh scripts/New-ReadmeMedia.ps1` creates the demo folders, drives JBX through a short tour with UI Automation and writes the screenshots and `tour.gif` to `.github/media`. Close JBX first: the script swaps in a temporary settings file and puts yours back afterwards. It needs Python 3 with Pillow (`python -m pip install pillow`). Regenerate the media when a change alters what the tour shows.

## Releasing (maintainers)

1. Merging to `main` keeps a release pull request open, with the next version and changelog worked out from the Conventional Commit titles.
2. Merging that pull request tags the version and runs the release workflow: build and test, publish `JBX.exe`, the software bill of materials, signing (once the signing service is set up, with owner approval in the `release` environment), build provenance and checksums. The result is a **draft** release.
3. Run the smoke checklist on the draft's `JBX.exe`, then publish the draft. Never publish an unsigned build.

Smoke checklist:

- [ ] On a clean Windows VM with no .NET installed, a double-click starts JBX with no prompt and no elevation.
- [ ] It runs from a read-only folder and from a network share.
- [ ] Nothing is written next to the exe or to the registry; settings are under `%LOCALAPPDATA%\getaxtools\JBX\`.
- [ ] A config with an unreachable share keeps the window responsive, and the row shows *Timed out*.
- [ ] Two instances run side by side against different configs.
- [ ] No network connection is made during a full manual run.
- [ ] About shows the version and the third-party licences; the signature verifies and Windows shows the publisher.

To rebuild an existing tag, run the **Release** workflow by hand with the tag name.

## Reporting bugs and asking for features

Use the issue forms. For bugs, include your Windows version, JBX version, steps to reproduce and, if possible, a small config that shows the problem, **with made-up paths and host names** (for example `\\fs01.contoso.test\logs`). For security problems, **don't open an issue**: follow [SECURITY.md](SECURITY.md).
