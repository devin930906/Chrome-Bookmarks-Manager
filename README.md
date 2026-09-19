# Chrome Bookmarks Manager

Chrome Bookmarks Manager is a Windows desktop application for managing very large Chrome bookmark libraries. The project is designed around local-first operation, explicit data-safety boundaries, reproducible builds, and eventual support for safe editing of Chrome's native `Bookmarks` file.

## Project status

**Pre-release — V0.4 Search / Index (completed)**

V0.4 adds fast, read-only in-memory search and result navigation on top of the completed V0.3 browser.

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
- direct bookmark URLs for the currently selected folder
- explicit WPF virtualization and recycling for tree and bookmark results
- one in-memory search index built from the already loaded document
- Name and URL substring search, including domain text
- ordinal case-insensitive matching
- Chinese / Unicode substring matching
- `All bookmarks` and direct-child `Current folder` scopes
- 250 ms production debounce
- cancellable latest-query-wins behavior so stale results cannot overwrite newer searches
- result navigation back to the existing parent folder and original bookmark instance
- synthetic SearchScale verification at 10,000 URLs in normal CI
- explicit SearchScale release measurement at 250,000 synthetic URLs

V0.4 is still strictly read-only. It does **not** provide add, rename, edit URL, delete, move, reorder, drag/drop, Undo/Redo, save, overwrite, repair, or production Chrome write-back.

Editing begins in V0.5. Direct Chrome profile write-back remains disabled until the V0.9 Safe Chrome Write milestone passes its compatibility, backup, checksum, recovery, and atomic-replace gates.

## Target

- Windows 10 / Windows 11 x64
- .NET 10
- WPF
- C#
- Self-contained single-file EXE
- GitHub Actions

## Safety

Real Chrome `Bookmarks` files are private user data and must never be committed to this repository.

Only synthetic fixtures under `samples/` are permitted in Git. Generated scale data use reserved example domains and do not contain the owner's real bookmark titles, URLs, queries, or raw file contents.

V0.4 remains strictly read-only. Search runs against the already loaded in-memory document/index and does not reread or modify the source Chrome `Bookmarks` file. Production Chrome profile write-back remains intentionally out of scope until V0.9.

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

Run the explicit V0.4 search release measurement with 250,000 generated URLs:

```powershell
pwsh -NoProfile -File scripts/Measure-Search.ps1 -UrlCount 250000
```

Verify the production WPF browser and search controls still enforce their UI contracts:

```powershell
pwsh -NoProfile -File scripts/Verify-BrowserVirtualization.ps1
pwsh -NoProfile -File scripts/Verify-SearchUi.ps1
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
6. search UI contract verification
7. named BrowserScale gate with the normal synthetic workload
8. named SearchScale gate with the normal 10,000-URL synthetic workload
9. full xUnit test suite
10. self-contained Windows x64 publish
11. single-file output verification
12. executable startup smoke test
13. artifact upload

The normal CI BrowserScale and SearchScale gates use synthetic 10,000-URL workloads. The 250,000-URL browser-state and search measurements remain explicit release commands rather than permanent heavy normal CI steps.

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

V0.3 automated verification and Windows 10 owner acceptance are complete. The browser UI has been validated against the owner's real private Chrome Bookmarks file without modifying the source file.

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

## V0.3 owner acceptance

Windows 10 owner acceptance completed successfully on 2026-09-19.

- real Chrome Bookmarks load: 209,382 URLs and 3,404 folders
- observed load time on the owner's Windows 10 machine: approximately 2.5 seconds
- three-root folder tree rendered correctly
- nested folder browsing and selected-folder bookmark lists worked correctly
- bookmark Name / URL rows displayed correctly locally
- large-list scrolling remained usable
- source-file SHA-256 unchanged before/after
- source-file length unchanged before/after
- source-file last-write timestamp unchanged before/after
- no private bookmark titles, URLs, or raw file contents were committed or uploaded

## V0.4 verification status

V0.4 automated engineering verification, Windows 10 owner acceptance, PR #4 merge, and post-merge `main` verification are complete.

Automated evidence currently includes:

- search-index identity/order/count tests
- Name / URL / Unicode / scope search semantics tests
- debounce, cancellation, latest-query-wins, and reset-state tests
- result-navigation tests preserving original bookmark identity
- browser virtualization and dedicated search-UI mechanical guards
- SearchScale 10,000 synthetic URL normal CI gate
- explicit SearchScale 250,000 synthetic URL release measurement
- complete regression suite
- self-contained single-EXE publish and startup smoke test

The explicit 250,000-URL SearchScale measurement on the GitHub Windows runner completed with 250,000 exact results while retaining original bookmark references and stable order. Observed timing and memory figures are diagnostic only; they are not a performance guarantee for the owner's Windows 10 system.

## V0.4 merge and post-merge verification

PR #4 was merged to `main` on 2026-09-19 as merge commit `592c6c7b1a06c6499d50a5dcf19e5e0ee10c2402`.

Post-merge `main` Actions run #94 completed successfully with repository safety, Release build, ReaderScale, browser virtualization, search UI contract, BrowserScale, SearchScale, the full xUnit suite, Windows x64 publish, strict single-file verification, executable startup smoke test, and artifact upload.

## V0.4 owner acceptance

Windows 10 owner acceptance completed successfully on 2026-09-19 using the owner's private Chrome Bookmarks file.

- application launched normally on Windows 10
- real Chrome Bookmarks loaded successfully: 209,382 URLs and 3,404 folders
- observed load time: approximately 1.7 seconds
- folder tree and direct-bookmark list rendered correctly
- Name search passed
- URL / domain-text search passed
- Chinese / Unicode substring search passed
- case-insensitive matching passed
- `All bookmarks` and direct-child `Current folder` scopes passed
- rapid successive queries preserved latest-query-wins behavior
- clearing search restored normal folder browsing
- search-result activation navigated back to the existing parent folder/bookmark
- broad-result scrolling remained usable
- source-file SHA-256, length, and last-write timestamp remained unchanged before/after acceptance
- no private bookmark titles, URLs, raw file contents, private queries, or screenshots were committed or uploaded to the repository

## Roadmap

- **V0.1 — Bootstrap: completed** — project shell, tests, privacy guardrails, CI, single EXE
- **V0.2 — Chrome Bookmarks Reader: completed** — native bookmark parsing, validation, cancellation, metadata preservation, read-only WPF loading; Windows 10 owner acceptance passed with the private source file unchanged
- **V0.3 — Browser UI: completed** — folder tree, selected-folder bookmark list, status summaries, virtualization/recycling; Windows 10 owner acceptance passed with the private source file unchanged
- **V0.4 — Search / Index: completed** — in-memory indexing, read-only search, result navigation; automated release gates, Windows 10 owner acceptance, merge, and post-merge verification complete
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
- [V0.4 implementation plan](docs/superpowers/plans/2026-09-19-v0.4-search-index.md)

## Development principles

```text
Read broadly.
Edit explicitly.
Write conservatively.
Backup always.
Verify after writing.
```

Data correctness and data safety take priority over feature count and visual polish.
