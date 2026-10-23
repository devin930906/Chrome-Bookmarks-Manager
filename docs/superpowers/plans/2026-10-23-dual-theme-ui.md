# Night Workspace Dual Theme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved night workspace layout with persistent Light/Dark modes while retaining every existing desktop operation.

**Architecture:** Keep WPF and the existing MainViewModel/domain services. Share semantic brushes through replaceable theme dictionaries, style existing controls, and add a presentation-only partial window layer for details, theme and window state. Extend settings without losing language or theme.

**Tech Stack:** C#, .NET 10, WPF, xUnit, PowerShell 7. No new runtime framework or remote assets.

**Spec:** `docs/superpowers/specs/2026-10-23-chrome-bookmarks-manager-dual-theme-design.md` (user approved 2026-10-23).

## Global Constraints

- Keep WPF, current .NET 10 target, original Chrome data model and Safe Save services.
- Default window 1180×760, minimum 900×560; default theme dark, only `light` and `dark`.
- Keep language default zh-CN and immediate zh-CN/en-US switching.
- Theme changes must not rebuild ViewModel, document, search, selected nodes, history or dirty state.
- Preserve original handlers, keyboard/context/drag-drop rules and Can* command enablement.
- App UI is themed; native file pickers remain system controlled. Do not change Windows global settings.
- No real bookmarks or external image/icon requests in tests; use samples and example domains only.
- No release/publish to GitHub, no replacement of user-installed EXE. Local build/publish artifacts are allowed.

## Review Focus

1. Saving language after theme (and reverse) must preserve both, including old/corrupt configuration: Task 1 round-trip tests.
2. Menus/popups/dialogs opened after a theme change must resolve current brushes and preserve localized resources: Task 1/2/4 STA WPF tests.
3. New checkbox/details controls must not swallow Ctrl/Shift selection, text editing shortcuts or virtualization: Task 3 interaction and original contracts.
4. Small windows and English at increased DPI must retain reachable search, save/theme, cancel and details reopen controls: Task 3 real WPF layout measurements/renders.
5. MessageBox replacement must preserve Yes/No/Cancel results, cancellation defaults and save/delete guards: Task 4 dialog result tests and existing safety suite.

## Shared Interfaces

- `UserSettingsStore.LoadTheme(): string`, `SaveTheme(string): void`, `NormalizeTheme(string?): string`; `DefaultTheme="dark"`, `LightTheme="light"`.
- `ChromeBookmarksManager.Theming.ThemeService`: static `CurrentTheme`, `Initialize(string?)`, `SetTheme(string?)`, `ThemeChanged` event. Only swaps its own palette resource dictionary.
- Palette keys (Brush): AppBackgroundBrush, SidebarBackgroundBrush, SurfaceBrush, HoverBrush, PrimaryTextBrush, SecondaryTextBrush, MutedTextBrush, BorderBrush, AccentBrush, AccentForegroundBrush, SelectionBrush, FocusBrush, SuccessBrush, WarningBrush, DangerBrush, DisabledTextBrush.
- Controls resource dictionary: `Themes/Controls.xaml`. Keyed styles: PrimaryButtonStyle, DangerButtonStyle, QuietButtonStyle, IconButtonStyle, WorkspaceWindowStyle, WorkspaceTreeItemStyle, WorkspaceListItemStyle. Remaining standard controls use implicit styles.
- `tests/.../Ui/WpfTestHost.cs`: one process-wide STA dispatcher with one WPF Application and `Run(Action)` / `Run<T>(Func<T>)`, to avoid multiple Application instances across tests.

## Task 1: Persistent theme foundation

**Ownership:** worker in isolated `theme-foundation` worktree.

**Files:** Modify Infrastructure/UserSettingsStore.cs, App.xaml/.cs, tests project; create Theming/ThemeService.cs, Themes/Dark.xaml, Themes/Light.xaml, tests/Theming/ThemeSettingsTests.cs and Ui/WpfTestHost.cs.

- [ ] Add tests first: missing/legacy/bad JSON -> dark; light round-trip; SaveLanguage preserves light and SaveTheme preserves en-US; unsupported values normalize; read/write permission failures do not silently claim persistence.
- [ ] Run focused tests, verify missing theme APIs fail before production changes.
- [ ] Extend settings shared model; add palette/service which preserves localization and only replaces own resources. Startup initialize theme before base startup.
- [ ] Add STA tests: switching both directions changes brush values; repeated initialization has one palette; localization remains; no application gracefully updates logical theme.
- [ ] Run focused tests and build; commit only owned files with test evidence.

## Task 2: Shared controls and existing dialog visuals

**Ownership:** worker in isolated `workspace-controls` worktree, same token contract; does not modify palettes, App, MainWindow or localization.

**Files:** Create Themes/Controls.xaml; modify BookmarkEditDialog.xaml, BatchBookmarkImportDialog.xaml, MoveNodeDialog.xaml, DiscardChangesDialog.xaml. Add focused XAML/STA contract tests in tests/Ui/ControlThemeTests.cs (coordinate shared host with Task 1).

- [ ] Add failing contract verifying dynamic theme resources and all real dialog names/handlers remain.
- [ ] Style Window, Button, TextBox, ComboBox/popup, Menu/MenuItem/ContextMenu, ToolTip, TreeView/TreeViewItem, ListView/GridView/list rows, scrolling, status and splitter/focus/disabled states. Templates retain PARTs and virtualization presenter semantics.
- [ ] Apply common window style to existing dialogs, eliminate raw system color leaks, keep validators and handler wiring intact; no business changes.
- [ ] Verify XAML XML/compile on isolated branch and focused tests after integration; commit owned files.

## Task 3: Three-column main window and view-only state

**Ownership:** main thread in night-workspace worktree; integrate Task 1 and 2 commits before building themed UI.

**Files:** MainWindow.xaml, new MainWindow.Workspace.cs, minimal hooks in MainWindow.xaml.cs; Localization/Strings.zh-CN.xaml and en-US.xaml; tests/Ui/WorkspaceUiTests.cs.

- [ ] Add failing tests for original handler/name retention plus theme/details/bulk controls and state-preserving theme toggle.
- [ ] Preserve all existing menus, context menus and the single virtualized production TreeView/ListView. Rearrange into left folder tree, center search/toolbar/list, collapsible right details and status.
- [ ] Add View menu, theme toggle + appearance window (Task 4), detail/bulk controls wired to existing handlers/Can* properties. View-only state does not belong to document history. Use bindings/shared property notifications rather than polling.
- [ ] Add bilingual strings for all new labels; dynamic resources update existing windows without resetting either preference.
- [ ] Keep selected item, scope, tree expansion, scroll, searches and dirty/history through theme changes; retain default 250ms debounce and original Can* restrictions. Do not add automatic sorting.
- [ ] Narrow-width layout collapses details and retains reopen entry. Run original virtualization/search/edit/move/delete/clipboard/localization contracts and focused WPF tests.
- [ ] Commit implementation and tests.

## Task 4: Themed appearance and confirmation dialogs

**Ownership:** main thread or independent worker owning only new dialog files, integrated sequentially.

**Files:** AppearanceDialog.xaml/.cs; AppMessageDialog.xaml/.cs; MainWindow.xaml.cs call sites; theme keys in bilingual resources owned by main.

- [ ] Add result policy tests before implementation: YesNoCancel defaults Cancel, YesNo defaults No, OK returns OK, close/Escape preserve cancellation, display text preserves original error/recovery details.
- [ ] Implement themed modal helper with signature compatible with existing MessageBox.Show call shapes, localized buttons and safe defaults. Replace only UI calls, not guard or transaction logic.
- [ ] Implement Light/Dark radio choices and read/write error notice; preserve any open dialog inputs on palette swaps. Modal views do not reconstruct document.
- [ ] Verify confirmation flows, original safety contracts and theme/language consistency; commit.

## Task 5: Regression, actual WPF rendering and handoff

**Files:** tests/Ui runtime integration/render tests, relevant scripts only if actual markup assumptions changed, README.zh-CN.md / README.md feature notes, approved spec and this plan.

- [ ] Run all original tests; baseline is 447 passed, zero failed (six pre-existing analyzer/nullability warnings).
- [ ] Run all relevant UI/safety verification scripts; retain substantive assertions. Any test adjustment must reflect equivalent functional contracts, never weaken save/root protections.
- [ ] In STA test host instantiate the built MainWindow and own dialogs with the actual application resources; use synthetic fixture, real handlers and both themes/languages. Measure 1180×760 and 900×560 plus equivalent 125/150% constraints and verify reachability/virtualization.
- [ ] Render actual WPF visuals to PNG; smoke-launch local built EXE. If interactive native desktop control is unavailable, clearly distinguish runtime-render evidence from manual clicks in a user desktop.
- [ ] Local Windows single-file publish, verify EXE and hashes; no remote Release or overwrite installed app.
- [ ] Fresh whole-branch review; address actionable findings; rerun affected tests. Commit and hand off branch, local EXE and precise validation limits.

## Execution decisions

The user has explicitly confirmed the complete design and start of implementation, so execute through validation without another round of implementation permission. Independent workers use separate git worktrees; main owns integration and MainWindow. Native managed worktree tool cannot target a newly cloned repository from this projectless current directory, so native git worktrees are used inside the authorized workspace. Git HTTPS uses a per-command OpenSSL backend after sandbox Schannel credential failure; no persistent system/git setting is changed. NuGet restore/test uses approved host execution because sandbox TLS fails.

## Cloud delivery scope correction

The user explicitly required the GitHub cloud repository, including visible changes on the default branch. This supersedes the earlier local-only delivery decision: upload the implementation through a feature branch and PR, run remote CI, then merge to main. Do not create a new tagged Release or replace installed applications. The local worktree remains a build/test workspace only.
