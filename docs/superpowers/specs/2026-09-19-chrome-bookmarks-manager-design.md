# Chrome Bookmarks Manager — Design Spec

> **Repository:** `devin930906/Chrome-Bookmarks-Manager`  
> **Status:** APPROVED DESIGN BASELINE  
> **Approved:** 2026-09-19  
> **Primary user:** DevinOoi  
> **Target:** Windows 10 / Windows 11 x64  
> **Delivery:** self-contained single Windows EXE  
> **Development method:** Superpowers architectural workflow + TDD

---

## 1. Purpose

Build **Chrome Bookmarks Manager**, a local-first Windows desktop application for managing Chrome's native `Bookmarks` file at a scale substantially larger than Chrome's built-in bookmark UI is comfortable to manage manually.

The product is not a generic JSON editor. It must understand Chrome bookmark structure, preserve data it does not understand, remain usable with a very large bookmark library, and treat write-back to Chrome as a high-risk operation that is enabled only after explicit compatibility verification.

The application is for personal use first. It does not require accounts, cloud services, browser extensions, a server, or a remote database.

---

## 2. Real-world data baseline

The current real bookmark file used as the performance and safety reference is approximately:

| Metric | Baseline |
|---|---:|
| File size | ~119 MB |
| URL nodes | ~209,382 |
| Folder nodes | ~3,404 |
| Maximum observed depth | ~7 |
| Bookmark format version | 1 |
| Primary roots | `bookmark_bar`, `other`, `synced` |
| URL nodes with extra metadata | ~522 |

The real private file is **never committed to GitHub and never uploaded to GitHub Actions**.

Synthetic and anonymized fixtures are used in CI.

---

## 3. Product goals

The final application must support:

- Open Chrome native `Bookmarks`
- Browse bookmark roots and folder hierarchy
- Browse a selected folder's children
- Search by bookmark name and URL
- Add bookmarks
- Add folders
- Rename bookmarks/folders
- Edit URLs
- Delete single or multiple nodes
- Move nodes
- Reorder nodes
- Drag and drop
- Batch operations
- Undo / Redo
- Detect exact duplicate URLs
- Import/export in later versions
- Safely write the resulting structure back to Chrome
- Produce a downloadable Windows EXE through GitHub Actions

The application should behave more like a high-density Windows file manager than a decorative dashboard.

---

## 4. Non-goals for V1.0

The first stable release does **not** need:

- Cloud sync
- User accounts
- Web application
- Mobile application
- macOS support
- Linux support
- Chrome extension
- AI automatic classification
- Large-scale online dead-link checking
- Multi-user collaboration
- Server-side database
- SQLite as the primary bookmark store
- Online favicon crawling by default

These are intentionally deferred so data correctness, safety, performance, and maintainability remain the priority.

---

## 5. Technology decision

### 5.1 Stack

Use:

- C#
- .NET 10
- WPF
- XAML
- System.Text.Json
- xUnit
- GitHub Actions
- `win-x64` publish target

### 5.2 Why WPF

WPF matches the actual constraints:

- Windows-only
- local file access
- large data lists
- tree + list management UI
- mature binding
- mature virtualization
- direct .NET file APIs
- straightforward self-contained Windows publishing

### 5.3 Explicitly not selected

Electron, Tauri 2, and a WebView2-based shell are not used for V1.0.

They are technically capable, but add web-runtime or multi-language complexity that does not materially improve this Windows-only local application.

---

## 6. Distribution

Target deliverable:

```text
ChromeBookmarksManager.exe
```

The publish configuration must target:

- `net10.0-windows`
- `win-x64`
- self-contained
- single-file
- Release

The user should not need to install .NET, Node.js, Rust, Python, or Visual Studio.

The final claim that the app is "single EXE" is accepted only after GitHub Actions output and Windows 10 real-machine testing confirm the published artifact has no required sidecar runtime files.

Unsigned GitHub-built EXEs may trigger Windows SmartScreen. Code signing is optional for V1.0.

---

## 7. Architecture

Use clear, testable boundaries:

```text
WPF UI
  ↓
ViewModels / Commands
  ↓
Application Services
  ↓
Domain Model
  ↓
Chrome Bookmark Codec
  ↓
Chrome Bookmarks JSON
```

Infrastructure services provide filesystem access, atomic replacement, settings, logging, and Chrome process detection.

### 7.1 Boundary rules

- UI must not parse JSON.
- UI must not directly write bookmark files.
- ViewModels must not contain Chrome persistence logic.
- Codec must not display UI dialogs.
- Search must not own saving.
- Save must not own bookmark-editing behavior.
- Domain logic must be testable without WPF.
- Private real bookmark files must not be needed for unit tests.

---

## 8. Proposed repository structure

```text
Chrome-Bookmarks-Manager/
├─ .github/
│  └─ workflows/
│     ├─ build-windows.yml
│     └─ release-windows.yml
├─ docs/
│  └─ superpowers/
│     ├─ specs/
│     └─ plans/
├─ src/
│  └─ ChromeBookmarksManager/
│     ├─ Domain/
│     ├─ Chrome/
│     ├─ Application/
│     ├─ ViewModels/
│     ├─ Views/
│     ├─ Infrastructure/
│     └─ Commands/
├─ tests/
│  └─ ChromeBookmarksManager.Tests/
├─ samples/
│  └─ Bookmarks.sample.json
├─ .gitignore
├─ README.md
└─ ChromeBookmarksManager.slnx
```

Files should remain focused. Avoid a giant `MainWindow.xaml.cs`, giant service, or "God class".

---

## 9. Chrome bookmark compatibility

Chrome `Bookmarks` is JSON, but it must not be treated as arbitrary JSON.

The codec must account for at least:

- `checksum`
- `version`
- `roots`
- `bookmark_bar`
- `other`
- `synced`
- node `id`
- node `guid`
- node `name`
- node `type`
- node `url`
- `children`
- `date_added`
- `date_modified`
- `date_last_used`
- `meta_info`
- unknown future fields

### 9.1 Unknown-field preservation

Unknown properties must be retained through a read → edit → write round trip unless there is verified Chromium behavior requiring otherwise.

The implementation should keep an extension-data/raw-property container in the model or persistence layer rather than dropping properties that are not currently mapped.

### 9.2 IDs and GUIDs

Before creation/copy/write functionality is finalized, Chromium's current behavior must be verified for:

- new ID generation
- GUID generation
- uniqueness
- root node restrictions
- copy semantics
- move semantics

Do not invent rules based on guesswork.

### 9.3 Chrome time

Chrome bookmark timestamps must be handled by a dedicated conversion component such as `ChromeBookmarkTime`.

Persistence format and human-readable UI format are separate responsibilities.

### 9.4 Checksum

Before production write-back is enabled:

1. inspect current Chromium source,
2. implement checksum behavior in an isolated component,
3. test against Chrome-generated files,
4. modify test-profile data and let Chrome read it,
5. verify file behavior after Chrome closes and persists again.

"Valid JSON" is not sufficient evidence of Chrome compatibility.

---

## 10. Large-file strategy

The design must assume ~119 MB and ~209k URL nodes from the beginning.

### 10.1 Parsing

Preferred direction:

```text
FileStream
  ↓
Utf8JsonReader
  ↓
Domain Model
```

Writing:

```text
Domain Model
  ↓
Utf8JsonWriter
  ↓
Temporary File
```

A streaming reader is preferred because it avoids unnecessary giant UTF-16 strings and full JSON DOM duplication, but final selection must be validated by benchmark results.

### 10.2 UI thread

Large parsing, indexing, duplicate analysis, and write operations must not permanently block the WPF UI thread.

Use async/background execution where appropriate and support `CancellationToken` for long-running operations.

### 10.3 Progress

Use honest progress reporting.

If exact percentage cannot be calculated, display indeterminate stages such as:

- Reading Bookmarks
- Parsing
- Validating
- Building search index

Do not display fabricated percentages.

---

## 11. Domain model

Core model:

```text
BookmarkDocument
├─ Roots
│  ├─ BookmarkBar
│  ├─ Other
│  └─ Synced
└─ Nodes
   ├─ BookmarkFolder
   └─ BookmarkUrl
```

Common node state includes:

- Id
- Guid
- Name
- Type
- date fields
- Parent
- unknown properties

URL nodes include URL and metadata.

Folder nodes include ordered children.

The model must make illegal hierarchy operations difficult to perform.

---

## 12. Index and search

Search must operate against in-memory model/index data, not repeatedly reread the 119 MB file.

Useful indexes may include:

- ID index
- GUID index
- parent index
- exact URL index
- normalized lookup data where justified
- text lookup data

Initial search supports:

- bookmark name
- URL
- domain text
- current folder
- global scope

Search input should be debounced.

Search results should reference existing nodes rather than clone whole node objects.

---

## 13. UI design

Primary layout:

```text
┌──────────────────────────────────────────────────────────────┐
│ Chrome Bookmarks Manager                           ─ □ ×     │
├──────────────────────────────────────────────────────────────┤
│ File  Edit  View  Tools                   [Search.........]  │
├───────────────────┬──────────────────────────────────────────┤
│ Folder tree       │ Current folder / search results          │
│                   │                                          │
│ Bookmarks bar     │ Name                  URL                 │
│ Other bookmarks   │ ...                                       │
│ Mobile bookmarks  │ ...                                       │
├───────────────────┴──────────────────────────────────────────┤
│ counts │ state │ changed items │ source path                 │
└──────────────────────────────────────────────────────────────┘
```

Design priority:

1. information density
2. predictable Windows interaction
3. keyboard support
4. multi-selection
5. context menus
6. drag/drop
7. status visibility
8. visual polish

### 13.1 Virtualization

Never create one WPF control for every bookmark.

The list must use UI virtualization/recycling. Search results with tens of thousands of matches must not materialize tens of thousands of visible controls simultaneously.

The folder tree starts collapsed or minimally expanded; no automatic expand-all.

---

## 14. Application state

Use explicit states such as:

- `NoDocument`
- `Loading`
- `LoadedClean`
- `LoadedDirty`
- `Saving`
- `SaveFailed`

Commands are enabled according to state.

Example:

- Save disabled in `LoadedClean`
- Save enabled in `LoadedDirty`
- risky editing constrained during `Saving`

Closing a dirty document prompts:

- Save
- Don't save
- Cancel

If Save fails, the close flow must not behave as if saving succeeded.

---

## 15. Editing

UI actions invoke application commands/services rather than mutate JSON directly.

Core operations:

- Add bookmark
- Add folder
- Rename
- Edit URL
- Delete
- Move
- Reorder
- Batch delete
- Batch move

Root nodes have special protection and cannot be treated like ordinary folders.

---

## 16. Move and drag/drop invariants

Prevent:

- moving a folder into itself
- moving a folder into its descendant
- moving protected roots
- deleting protected roots
- one node belonging to multiple parents
- corrupted child ordering

A successful move updates:

- source parent's children
- target parent's children
- node parent reference
- relevant indexes
- UI
- Undo history

---

## 17. Sort semantics

Distinguish:

1. temporary UI sort, and
2. persisted bookmark order.

Clicking a column header must not silently rewrite Chrome's bookmark order.

Persisted sorting requires an explicit operation.

---

## 18. Undo / Redo

Use differential/command history, not full-document clones.

Example:

```text
DeleteAction
- Parent
- PreviousIndex
- NodeReference
```

Do not store a full ~119 MB before/after document snapshot for each action.

Operations that should be reversible include:

- rename
- edit URL
- add
- delete
- move
- reorder
- batch delete where practical

---

## 19. Safe save design

Production saving is intentionally deferred until V0.9.

The desired pipeline is:

```text
Current Domain Model
  ↓
Pre-save validation
  ↓
Update verified Chrome metadata/checksum
  ↓
Write temporary file
  ↓
Flush
  ↓
Re-read temporary file
  ↓
Validate temporary file
  ↓
Back up original
  ↓
Atomic replacement
  ↓
Re-read final file
  ↓
Post-save validation
```

Direct `File.WriteAllText(originalPath)` replacement is prohibited.

If any validation step fails, saving stops.

---

## 20. Backup and rollback

Before production replacement, create a dated backup such as:

```text
Bookmarks.backup-20260919-170000
```

Early versions should favor retention over automatic cleanup.

The first stable release may keep backups until the user manually removes them.

Saving failure must leave enough information to recover.

---

## 21. Chrome process protection

When the save target is an active Chrome profile file, detect `chrome.exe`.

If Chrome is running, show a clear warning explaining that Chrome can overwrite or rewrite bookmark data.

Initial policy should be conservative:

- Cancel
- Save As
- Advanced "save anyway" only when deliberately chosen

The early production-safe version may block direct overwrite by default while Chrome is running.

---

## 22. Privacy and logging

The application is local-first.

Do not upload bookmark contents.

Local logs may contain:

- application version
- operation name
- timing
- exception type
- file size
- node counts

Do not log the complete private bookmark URL/title dataset by default.

---

## 23. Git privacy rules

The repository must ignore real bookmark files and private test data.

Initial `.gitignore` must cover at least:

```gitignore
Bookmarks
Bookmarks.bak
Bookmarks.*
*.backup
*.bak

UserData/
PrivateData/
TestData/Private/
```

CI uses only anonymous fixtures and generated datasets.

---

## 24. Testing strategy

Use TDD for core logic:

```text
RED
↓
GREEN
↓
REFACTOR
```

Critical test areas:

### Reader
- basic file
- nested folders
- empty folders
- URLs
- `meta_info`
- unknown fields
- malformed JSON
- missing roots

### Domain
- parent/child relationships
- root protection
- move
- reorder
- add
- delete

### Search
- name
- URL
- case handling
- Unicode
- Chinese
- large synthetic dataset

### Writer
- round trip
- unknown field preservation
- metadata preservation
- ordering
- dates
- IDs
- GUIDs

### Save
- temp write
- backup
- validation failure
- replacement failure
- rollback/recovery

---

## 25. Round-trip verification

A core regression test is:

```text
Input Bookmarks
  ↓ Read
Domain Model
  ↓ Write
Output Bookmarks
  ↓ Read again
```

Verify at least:

- same logical node count
- same hierarchy
- same URLs
- same names
- `meta_info` retained
- unknown fields retained
- untouched data not lost

---

## 26. Performance testing

Create generated benchmark datasets around:

- 10k URLs
- 100k URLs
- 250k URLs
- 500k URLs

Measure:

- parse duration
- index build duration
- search duration
- memory consumption
- write duration

Do not invent target numbers before observing baseline measurements.

---

## 27. GitHub Actions

Initial build pipeline:

```text
Push / Pull Request / workflow_dispatch
  ↓
Checkout
  ↓
Setup .NET 10
  ↓
Restore
  ↓
Build
  ↓
Test
  ↓
Publish win-x64 self-contained single-file
  ↓
Verify EXE
  ↓
Upload Artifact
```

Expected artifact:

```text
ChromeBookmarksManager-win-x64/
└─ ChromeBookmarksManager.exe
```

No private `Bookmarks` file is ever sent to CI.

---

## 28. Release workflow

Later release flow:

```text
Tag vX.Y.Z
  ↓
Build
  ↓
Test
  ↓
Publish
  ↓
SHA256
  ↓
GitHub Release
  ↓
Attach EXE + checksum
```

Use Semantic Versioning:

```text
MAJOR.MINOR.PATCH
```

---

## 29. Delivery roadmap

### V0.1 — Bootstrap

Create a stable project baseline:

- .NET 10 WPF application
- solution
- test project
- basic main window
- base project folders
- anonymous sample fixture
- `.gitignore`
- README
- GitHub Actions build
- self-contained single-EXE publish
- downloadable Artifact

**No Chrome production write support.**

### V0.2 — Reader

- domain model
- roots
- folders
- URLs
- metadata
- unknown fields
- validation
- large-file loading

Still read-only.

### V0.3 — Browser UI

- folder tree
- bookmark list
- selection
- status bar
- virtualization

### V0.4 — Search / Index

- in-memory index
- debounced search
- global/current-folder search
- navigation to result

### V0.5 — Editing

- add
- rename
- edit URL
- add folder
- dirty-state tracking

Edits may remain in-memory / Save As only.

### V0.6 — Move / Drag & Drop

- move
- reorder
- drag/drop
- move dialog
- cycle/root protection

### V0.7 — Delete / Batch

- multi-select
- delete
- batch delete
- batch move
- confirmation

### V0.8 — Undo / Redo

- command history
- reversible edits
- reversible moves
- reversible deletes

### V0.9 — Safe Chrome Write

- writer
- checksum
- Chrome metadata verification
- validation
- backup
- atomic replacement
- Chrome process detection
- round-trip
- real Chrome test-profile verification

Only after V0.9 Release Gate passes may direct production-profile write-back be enabled.

### V1.0 — Stable personal-use release

Includes the complete daily-use management path with safe saving.

---

## 30. V0.9 hard release gate

Production Chrome write-back remains disabled until all critical checks pass:

- current Chromium bookmark persistence format reverified
- checksum behavior reverified
- writer unit tests complete
- unknown-field round trip passes
- `meta_info` round trip passes
- Chrome-time round trip passes
- ID/GUID behavior verified
- root protection passes
- temporary write passes
- backup passes
- atomic replacement passes
- replacement-failure test passes
- post-write reload passes
- real Chrome test profile passes
- Chrome can start and show the edited data
- after Chrome exits, resulting file remains coherent
- GitHub Actions is green
- Release EXE works on Windows 10 x64
- independent external backup of production bookmarks exists

Failure of any critical data-safety check means production overwrite stays disabled.

---

## 31. Dependency policy

Prefer:

- .NET BCL
- WPF
- System.Text.Json

Any new NuGet package must have a concrete justification covering:

- why it is needed
- viable alternatives
- license
- maintenance status
- single-file impact
- native dependency impact

Avoid dependency growth merely for convenience.

---

## 32. Settings and local app data

The application may store its own settings/log/cache under:

```text
%LOCALAPPDATA%\ChromeBookmarksManager\
```

This does not violate the "single EXE" distribution requirement.

Single EXE means the published application package requires only the EXE, not that the running application may never create settings or logs.

Portable mode may be added later.

---

## 33. Error-handling principles

Errors must be specific and actionable.

Do not show only "Error".

Examples:

- file not found
- permission denied
- malformed JSON
- unsupported bookmark structure
- validation failed
- temp write failed
- backup failed
- final replace failed

For data-risk errors, abort rather than attempt a best-effort write.

---

## 34. Core invariants

The implementation must preserve these project-wide invariants:

1. `main` should remain buildable.
2. Real private bookmarks never enter GitHub.
3. Unknown Chrome fields are not silently discarded.
4. Protected roots cannot be moved/deleted like normal folders.
5. UI does not directly write files.
6. Writes require validation.
7. Production replacement requires backup.
8. Long operations do not permanently block the UI thread.
9. 200k nodes do not become 200k simultaneous WPF controls.
10. Undo stores deltas/actions rather than whole-document clones.
11. Completion claims require test or runtime evidence.
12. Chrome write compatibility is verified, not assumed.

---

## 35. Development process

This project follows the Superpowers architectural workflow:

```text
Design
↓
Written Spec
↓
User Review
↓
Implementation Plan
↓
User Review / execution choice
↓
TDD
↓
Implementation
↓
Verification
↓
Code Review
↓
Merge
```

Implementation must not start before the written implementation plan is reviewed.

Core code work uses:

- small testable tasks
- TDD where appropriate
- frequent commits
- verification before completion
- focused files with clear interfaces

---

## 36. First implementation milestone acceptance criteria

V0.1 is complete only when all applicable checks pass:

- repository has the project structure
- WPF project builds
- test project builds
- tests pass
- GitHub Actions passes
- publish command passes
- Windows x64 self-contained single EXE artifact exists
- artifact is downloadable
- EXE launches on Windows 10 x64
- no separate .NET install is required
- no private bookmark file exists in repository history
- README clearly states pre-release safety limitations

---

## 37. V1.0 acceptance criteria

V1.0 requires:

- real ~119 MB file can be read
- ~200k bookmark browsing is usable
- search works
- add/edit/delete/move works
- drag/drop works
- batch operations work
- Undo/Redo works
- dirty state is visible
- backups work
- atomic save works
- Chrome-process protection works
- checksum/Chrome compatibility is verified
- round-trip tests pass
- real Chrome test-profile validation passes
- Windows 10 smoke test passes
- GitHub Actions passes
- single EXE release passes

---

## 38. Design philosophy

The project follows:

```text
Read broadly.
Edit explicitly.
Write conservatively.
Backup always.
Verify after writing.
```

Priority order:

```text
Data correctness
> Data safety
> Tests
> Performance
> Usability
> Visual polish
> Advanced features
```

The intended end result is not "a JSON editor with bookmark fields."

It is a **local-first, data-safety-first, Windows bookmark manager designed for a real 200k-scale Chrome bookmark library and maintained through a reproducible GitHub Actions single-EXE pipeline.**
