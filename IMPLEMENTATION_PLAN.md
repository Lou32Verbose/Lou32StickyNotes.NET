# Cross-Platform Completion Plan

This plan tracks the remaining feature and platform gaps after the latest implementation passes. Completed items are recorded for context; open items are ordered by priority.

## Completed phases
- Introduced an `IHotkeyRegistrar` abstraction with a Windows hook implementation and safe fallback to avoid crashes on macOS/Linux while enabling hotkey-driven note creation on Windows.
- Added cross-platform confirmation dialogs for destructive note closes with persisted "don't ask again" preference.
- Implemented the "apply font to all notes" pipeline that validates fonts, rewrites saved note fonts, and notifies open windows.
- Surfaced detailed backup import/export results with cancellation, validation of RTF payloads, and log directory discovery.
- Switched SQLite access to pooled, per-operation connections with write synchronization to improve UI responsiveness across platforms.
- Wrapped backups in checksum-verified envelopes with import rejection on tamper or validation failure.
- Added a cross-platform GitHub Actions workflow to build and test on Windows, macOS, and Linux.
- Added X11-backed hotkey registration on Linux with platform-aware status messaging in settings.

## Remaining backlog

### 1) Cross-platform hotkeys (macOS)
*Problem*: macOS still lacks a CoreGraphics-backed registrar; Linux support now exists only when X11 is available.

**Roadmap**
1. Implement macOS registrar using `CGEventTapCreate`, with accessibility permission prompts and modifier translation.
2. Add functional tests on macOS runners that verify registration succeeds and gracefully downgrades when permissions are missing.
3. Extend settings messaging to reflect macOS-specific permission requirements once the registrar is active.

### 2) Rich text editing parity
*Problem*: Notes still use plain text bindings; validation and backup paths already support RTF.

**Roadmap**
1. Swap note editor to an Avalonia rich text surface that stores RTF in `_contentRtf` while keeping `_contentText` for search.
2. Add serialization/deserialization for RTF in the repository and migrate existing plain-text rows (wrap in minimal RTF header).
3. Provide formatting toolbar/shortcuts (bold/italic/underline) with Command/Ctrl key differences per OS.
4. Add migration tests ensuring legacy notes remain readable and round-trip through backups.

**Sketch**
```csharp
var rtf = _richTextSerializer.ToRtf(editor.Document);
await _repository.UpdateContentAsync(_note.Id, rtf, plainText: editor.Text);
```

### 3) Window chrome and resize QA
*Problem*: Platform-specific chrome choices lack regression coverage, risking macOS/Linux resize bugs.

**Roadmap**
1. Gate chrome configuration behind a platform service so tests can assert applied decorations per OS.
2. Add UITest/Playwright harness that verifies resize hit-testing regions on Windows/macOS/Linux.
3. Add optional debug overlay (toggle in settings or environment variable) to visualize hit-test borders during QA runs.

**Sketch**
```csharp
_windowChrome.Apply(new ChromeOptions
{
    UseNativeTitleBar = _platform.IsMacOS,
    BorderThickness = _platform.IsLinux ? 1 : 0
});
```

### 4) Restore UX and backup retention
*Problem*: Backup envelopes are validated, but restore selection and retention controls are minimal.

**Roadmap**
1. Add a restore picker in settings that enumerates dated backups from `AppPathHelper.GetBackupsDirectory()` with checksum status.
2. Expose retention configuration (count/days) with sensible defaults per platform and enforce before creating new backups.
3. Add tests covering retention trimming and checksum-aware restore selection.

**Sketch**
```csharp
var backups = await _backupService.ListBackupsAsync();
var chosen = await _dialogService.ShowPickerAsync(backups);
await _backupService.ImportAsync(chosen.Path, ct);
```

### 5) Cross-platform UI automation and perf coverage
*Problem*: CI runs unit tests only; no UI/performance coverage for autosave/import or chrome behavior.

**Roadmap**
1. Add headless UI smoke tests (per OS) to open a note, type, resize, and trigger close confirmation.
2. Add perf tests for autosave under typing load and bulk import/export to guard against regressions in the pooled DB pipeline.
3. Wire these suites into the existing GitHub Actions matrix with OS-specific skips when dependencies are missing.

**Sketch**
```yaml
- name: Run UI smoke tests
  run: dotnet test --filter "Category=UISmoke"
```
