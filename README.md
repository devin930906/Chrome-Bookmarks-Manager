# Chrome Bookmarks Manager

Chrome Bookmarks Manager is a Windows desktop application for managing very large Chrome bookmark libraries. The project is designed around local-first operation, explicit data-safety boundaries, reproducible builds, and eventual support for safe editing of Chrome's native `Bookmarks` file.

## Project status

**Pre-release — V0.6 Move / Reorder / Drag & Drop (implementation and automated release gates complete; Windows 10 owner acceptance pending)**

V0.6 adds browser-style in-memory bookmark and folder movement, reordering, Drag & Drop, and Move to... on top of the completed V0.5 editing milestone.

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
- in-memory add bookmark and folder, rename ordinary folders/bookmarks, and edit bookmark URLs
- dirty-state tracking after successful edits
- Discard / Cancel confirmation before dirty-document reload or application close
- Cancel preserves the current in-memory document; Discard never writes the source file
- bookmark Move to... across any valid folder, appending at the destination end
- folder Move to... with self/descendant-cycle prevention
- bookmark Drag & Drop for same-folder Before/After reorder and cross-folder Into movement
- folder Drag & Drop using top/middle/bottom thirds for Before/Into/After placement
- permanent Chrome roots remain valid Into destinations but can never be moved as sources
- mixed folder/bookmark child order is preserved when reordering only one node kind
- pure moves preserve node object identity, IDs, GUIDs, metadata, timestamps, URLs, descendants, and document counts
- active search remains coherent after cross-folder moves without rebuilding the search index
- named V0.6 MoveScale verification at 10,000 synthetic URLs in normal CI
- V0.6 production-source safety gate rejects file-write/write-back primitives

V0.6 editing and movement are limited to the in-memory document. Delete/batch operations, Undo/Redo, Save, overwrite, repair, and production Chrome write-back are not available. Dirty-document reload and app close require explicit Discard / Cancel confirmation; there is no Save path.

Direct Chrome profile write-back remains disabled until the V0.9 Safe Chrome Write milestone passes its compatibility, backup, checksum, recovery, and atomic-replace gates.

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

V0.6 editing and movement change only the in-memory bookmark graph. Opening a file reads the source; search, editing, Move to..., and Drag & Drop do not write to it. A dirty reload or app close requires an explicit Discard choice, and no Save/write path exists. Production Chrome profile write-back remains intentionally out of scope until V0.9.

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

Run the V0.6 movement scale gate with a larger synthetic workload when preparing a release:

```powershell
$env:CBM_MOVE_URL_COUNT = "250000"
dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=MoveScale"
Remove-Item Env:CBM_MOVE_URL_COUNT
```

Verify the production WPF browser and search controls still enforce their UI contracts:

```powershell
pwsh -NoProfile -File scripts/Verify-BrowserVirtualization.ps1
pwsh -NoProfile -File scripts/Verify-SearchUi.ps1
pwsh -NoProfile -File scripts/Verify-V05EditingUi.ps1
pwsh -NoProfile -File scripts/Verify-V06MoveUi.ps1
pwsh -NoProfile -File scripts/Verify-V06BookmarkDragDrop.ps1
pwsh -NoProfile -File scripts/Verify-V06FolderDragDrop.ps1
pwsh -NoProfile -File scripts/Verify-V06MoveSafety.ps1
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
2. restore and Release build
3. named Chrome Bookmarks ReaderScale gate
4. browser virtualization/recycling contract verification
5. search UI contract verification
6. V0.5 editing UI contract verification
7. V0.6 Move to... UI contract verification
8. V0.6 bookmark Drag & Drop contract verification
9. V0.6 folder Drag & Drop contract verification
10. V0.6 production-source move/write-safety verification
11. named BrowserScale gate
12. named SearchScale gate
13. named V0.6 MoveScale gate with the normal 10,000-URL synthetic workload
14. full xUnit test suite
15. self-contained Windows x64 publish
16. single-file output verification
17. executable startup smoke test
18. artifact upload

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

## V0.5 owner acceptance

Status: **Passed on Windows 10 on 2026-09-20** using the owner's private Chrome Bookmarks file. The owner confirmed the documented V0.5 edit, search, reload, and close flows passed; SHA-256, length, and LastWriteTimeUtc comparisons all returned `True`.

- The Windows x64 artifact from [Actions #119](https://github.com/devin930906/Chrome-Bookmarks-Manager/actions/runs/35488919731) opened and loaded the file; 209,382 URLs and 3,404 folders were visible.
- Temporary in-memory bookmark/folder creation, rename, URL edit, and search passed.
- Dirty reload: Cancel preserved edits; Discard reloaded the file and removed them.
- Dirty close: Cancel kept the app open; Discard closed it.
- The source file's SHA-256, byte length, and LastWriteTimeUtc remained unchanged.
- No private bookmark content was committed or uploaded.

### Reproduction procedure

Close Chrome completely before capturing the source-file baseline, so Chrome itself cannot change the file during the check. Set `$bookmarksPath` to the exact `Bookmarks` file loaded in the app and record its integrity values in the same PowerShell session:

```powershell
$bookmarksPath = "C:\Path\To\Chrome\User Data\Default\Bookmarks"
$before = Get-Item -LiteralPath $bookmarksPath
$beforeHash = (Get-FileHash -LiteralPath $bookmarksPath -Algorithm SHA256).Hash
$beforeLength = $before.Length
$beforeWriteUtc = $before.LastWriteTimeUtc
```

Open the EXE and load that file. Add a bookmark and folder named `V0.5 acceptance bookmark` and `V0.5 acceptance folder`, rename both, edit the test bookmark URL to `https://example.com/v05-acceptance-updated`, and confirm search finds the updated title or URL. Check that the dirty state is visible.

While the document is dirty, open the same file again and choose **Cancel** in the discard prompt; verify the in-memory edits remain. Repeat and choose **Discard**; verify the file reloads and the unsaved edits disappear. Make one new test edit, click the window close button, choose **Cancel** and verify the app stays open; close again and choose **Discard**.

After the app closes, compare the source file:

```powershell
$after = Get-Item -LiteralPath $bookmarksPath
$afterHash = (Get-FileHash -LiteralPath $bookmarksPath -Algorithm SHA256).Hash
[pscustomobject]@{
    SHA256Unchanged = $beforeHash -ceq $afterHash
    LengthUnchanged = $beforeLength -eq $after.Length
    LastWriteTimeUtcUnchanged = $beforeWriteUtc -eq $after.LastWriteTimeUtc
}
```

All three values must be `True`. Report only pass/fail and the three comparison results. Do not commit or upload the private file, its bookmark titles or URLs, private search queries, or screenshots.

## V0.6 automated verification status

V0.6 implementation is complete through the automated release-candidate gates. Windows 10 owner acceptance remains required before PR #6 may be merged.

Automated evidence includes:

- controlled domain mutation and atomic move-service tests
- same-parent mixed-child bookmark/folder reorder semantics
- root, foreign-document, self-target, and deep-descendant protections
- MainViewModel dirty-state and projection-coherence tests
- Move to... target-tree validation
- bookmark-list and folder-tree Drag & Drop behavior
- dedicated bookmark and folder Drag & Drop mechanical UI guards
- V0.6 no-write production-source safety gate
- named MoveScale gate using 10,000 synthetic URLs in normal CI
- complete regression suite, Windows x64 single-file publish, and EXE startup smoke test
- Actions #145 passed the complete Task 7 folder Drag & Drop pipeline
- Actions #148 passed MoveScale, move safety, the complete test suite, publish, single-file verification, smoke test, and artifact upload

The named 10,000-URL MoveScale test completed in approximately 50 ms in its dedicated gate on the GitHub Windows runner (and approximately 73 ms when repeated inside the full suite). These timings are diagnostic observations only, not a performance guarantee.

V0.6 remains strictly in-memory. The source Chrome `Bookmarks` file is not saved, replaced, repaired, checksummed, backed up, or otherwise modified by V0.6 code.


## Roadmap

- **V0.1 — Bootstrap: completed** — project shell, tests, privacy guardrails, CI, single EXE
- **V0.2 — Chrome Bookmarks Reader: completed** — native bookmark parsing, validation, cancellation, metadata preservation, read-only WPF loading; Windows 10 owner acceptance passed with the private source file unchanged
- **V0.3 — Browser UI: completed** — folder tree, selected-folder bookmark list, status summaries, virtualization/recycling; Windows 10 owner acceptance passed with the private source file unchanged
- **V0.4 — Search / Index: completed** — in-memory indexing, read-only search, result navigation; automated release gates, Windows 10 owner acceptance, merge, and post-merge verification complete
- **V0.5 — Editing: completed** — in-memory add/rename/edit URL, dirty-state tracking, explicit discard protection, Windows 10 owner acceptance, PR #5 merge, and post-merge verification complete
- **V0.6 — Move / Drag & Drop: implementation and automated release gates complete; Windows 10 owner acceptance pending** — bookmark/folder Move to..., reorder, Drag & Drop, hierarchy protection, MoveScale, and no-write safety gates
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
- [V0.5 implementation plan](docs/superpowers/plans/2026-09-19-v0.5-editing.md)
- [V0.6 approved design spec](docs/superpowers/specs/2026-09-20-v0.6-move-drag-drop-design.md)
- [V0.6 implementation plan](docs/superpowers/plans/2026-09-20-v0.6-move-drag-drop.md)

## Development principles

```text
Read broadly.
Edit explicitly.
Write conservatively.
Backup always.
Verify after writing.
```

Data correctness and data safety take priority over feature count and visual polish.
