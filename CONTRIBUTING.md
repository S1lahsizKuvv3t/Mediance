# Contributing to Mediance

Thanks for taking the time to help. The best contributions are small enough to review, solve one clear problem, and preserve the widget's quiet desktop behavior.

## Before writing code

- Search existing issues and pull requests.
- Open an issue before starting a large feature or a change to native Windows integration.
- Keep privacy in mind: listening history, lyrics text, artwork, and query metadata must not be added to logs or test fixtures.
- Do not change the system-wide default audio output. Mediance audio routing is deliberately scoped to the selected application.

## Local setup

Requirements:

- Windows 11 24H2 or newer
- .NET 10 SDK
- x64 environment

```powershell
git clone <your-fork-url>
cd Mediance
dotnet restore Mediance.slnx
dotnet build Mediance.slnx --configuration Release
dotnet test tests/Mediance.Core.Tests --configuration Release
```

The native window smoke test can be run with:

```powershell
.\scripts\dev.ps1 glass-test
```

Close a normally running Mediance window before starting the smoke test.

## Project boundaries

- Portable rules belong in `Mediance.Core`.
- Windows API and COM integration belong in `Mediance.Windows`.
- Network lyrics providers belong in `Mediance.Lyrics`.
- UI behavior belongs in the WinUI desktop project.
- New provider results must be identity-validated. Plain lyrics must never be labelled or animated as synchronized.
- Source-authored timestamps keep priority over local manual timings.
- Media updates must not activate the widget or take keyboard focus.

Undocumented Windows interfaces need extra care. Link to the implementation or documentation used to verify ABI signatures and keep the adapter isolated.

## Tests

Add a test when changing selection, matching, persistence, timing, parsing, or recovery behavior. Provider tests should use short synthetic content and local HTTP handlers. Do not commit real lyrics.

Native behavior that cannot be proven by a unit test should include a short manual test note in the pull request.

## Pull requests

Keep commits readable and avoid unrelated formatting changes. A pull request should explain:

- the user-visible problem;
- what changed;
- how it was tested;
- any remaining platform limitation.

All build and test checks must pass before merge. By submitting a contribution, you agree that it may be distributed under the repository's current license and any future license adopted for the official Mediance project.
