# GitHub repository setup

## Create the repository

Use these public details:

- **Name:** `Mediance`
- **Description:** `A customizable floating media controller for Windows 11 with synchronized lyrics and per-app audio routing.`
- **Visibility:** Public
- **Initialize with README:** No; the local project already contains one
- **License selected by GitHub:** None; the repository already contains its current license

Suggested topics:

```text
windows windows-11 winui3 csharp dotnet media-controller spotify lyrics acrylic audio-routing desktop-app
```

After GitHub creates the empty repository, follow the push commands shown on its page. The local branch is already named `main`.

## Repository settings

Under **Settings → General**:

- keep Issues enabled;
- enable Discussions only if there is time to moderate and answer general questions;
- disable the wiki while the maintained documentation lives in `docs/`;
- use squash merging for small pull requests;
- automatically delete merged branches.

Under **Settings → Actions → General**, allow GitHub Actions from trusted marketplace publishers. The included workflow needs checkout and .NET setup actions and requests read-only repository contents.

Under **Settings → Code security and analysis**:

- enable Dependabot alerts;
- enable dependency graph;
- enable secret scanning when GitHub offers it for the repository;
- enable private vulnerability reporting so `SECURITY.md` has a private destination.

## Protect main

After the first successful workflow run, create a branch ruleset for `main`:

- require a pull request before merging when accepting outside contributions;
- require the `build-and-test` status check;
- block force pushes;
- block branch deletion;
- allow repository administrators to fix an urgent release issue when necessary.

For a single-maintainer beta, requiring one review can wait until another regular contributor joins.

## First repository check

Before the first push, confirm that these remain outside Git:

- `.tools/`, `artifacts/`, `bin/`, `obj/`, and `work/`;
- local agent and internal planning files;
- settings, logs, manual lyrics timing files, backups, and invalid recovery files;
- certificates, signing keys, and release ZIP files.

After the push, open the Actions tab and wait for **Windows build** to finish. Fix a failed check before publishing a release.

## First beta release

1. Finish the checks in [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).
2. Create a tag named `v0.9.0-beta.1`.
3. Use [RELEASE_NOTES_TEMPLATE.md](RELEASE_NOTES_TEMPLATE.md) as the release description.
4. Upload the complete self-contained `Mediance-0.9.0-beta.1-win-x64.zip` package.
5. Upload a text file containing its SHA-256 value.
6. Mark it as a pre-release.
7. Download the published asset once and verify it on a clean Windows account or computer.

Do not commit release binaries to the source tree. GitHub Releases is the distribution area for beta ZIP files.
