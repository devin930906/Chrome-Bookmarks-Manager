# Chrome Bookmarks Manager

Chrome Bookmarks Manager is a Windows desktop application for managing very large Chrome bookmark libraries. The project is designed around local-first operation, explicit data-safety boundaries, reproducible builds, and eventual support for safe editing of Chrome's native `Bookmarks` file.

## Project status

**Pre-release — V0.1 bootstrap**

V0.1 establishes the WPF application shell, automated tests, repository privacy guardrails, reproducible Windows publishing, executable smoke testing, and GitHub Actions artifact delivery.

V0.1 does not read, edit, or overwrite production Chrome Bookmarks files.

Direct Chrome profile write-back remains disabled until the Safe Chrome Write milestone passes its compatibility and recovery gates.

## Target

- Windows 10 / Windows 11 x64
- .NET 10
- WPF
- C#
- Self-contained single-file EXE
- GitHub Actions

## Safety

Real Chrome `Bookmarks` files are private user data and must never be committed to this repository.

Only synthetic fixtures under `samples/` are permitted in Git.

The current sample data uses reserved example domains and does not contain the user's real bookmark titles or URLs.

Production Chrome profile write-back is intentionally out of scope until V0.9.

## Build

```powershell
dotnet restore ChromeBookmarksManager.slnx
dotnet build ChromeBookmarksManager.slnx --configuration Release --no-restore
```

## Test

```powershell
dotnet test ChromeBookmarksManager.slnx --configuration Release
```

## Publish

```powershell
pwsh -NoProfile -File scripts/Publish-Windows.ps1
pwsh -NoProfile -File scripts/Verify-SingleFilePublish.ps1 -PublishDirectory artifacts/win-x64
```

Output:

```text
artifacts/win-x64/ChromeBookmarksManager.exe
```

The application project uses `IncludeNativeLibrariesForSelfExtract=true` so the WPF native runtime libraries are bundled into the distributed single EXE. On launch, .NET may extract required native libraries to its runtime extraction location.

## Smoke test

```powershell
pwsh -NoProfile -File scripts/SmokeTest-Windows.ps1 -ExecutablePath artifacts/win-x64/ChromeBookmarksManager.exe
```

The smoke test verifies that the published executable starts and remains alive through the startup observation window.

## Repository safety check

```powershell
pwsh -NoProfile -File scripts/Verify-RepositorySafety.ps1
```

The safety gate rejects tracked production-style `Bookmarks` files, backup files, and private-data directories.

## GitHub Actions

The workflow at `.github/workflows/build-windows.yml` runs on Windows and performs:

1. repository privacy verification
2. restore
3. Release build
4. xUnit tests
5. self-contained Windows x64 publish
6. single-file output verification
7. executable startup smoke test
8. artifact upload

The downloadable workflow artifact is named:

```text
ChromeBookmarksManager-win-x64
```

## Roadmap

- **V0.1 — Bootstrap:** project shell, tests, privacy guardrails, CI, single EXE
- **V0.2 — Chrome Bookmarks Reader:** native bookmark parsing and validation
- **V0.3 — Browser UI:** folder tree, bookmark list, virtualization
- **V0.4 — Search / Index:** in-memory indexing and fast search
- **V0.5 — Editing:** add, rename, edit URL, dirty-state tracking
- **V0.6 — Move / Drag & Drop:** movement, reordering, hierarchy protection
- **V0.7 — Delete / Batch:** multi-select and batch operations
- **V0.8 — Undo / Redo:** reversible command history
- **V0.9 — Safe Chrome Write:** checksum, backup, atomic replace, Chrome compatibility verification
- **V1.0 — Stable personal-use release**

## Design and implementation documents

- [Approved design spec](docs/superpowers/specs/2026-09-19-chrome-bookmarks-manager-design.md)
- [V0.1 implementation plan](docs/superpowers/plans/2026-09-19-v0.1-bootstrap.md)

## Development principles

```text
Read broadly.
Edit explicitly.
Write conservatively.
Backup always.
Verify after writing.
```

Data correctness and data safety take priority over feature count and visual polish.
