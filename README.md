# Chrome Bookmarks Manager

Chrome Bookmarks Manager is a Windows desktop application for managing very large Chrome bookmark libraries. The project is designed around local-first operation, explicit data-safety boundaries, reproducible builds, and eventual support for safe editing of Chrome's native `Bookmarks` file.

## Project status

**Pre-release — V0.2 Chrome Bookmarks Reader**

V0.2 adds a read-only Chrome Bookmarks reader on top of the V0.1 WPF/bootstrap baseline.

The reader currently supports:

- Chrome native bookmark file version `1`
- `bookmark_bar`, `other`, and `synced` roots
- nested folders and URL nodes while preserving child order and parent links
- exact bookmark and folder counts
- raw Chrome timestamp strings
- `meta_info` in object or legacy serialized-string form
- `checksum` and `checksum_sha256` as read-only metadata
- retention of unsupported/unknown JSON properties in memory
- typed validation failures for malformed or structurally unsafe data
- asynchronous file loading and cancellation
- read-only loading from the WPF shell

V0.2 does **not** edit, save, overwrite, repair, reorder, delete, move, regenerate checksums, or write back to a Chrome profile.

Direct Chrome profile write-back remains disabled until the Safe Chrome Write milestone (V0.9) passes its compatibility, backup, checksum, recovery, and atomic-replace gates.

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

V0.2 remains strictly read-only. Production Chrome profile write-back is intentionally out of scope until V0.9.

## Build

```powershell
dotnet restore ChromeBookmarksManager.slnx
dotnet build ChromeBookmarksManager.slnx --configuration Release --no-restore
```

## Test

Run the full test suite:

```powershell
dotnet test ChromeBookmarksManager.slnx --configuration Release
```

Run the explicit large-reader release measurement with 250,000 generated URLs:

```powershell
pwsh -NoProfile -File scripts/Measure-Reader.ps1 -UrlCount 250000
```

The generated workload uses synthetic reserved-domain data only, is written directly as UTF-8 JSON, and is deleted after the measurement.

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
4. named Chrome Bookmarks reader scale/cancellation gate
5. full xUnit test suite
6. self-contained Windows x64 publish
7. single-file output verification
8. executable startup smoke test
9. artifact upload

The named reader gate uses the ordinary synthetic 10,000-URL workload. The explicit 250,000-URL measurement remains a release command rather than a normal CI requirement.

The downloadable workflow artifact is named:

```text
ChromeBookmarksManager-win-x64
```

## Roadmap

- **V0.1 — Bootstrap: completed** — project shell, tests, privacy guardrails, CI, single EXE
- **V0.2 — Chrome Bookmarks Reader: implementation complete / release validation in progress** — native bookmark parsing, validation, cancellation, metadata preservation, read-only WPF loading
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
- [V0.2 implementation plan](docs/superpowers/plans/2026-09-19-v0.2-chrome-bookmarks-reader.md)

## Development principles

```text
Read broadly.
Edit explicitly.
Write conservatively.
Backup always.
Verify after writing.
```

Data correctness and data safety take priority over feature count and visual polish.
