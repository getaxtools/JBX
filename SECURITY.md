# Security policy

## Supported versions

Only the latest release gets security fixes. Please update before reporting.

## Reporting a vulnerability

**Please don't open a public issue.** Report privately through GitHub: go to the repository's **Security** tab, then **Report a vulnerability**. That opens a private advisory only the maintainer can see.

Please include:

- the JBX version and Windows version
- what an attacker can do, and steps or a sample config that shows it (made-up paths and host names only)
- whether you've shared the issue with anyone else

## What to expect

This is a personal open-source project maintained on a best-effort basis:

- acknowledgement within 7 days
- for confirmed issues, a fix target of 7 days for Critical and 30 days for High severity
- credit in the advisory and release notes, unless you prefer to stay anonymous

Please give us a reasonable chance to release a fix before disclosing publicly. We won't take legal action against good-faith research that respects users' privacy and doesn't degrade the service for others.

## Scope

In scope:

- the JBX desktop application and its release artifacts
- config and settings parsing (for example a crafted config that crashes, hangs or runs something)
- anything that makes JBX write to a folder it displays, launch an unexpected program, or reveal paths, host names or user names outside the user's own machine
- anything that lets one unreachable or hostile share freeze the app

Out of scope:

- vulnerabilities in Windows, .NET or other third-party software that JBX doesn't control (report those upstream; tell us if JBX should mitigate)
- access the user already has: JBX only shows folders the user's own account can reach
- issues that require an attacker who already controls the user's account or machine

## How releases are protected

- Releases are code-signed and published with `SHA256SUMS.txt`, a software bill of materials (CycloneDX) and build provenance attestations. Unsigned builds are never published.
- Every pull request runs the test suite (including architecture tests that fail the build if networking or file-system access appears outside its allowed place), static analysis (CodeQL), dependency and license audits and a secret scan.
- JBX makes no network connections and collects no data.
