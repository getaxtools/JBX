## What and why

<!-- What does this change do for users, and why? Link the issue: "Closes #123". -->

## How it was tested

<!-- Which unit, headless UI or integration tests did you add or change? Anything you checked by hand? For a new architecture or safety test, say how you saw it fail first. -->

## Definition of Done

- [ ] Unit tests cover the new or changed behaviour, including denied, not found and timed out (a bug fix includes a test that fails without the fix)
- [ ] UI changes have a headless test: states render, keyboard path works, controls have automation names
- [ ] Nothing can hang: file-system calls go through the platform interfaces with a timeout; nothing blocks the UI thread
- [ ] No hard-coded user-visible text (strings are in `src/Jbx.Desktop/Resources/Strings.resx`); errors say what to do
- [ ] Keyboard and screen reader work; shortcuts follow Windows conventions
- [ ] Code follows CODING-STANDARDS.md (self-explanatory names, minimal comments)
- [ ] No new network calls, process launches, write paths or packages (or agreed in the issue)
- [ ] Build, tests, format check, hygiene and license check pass locally
- [ ] PR title is a Conventional Commit; commits are signed off (`git commit -s`)

## AI assistance

- [ ] No AI assistance
- [ ] AI-assisted: I reviewed every line, understand it and can explain it in review

<!-- If AI-assisted, which tool and for what (for example "generated test cases, rewritten by hand")? -->
