# Chrome Bookmarks Manager

Chrome Bookmarks Manager is a Windows desktop application for managing very large Chrome bookmark libraries. The project is designed around local-first operation, explicit data-safety boundaries, reproducible builds, and eventual support for safe editing of Chrome's native `Bookmarks` file.

## Project status

**Pre-release — V0.3 Browser UI**

V0.3 adds a real read-only bookmark browser on top of the completed V0.2 reader.

The current application supports:

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
- a three-root folder tree backed by the parsed Chrome document
- folder-only tree presentation wrappers
- direct bookmark URLs for the currently selected folder
- read-only bookmark selection
- status summaries for document counts, selected-folder counts, load state, and source path
- explicit WPF UI virtualization and recycling for the folder tree and bookmark list
- synthetic large-browser-state verification up to 250,000 URLs

V0.3 still does **not** provide search, add, rename, edit, delete, move, reorder, drag/drop, Undo/Redo, checksum generation, save, overwrite, repair, or production Chrome write-back.

Search/index remains scheduled for V0.4. Editing begins in V0.5. Direct Chrome profile write-back remains disabled until the V0.9 Safe Chrome Write milestone passes its compatibility, backup, checksum, recovery, and atomic-replace gates.

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

The current sample and generated scale data use reserved example domains and do not contain the user's real bookmark titles or URLs.

V0.3 remains strictly read-only. Production Chrome profile write-back is intentionally out of scope until V0.9.

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

Run the explicit V0.3 browser-state release measurement with 250,000 generated URL nodes:

```powershell
pwsh -NoProfile -File scripts/Measure-BrowserState.ps1 -UrlCount 250000
```

Verify the production WPF browser controls still enforce virtualization/recycling:

```powershell
pwsh -NoProfile -File scripts/Verify-BrowserVirtualization.ps1
```

The generated workloads use synthetic reserved-domain data only.

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
4. named Chrome Bookmarks ReaderScale gate
5. browser virtualization/recycling contract verification
6. named BrowserScale gate with the normal synthetic workload
7. full xUnit test suite
8. self-contained Windows x64 publish
9. single-file output verification
10. executable startup smoke test
11. artifact upload

The normal CI browser gate uses a synthetic 10,000-URL workload. The explicit 250,000-URL browser-state measurement remains a release command rather than a permanent heavy CI step.

The downloadable workflow artifact is named:

```text
ChromeBookmarksManager-win-x64
```

## V0.2 owner acceptance

Windows 10 owner acceptance completed successfully on 2026-09-19.

- real Chrome Bookmarks load: 209,382 URLs and 3,404 folders
- observed load time on the owner's Windows 10 machine: 1.8 seconds
- source-file SHA-256 unchanged before/after
- source-file length unchanged before/after
- source-file last-write timestamp unchanged before/after
- no private bookmark titles, URLs, raw file contents, or private logs were committed or uploaded

## V0.3 verification status

Automated V0.3 implementation verification is complete. Windows 10 owner acceptance for the new browser UI remains the final release gate before V0.3 may be marked complete and merged.

Automated evidence includes:

- folder-tree presentation tests
- read-only browser-state tests
- WPF binding/build verification
- explicit virtualization/recycling verification
- BrowserScale 10,000 synthetic URL gate
- explicit BrowserScale 250,000 synthetic URL measurement
- complete regression suite
- self-contained single-EXE publish and startup smoke test

The 250,000-URL browser-state measurement on the GitHub Windows runner completed successfully with exact domain-reference verification. Runner timings are observational only and are not treated as a Windows 10 owner-machine performance guarantee.

## Roadmap

- **V0.1 — Bootstrap: completed** — project shell, tests, privacy guardrails, CI, single EXE
- **V0.2 — Chrome Bookmarks Reader: completed** — native bookmark parsing, validation, cancellation, metadata preservation, read-only WPF loading; Windows 10 owner acceptance passed with the private source file unchanged
- **V0.3 — Browser UI: implementation complete / owner acceptance pending** — folder tree, selected-folder bookmark list, status summaries, virtualization/recycling
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
- [V0.3 implementation plan](docs/superpowers/plans/2026-09-19-v0.3-browser-ui.md)

## Development principles

```text
Read broadly.
Edit explicitly.
Write conservatively.
Backup always.
Verify after writing.
```

Data correctness and data safety take priority over feature count and visual polish.
