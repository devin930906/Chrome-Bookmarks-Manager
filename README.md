# Chrome Bookmarks Manager

Chrome Bookmarks Manager is a Windows desktop application for managing very large Chrome bookmark libraries. The project is designed around local-first operation, explicit data-safety boundaries, reproducible builds, and eventual support for safe editing of Chrome's native `Bookmarks` file.

## Project status

**Pre-release — V0.8 Undo / Redo completed**

V0.8 adds reversible in-memory command history across the completed V0.5 editing, V0.6 movement, and V0.7 delete/batch mutation surfaces. Automated validation, Windows 10 owner acceptance, PR #8 merge, and post-merge main verification are complete.

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
- named V0.6 MoveScale verification at 10,000 synthetic URLs plus 1,000 synthetic folders in normal CI
- V0.6 production-source safety gate rejects file-write/write-back primitives
- bookmark list supports Ctrl/Shift multi-selection and Ctrl+A for the displayed results
- Delete key and bookmark/folder context menus support confirmed single and batch deletion
- batch bookmark deletion works across folders, removes duplicate selections once, and keeps surviving item order
- batch Move to... preserves selected bookmark order across one or more source folders
- delete confirmation defaults to Cancel and folder confirmation reports recursive URL/folder counts
- named V0.7 DeleteBatchScale verification for a 10,000-bookmark batch, a 10,000-bookmark folder subtree, active-search rebuild, and 1,000 synthetic folders in normal CI
- V0.7 production-source safety gate rejects file-write/write-back primitives and File.Delete
- batch edits remain in memory; no Save or source-file write-back path is available
- V0.8 bounded 200-operation Undo/Redo history spans add, rename, URL edit, move/reorder, batch move, single/batch delete, and recursive folder delete
- Undo/Redo preserves original node identity, exact mixed-child placement, document counts, and search/index coherence
- Undoing all reachable changes back to the loaded baseline returns the document to clean state; a divergent new edit clears the redo branch
- Undo is available through Ctrl+Z; Redo through Ctrl+Y or Ctrl+Shift+Z, with visible menu/toolbar controls
- V0.8 history remains graph-local and in memory; it does not serialize or write the Chrome source file

V0.5 editing, V0.6 movement, V0.7 deletion/batch operations, and the V0.8 work-in-progress history layer change only the in-memory document. Save, overwrite, repair, and production Chrome write-back are not available. Dirty-document reload and app close require explicit Discard / Cancel confirmation; there is no Save path.

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

V0.5 editing, V0.6 movement, and V0.7 deletion change only the in-memory bookmark graph. Opening a file reads the source; search, editing, Move to..., Drag & Drop, and Delete do not write to or delete the source file. A dirty reload or app close requires an explicit Discard choice, and no Save/write path exists. Production Chrome profile write-back remains intentionally out of scope until V0.9.

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
$env:CBM_MOVE_FOLDER_COUNT = "4000"
dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=MoveScale"
Remove-Item Env:CBM_MOVE_URL_COUNT
Remove-Item Env:CBM_MOVE_FOLDER_COUNT
```

Run the V0.7 batch deletion scale gate with a larger synthetic workload when preparing a release:

```powershell
$env:CBM_DELETE_URL_COUNT = "250000"
$env:CBM_DELETE_FOLDER_COUNT = "4000"
dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=DeleteBatchScale"
Remove-Item Env:CBM_DELETE_URL_COUNT
Remove-Item Env:CBM_DELETE_FOLDER_COUNT
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
node scripts/Verify-V07DeleteUi.mjs
pwsh -NoProfile -File scripts/Verify-V07DeleteSafety.ps1
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
11. V0.7 production-source no-write/no-file-delete safety verification
12. named BrowserScale gate
13. named SearchScale gate
14. named V0.6 MoveScale gate with 10,000 synthetic URLs plus 1,000 synthetic folders
15. named V0.7 DeleteBatchScale gate with 10,000 synthetic URLs plus 1,000 synthetic folders
16. full xUnit test suite
17. self-contained Windows x64 publish
18. single-file output verification
19. executable startup smoke test
20. artifact upload

The normal CI BrowserScale and SearchScale gates use synthetic 10,000-URL workloads. MoveScale and DeleteBatchScale use synthetic 10,000-URL plus 1,000-folder workloads; DeleteBatchScale also verifies recursive subtree removal and active-search refresh. The 250,000-URL browser-state, search, move, and delete/batch measurements remain explicit release commands rather than permanent heavy normal CI steps.

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

V0.6 implementation, automated release-candidate gates, final author review, Windows 10 owner acceptance, PR #6 merge, and post-merge `main` verification are complete.

Automated evidence includes:

- controlled domain mutation and atomic move-service tests
- same-parent mixed-child bookmark/folder reorder semantics
- root, foreign-document, self-target, and deep-descendant protections
- MainViewModel dirty-state and projection-coherence tests
- Move to... target-tree validation
- bookmark-list and folder-tree Drag & Drop behavior
- dedicated bookmark and folder Drag & Drop mechanical UI guards
- V0.6 no-write production-source safety gate
- named MoveScale gate using 10,000 synthetic URLs plus 1,000 interleaved synthetic folders in normal CI
- complete regression suite, Windows x64 single-file publish, and EXE startup smoke test
- Actions #145 passed the complete Task 7 folder Drag & Drop pipeline
- Actions #148 passed MoveScale, move safety, the complete test suite, publish, single-file verification, smoke test, and artifact upload
- Actions #149 passed the release-candidate commit including the dedicated folder Drag & Drop UI guard and the complete regression/publish pipeline
- Actions #152 passed after partial owner-acceptance documentation was recorded, confirming the complete Windows pipeline remained green
- Windows 10 owner acceptance passed on 2026-09-20 using the owner's private Chrome Bookmarks file: bookmark/folder reorder, cross-folder Drag & Drop, bookmark/folder Move to..., invalid self/descendant protection, protected roots, search coherence, and dirty Discard/Cancel behavior all passed
- source-file SHA-256, byte length, and LastWriteTimeUtc were unchanged before/after acceptance; all three comparisons returned `True`; no private bookmark content was committed or uploaded
- PR #6 merged to `main` as `ca11a95127d639d28ceaad8cab251c00a1ede840`
- post-merge `main` Actions #156 passed the complete Windows pipeline

MoveScale timing is recorded as diagnostic evidence only and is not encoded as a correctness threshold. The final release-candidate fixture intentionally mixes a large bookmark list with many folder slots so bookmark and folder reordering exercise the real mixed-child model.

V0.6 remains strictly in-memory. The source Chrome `Bookmarks` file is not saved, replaced, repaired, checksummed, backed up, or otherwise modified by V0.6 code.

## V0.7 verification status

V0.7 implementation, automated release-candidate validation, Windows 10 owner acceptance, PR #7 merge, and post-merge `main` verification are complete.

- Tasks 1–7 completed domain removal accounting, single and batch delete services, batch Move to..., ViewModel orchestration, WPF multi-select/delete commands, release safety and scale gates, and documentation.
- Actions #177 passed the complete Windows pipeline at implementation commit `f0e6012b2760373c47d958e2b75b61e2e22a15cc`.
- Actions #178 passed the complete Windows pipeline at the final PR head `b3a4be35ce41652eb90b77a0cc713e17caaf4fdb`.
- Windows 10 owner acceptance passed on 2026-09-20 against the owner's private Chrome Bookmarks file. The owner confirmed the complete Task 8 checklist passed, including single and batch deletion, ordinary-folder subtree deletion, protected-root rejection, Ctrl/Shift/Ctrl+A selection, batch Move to..., search-result mutation/coherence, count refresh, dirty Discard/Cancel behavior, V0.6 Drag & Drop regression coverage, and the required source-file integrity checks.
- No private bookmark content was committed or uploaded.
- PR #7 merged to `main` as `3d45217d7df540a40a1af0a388d858c0dab3f0c6`.
- Post-merge `main` Actions #179 passed the complete Windows pipeline, including V0.7 UI, delete safety, DeleteBatchScale, full tests, publish, single-file verification, smoke test, and artifact upload.


## V0.8 release-candidate status

V0.8 implementation now covers the core history engine, reversible mutation entries, ViewModel integration for V0.5 editing / V0.6 movement / V0.7 deletion, and WPF Undo/Redo controls.

- Task 1: RED Actions #182 / GREEN Actions #183.
- Task 2: RED Actions #184 / GREEN Actions #185.
- Task 3: RED Actions #186 / GREEN Actions #187.
- Task 4: RED Actions #188 / GREEN Actions #189.
- Task 5: RED Actions #190 / GREEN Actions #191.
- Task 6 UI contract: RED Actions #193; GREEN Actions #195 passed the complete Windows pipeline.
- V0.8 UndoRedoScale and dedicated no-write history safety gates are wired into normal Windows CI.
- Actions #198 passed the complete V0.8 release-candidate Windows pipeline at `606e1f7d6dc7080542f9c13358fddbcf3ed3c180`.
- Final whole-branch review found no Critical or Important defects.
- Windows 10 owner acceptance passed on 2026-09-20; SHA-256, byte length, and LastWriteTimeUtc were all unchanged.
- PR #8 merged to `main` at `1529600255614398f7b8b1193ea21d4649c1d569`.
- Post-merge `main` Actions #204 passed the complete Windows pipeline.
- **V0.8 is completed. Next milestone: V0.9 — Safe Chrome Write.**
- V0.8 remains strictly in-memory. Safe Chrome source-file persistence remains deferred to V0.9.




## V0.9 Safe Chrome Write — pre-release safety status

V0.9 introduces the first explicit write-back path to the loaded clear-text Chrome `Bookmarks` file. **This branch is still pre-release and test-profile-only until the disposable Chrome profile compatibility gate and Windows 10 production owner acceptance are completed.**

The Save path is intentionally conservative:

- Save is explicit; editing alone never writes the source file.
- Google Chrome must be fully closed. A detected `chrome.exe` blocks Save.
- The source file is protected by an accepted SHA-256 / byte-length / LastWriteTimeUtc baseline. Any external change blocks overwrite and requires reload.
- New JSON is written to a unique same-directory temporary file first.
- The temporary file is reopened and logically validated before the source is touched.
- A unique application-owned backup is copied and verified by SHA-256 and byte length before replacement.
- Application backups use a pattern such as `Bookmarks.ChromeBookmarksManager.YYYYMMDD-HHmmss.fff.bak`; the application does **not** overwrite Chrome's own `Bookmarks.bak`.
- Final replacement uses the persistence boundary's `File.Replace` path rather than delete-then-move.
- The replaced source is reopened and validated again before Save is reported as successful.
- A failed pre-replace stage leaves the original source untouched. If an unexpected post-replace validation failure occurs, the verified application backup is retained and the UI reports that recovery may be required.
- Save / Discard / Cancel protects dirty close and dirty reload flows. A failed Save keeps the current in-memory document open and dirty.

### V0.9 automated gates

Normal Windows CI now includes:

- Chromium-compatible MD5 + SHA-256 bookmark checksum vectors;
- writer read → write → read round-trip and metadata/unknown-field preservation;
- ID/GUID compatibility validation;
- source baseline conflict detection;
- Chrome process safety detection;
- backup-first atomic transaction and failure injection;
- application-level Safe Save orchestration;
- MainViewModel save/history clean-checkpoint behavior;
- V0.9 WPF Save / Ctrl+S / dirty-flow contract;
- V0.9 persistence safety static gate;
- V0.9 `WriteScale` using 10,000 synthetic URLs;
- full regression, Windows x64 single-file publish, executable smoke test, and one-day artifact retention.

Run the normal write-scale gate:

```powershell
dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=WriteScale"
```

Explicit 250,000-URL release measurement:

```powershell
$env:CBM_WRITE_URL_COUNT = "250000"
dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=WriteScale" --logger "console;verbosity=normal"
Remove-Item Env:CBM_WRITE_URL_COUNT
```

Timing and memory values are diagnostic evidence, not correctness thresholds.

### Do not use the production Chrome profile yet

Before production-profile acceptance, V0.9 must first pass the disposable Chrome test-profile sequence in the implementation plan: save synthetic bookmarks, open the result in Chrome, let Chrome rewrite the file, reload it in the app, verify Unicode/nesting/checksums, and repeat the save cycle. Only after that gate passes will production owner acceptance begin with an independent external backup of the real `Bookmarks` file.

No real bookmark titles, URLs, raw private `Bookmarks` files, backups, test-profile private data, or screenshots containing private bookmark data may be committed or uploaded.

## Roadmap

- **V0.1 — Bootstrap: completed** — project shell, tests, privacy guardrails, CI, single EXE
- **V0.2 — Chrome Bookmarks Reader: completed** — native bookmark parsing, validation, cancellation, metadata preservation, read-only WPF loading; Windows 10 owner acceptance passed with the private source file unchanged
- **V0.3 — Browser UI: completed** — folder tree, selected-folder bookmark list, status summaries, virtualization/recycling; Windows 10 owner acceptance passed with the private source file unchanged
- **V0.4 — Search / Index: completed** — in-memory indexing, read-only search, result navigation; automated release gates, Windows 10 owner acceptance, merge, and post-merge verification complete
- **V0.5 — Editing: completed** — in-memory add/rename/edit URL, dirty-state tracking, explicit discard protection, Windows 10 owner acceptance, PR #5 merge, and post-merge verification complete
- **V0.6 — Move / Drag & Drop: completed** — bookmark/folder Move to..., reorder, Drag & Drop, hierarchy protection, MoveScale, no-write safety gates, Windows 10 owner acceptance, PR #6 merge, and post-merge verification complete
- **V0.7 — Delete / Batch: completed** — single and recursive deletion, bookmark multi-selection, batch delete/Move to..., DeleteBatchScale, no-write/no-file-delete safety, Windows 10 owner acceptance, PR #7 merge, and post-merge verification complete
- **V0.8 — Undo / Redo: completed** — bounded reversible command history covers V0.5–V0.7 mutations with history-position dirty state and Ctrl+Z/Ctrl+Y/Ctrl+Shift+Z; automated validation, whole-branch review, Windows 10 owner acceptance, PR #8 merge, and post-merge verification are complete
- **V0.9 — Safe Chrome Write: in development / test-profile-only** — checksum, deterministic writer, external-change detection, Chrome-process guard, verified backup, atomic replace, Save UI, persistence safety/scale gates, then disposable-profile and production owner acceptance
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
- [V0.7 design spec](docs/superpowers/specs/2026-09-20-v0.7-delete-batch-design.md)
- [V0.7 implementation plan](docs/superpowers/plans/2026-09-20-v0.7-delete-batch.md)
- [V0.8 design spec](docs/superpowers/specs/2026-09-20-v0.8-undo-redo-design.md)
- [V0.8 implementation plan](docs/superpowers/plans/2026-09-20-v0.8-undo-redo.md)
- [V0.9 design spec](docs/superpowers/specs/2026-09-20-v0.9-safe-chrome-write-design.md)
- [V0.9 implementation plan](docs/superpowers/plans/2026-09-20-v0.9-safe-chrome-write.md)

## Development principles

```text
Read broadly.
Edit explicitly.
Write conservatively.
Backup always.
Verify after writing.
```

Data correctness and data safety take priority over feature count and visual polish.
