# Feature Implementation Matrix

## Implemented Features
| Feature | Status (%) | Notes | Dependencies |
| --- | --- | --- | --- |
| Note lifecycle (create, open, position, soft-delete) | 95% | Notes load from SQLite, create default if absent, clamp to screen bounds, and confirm destructive closes with persisted preference. | `App` window orchestration, `NoteWindowViewModel`, and `NotesRepository` persistence【F:src/StickyNotesClassic.App/App.axaml.cs†L68-L240】【F:src/StickyNotesClassic.App/ViewModels/NoteWindowViewModel.cs†L250-L365】【F:src/StickyNotesClassic.Core/Repositories/NotesRepository.cs†L20-L205】 |
| Autosave and bounds tracking | 95% | Debounced content saves and throttled bounds saves with background queue; flushes on shutdown. | `AutosaveService` coupled to repository【F:src/StickyNotesClassic.Core/Services/AutosaveService.cs†L13-L123】【F:src/StickyNotesClassic.App/App.axaml.cs†L102-L210】 |
| Theming and default color propagation | 80% | Theme lookups and cross-window default color updates are in place; per-platform window chrome adjustments for macOS/Linux. | `ThemeService`, note windows, and platform-specific chrome settings【F:src/StickyNotesClassic.App/Views/NoteWindow.axaml.cs†L18-L71】【F:src/StickyNotesClassic.App/App.axaml.cs†L117-L157】 |
| Settings management | 90% | Settings load/save via SQLite, hotkey inputs gated by runtime support, and backup hints resolved; apply-to-all-font command implemented. | `SettingsViewModel` and `NotesRepository` settings serialization【F:src/StickyNotesClassic.App/ViewModels/SettingsViewModel.cs†L23-L338】【F:src/StickyNotesClassic.Core/Repositories/NotesRepository.cs†L120-L205】 |
| Backup/export/import | 90% | JSON export/import with checksum envelopes, RTF validation, cancellation, and detailed counts plus log directory hinting. | `BackupService` using `AppPathHelper` and repository【F:src/StickyNotesClassic.App/Services/BackupService.cs†L15-L211】【F:src/StickyNotesClassic.Core/Utilities/AppPathHelper.cs†L10-L88】 |
| Platform-aware storage paths | 100% | Application data, database, backups, and logs resolved per-OS with fallbacks and directory creation. | `AppPathHelper` used by DB, backups, logging【F:src/StickyNotesClassic.Core/Utilities/AppPathHelper.cs†L10-L88】【F:src/StickyNotesClassic.Core/Data/NotesDbContext.cs†L16-L40】 |
| Cross-platform CI | 90% | GitHub Actions matrix builds/tests Windows, macOS, and Linux; UI automation still pending. | `.github/workflows/ci.yml`【F:.github/workflows/ci.yml†L1-L41】 |

## Partially Implemented Features
| Feature | Status (%) | Gaps/Blockers | Dependencies |
| --- | --- | --- | --- |
| Global hotkey to create/show notes | 70% | Windows hook implemented; Linux X11 registrar available when DISPLAY is present; macOS pending CoreGraphics tap. | `HotkeyService` and platform registrars【F:src/StickyNotesClassic.App/Services/HotkeyService.cs†L8-L171】【F:src/StickyNotesClassic.App/Services/Hotkeys/LinuxHotkeyRegistrar.cs†L1-L196】【F:src/StickyNotesClassic.App/Services/Hotkeys/WindowsHotkeyRegistrar.cs†L1-L220】 |
| Rich text editing | 20% | Validation supports RTF blobs and backups enforce RTF integrity, but the editor is still plain text. | `ValidationService` and backup import validation【F:src/StickyNotesClassic.Core/Services/ValidationService.cs†L10-L58】【F:src/StickyNotesClassic.App/Services/BackupService.cs†L69-L211】 |
| Window chrome/resize QA | 30% | Platform-specific chrome choices exist, but no automated resize hit-test coverage or debug overlay. | Note window chrome settings【F:src/StickyNotesClassic.App/App.axaml.cs†L117-L157】【F:src/StickyNotesClassic.App/Views/NoteWindow.axaml.cs†L18-L71】 |
| Backup restore UX and retention | 40% | Backups use checksums and retention cleanup, but no restore picker UI or configurable retention settings. | `BackupService` scheduling and envelope validation【F:src/StickyNotesClassic.App/Services/BackupService.cs†L15-L211】 |
| UI automation/perf coverage | 10% | Unit tests run in CI, but no UI smoke or performance tests are present. | `.github/workflows/ci.yml` matrix placeholder for future suites【F:.github/workflows/ci.yml†L1-L41】 |

## Planned or Unimplemented Items
- **Formatting toolbar and RTF serialization**: required to unlock rich text editing and migrate existing notes.【F:src/StickyNotesClassic.Core/Services/ValidationService.cs†L10-L58】
- **macOS hotkey registrar**: still required for parity; settings show platform reason while Linux now hooks X11 when available.【F:src/StickyNotesClassic.App/Services/HotkeyService.cs†L94-L144】【F:src/StickyNotesClassic.App/Services/Hotkeys/LinuxHotkeyRegistrar.cs†L1-L196】
- **Chrome hit-test automation**: add UITests to assert resize handles on macOS/Linux.【F:src/StickyNotesClassic.App/App.axaml.cs†L117-L157】
- **Backup restore picker and retention controls**: expose backup selection UI and user-configurable limits.【F:src/StickyNotesClassic.App/Services/BackupService.cs†L15-L211】

## Technical Debt Affecting Completion
- **Limited macOS hotkey coverage**: Windows and Linux (X11) registrars exist; macOS remains a placeholder until CoreGraphics hooks are implemented.【F:src/StickyNotesClassic.App/Services/Hotkeys/LinuxHotkeyRegistrar.cs†L1-L196】【F:src/StickyNotesClassic.App/Services/Hotkeys/WindowsHotkeyRegistrar.cs†L1-L220】【F:src/StickyNotesClassic.App/Services/Hotkeys/MacHotkeyRegistrar.cs†L1-L40】
- **Lack of UI/perf integration tests**: CI runs unit tests only, so resize/hotkey/backup UX regressions can slip through.【F:.github/workflows/ci.yml†L1-L41】
- **Plain-text editor**: without a rich text surface, formatting-related bugs cannot be validated or fixed.【F:src/StickyNotesClassic.Core/Services/ValidationService.cs†L10-L58】

## Feature Flags and Deprecations
- **Platform gating**: Hotkeys advertise support based on registrar capability, disabling inputs on unsupported OSes.【F:src/StickyNotesClassic.App/Services/HotkeyService.cs†L94-L144】【F:src/StickyNotesClassic.App/ViewModels/SettingsViewModel.cs†L67-L144】
- **Backup retention toggle**: `AutoBackupEnabled` controls timer scheduling; retention pruning runs when enabled.【F:src/StickyNotesClassic.App/App.axaml.cs†L81-L106】
- **Visual toggles**: Gradient/shadow/gloss settings act as soft feature flags for theming options per note windows.【F:src/StickyNotesClassic.App/ViewModels/NoteWindowViewModel.cs†L20-L120】
