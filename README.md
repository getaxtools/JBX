# JBX - Jump Box Explorer

JBX is a small Windows desktop app for the jump box you connect to when something breaks. From one config file, it shows where every application's logs live in each environment (Production, UAT, Dev) and for each runtime type (API, web, service, Python, Node, ...), tells you which of those folders you can actually reach from this box, and opens the folder or file in one step.

> **Status: early development.** There is no release yet. The first release (v0.1.0) is planned to cover config-driven browsing, one tab per environment, favourite paths, reachability checks and opening folders and files.

## Why it exists

Finding the right log folder during an incident usually means a wiki page, a spreadsheet of share paths and a lot of trial and error in File Explorer, where one unreachable share can freeze the window for a minute. JBX replaces that with:

- **one config file** describing applications, environments and folder templates, owned by the platform team and shared with everyone
- **a tab per environment**, with Production clearly marked so it is never mistaken for UAT
- **bounded checks**: every folder is checked with a short timeout, so one dead share never blocks the rest
- **zero install**: one signed `JBX.exe`, no installer, no admin rights, no runtime to set up first

## Principles

- **Read-only.** JBX never writes to the folders it shows you.
- **No network code.** JBX makes no network connections of its own and collects no data. It reads the paths you configure, with your own access.
- **Keyboard first and accessible.** Everything works from the keyboard, with screen-reader names and light, dark and high-contrast themes.

## Build from source

Requirements: Windows 10 22H2 or later and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/getaxtools/jbx.git
cd jbx
dotnet build
dotnet test
dotnet run --project src/Jbx.Desktop
```

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and [CODING-STANDARDS.md](CODING-STANDARDS.md) first, and follow the [Code of Conduct](CODE_OF_CONDUCT.md). Report security issues privately as described in [SECURITY.md](SECURITY.md).

## License

Licensed under either of [MIT](LICENSE-MIT) or [Apache-2.0](LICENSE-APACHE) at your option. Contributions are accepted under the same terms, with a [Developer Certificate of Origin](https://developercertificate.org) sign-off on every commit.
