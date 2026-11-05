# Markdown Helper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows tray tool that turns clipboard markdown into output that pastes correctly in Teams and Discord.

**Architecture:** One WPF executable. `Core/` holds pure logic (parser, renderers, detector, settings) with no UI types. `Windows/` wraps Win32 (clipboard, hotkey, tray, startup). `UI/` holds windows, themes and the FlowDocument bridge. Markdown text is the single source of truth; every view is derived from it.

**Tech Stack:** C#, WPF, .NET Framework 4.8, xUnit (tests only), GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-05-markdown-helper-design.md`

## Global Constraints

- Target `net48`. No runtime NuGet packages. Output is one `.exe`.
- No math parsing. `$` is ordinary text everywhere.
- Never write clipboard text to disk. No network calls.
- Popup never takes focus. Default popup time 6 seconds. Default shortcut Ctrl+Alt+M.
- Discord limit 2,000 characters. The tool warns and never splits.
- Repo content is public: sample text must be generic.

## Review Focus

- Text with several prices: `$` pairs must render literally, with bold intact.
- Non-ASCII text (`§`, `×`, emoji) must survive the `HTML Format` byte offsets.
- The clipboard being locked by another app must not crash the tool.
- Source code on the clipboard (Python `**kwargs`, `# comment`) must not fire the popup.
- A formatted-mode edit must not lose links, tables or code blocks elsewhere in the document.

---

### Task 1: Project skeleton

**Files:** `MarkdownHelper.sln`, `src/MarkdownHelper/MarkdownHelper.csproj`, `tests/MarkdownHelper.Tests/MarkdownHelper.Tests.csproj`, `.gitignore`, `LICENSE`

- [ ] Create a WPF `net48` exe project and an xUnit test project that references it.
- [ ] Run `dotnet build` and `dotnet test`. Expected: both succeed with one placeholder test.
- [ ] Commit.

### Task 2: Markdown parser

**Files:** `src/MarkdownHelper/Core/MarkdownNodes.cs`, `Core/MarkdownParser.cs`, `Core/InlineParser.cs`, `tests/.../ParserTests.cs`

**Produces:** `MarkdownParser.Parse(string) -> MarkdownDocument`. Blocks carry `StartLine`/`EndLine` source spans. List items are blocks with their own spans.

- [ ] Write tests for every construct in the spec's coverage list, plus `**$14 to $0**` staying bold with literal dollars.
- [ ] Implement until green. Emphasis uses the CommonMark delimiter-run rules.
- [ ] Commit.

### Task 3: Output renderers

**Files:** `Core/HtmlRenderer.cs`, `Core/DiscordRenderer.cs`, `Core/MarkdownWriter.cs`, `Core/ClipboardHtml.cs`, tests for each

**Produces:** `HtmlRenderer.Render(doc) -> string`, `DiscordRenderer.Render(doc) -> string`, `MarkdownWriter.Write(doc) -> string`, `ClipboardHtml.Encode(fragment) -> byte[]`.

- [ ] Test HTML output, including raw HTML being escaped.
- [ ] Test the Discord table conversion, heading depth, rules and task lists.
- [ ] Test `ClipboardHtml` offsets by slicing the bytes with `StartFragment`/`EndFragment` and comparing to the UTF-8 fragment, with non-ASCII input.
- [ ] Test `Parse(Write(doc))` equals `doc` across a document covering all constructs.
- [ ] Implement until green. Commit.

### Task 4: Detector and settings

**Files:** `Core/MarkdownDetector.cs`, `Core/AppSettings.cs`, `Core/SettingsStore.cs`, tests

**Produces:** `MarkdownDetector.IsMarkdown(string) -> bool`; `AppSettings` (`NotifyMode`, `PopupSeconds`, `Hotkey`, `IgnoredApps`, `StartWithWindows`, `ShowMarkdown`, `LastMode`); `SettingsStore.Load/Save`.

- [ ] Test the two-signs rule, table and fence counting double, and the false positives from Review Focus.
- [ ] Test settings round trip, missing file and corrupt file falling back to defaults.
- [ ] Implement until green. Commit.

### Task 5: FlowDocument bridge

**Files:** `UI/FlowDocumentBuilder.cs`, `UI/FlowDocumentReader.cs`, tests run on an STA thread

**Produces:** `FlowDocumentBuilder.Build(doc, theme) -> FlowDocument`, `FlowDocumentReader.Read(FlowDocument) -> string`.

- [ ] Test that build-then-read keeps every construct's meaning, including links, tables and code blocks.
- [ ] Test that literal `*` and `#` typed by a user survive as escaped text.
- [ ] Implement until green. Commit.

### Task 6: Windows integration

**Files:** `Windows/NativeMethods.cs`, `ClipboardWatcher.cs`, `ClipboardWriter.cs`, `GlobalHotkey.cs`, `TrayIcon.cs`, `StartupRegistration.cs`, `WindowEffects.cs`, `ThemeWatcher.cs`

- [ ] Watcher uses `AddClipboardFormatListener`, retries a locked clipboard, and skips private formats, ignored apps, repeats and the tool's own writes.
- [ ] Writer sets `HTML Format` and `CF_UNICODETEXT` through Win32 so bytes are exact.
- [ ] Add a clipboard round-trip test gated on `MDH_CLIPBOARD_TESTS=1`, so a developer's clipboard is left alone.
- [ ] Commit.

### Task 7: Windows and theming

**Files:** `App.xaml`, `UI/Theme.cs`, `UI/Controls.xaml`, `UI/PreviewWindow.xaml`, `UI/PopupWindow.xaml`, `UI/SettingsWindow.xaml`, `AppController.cs`, `assets/app.ico`, `app.manifest`

- [ ] Build each window to match the approved mockup in light, dark and high contrast.
- [ ] Single instance via a named mutex; a second launch opens the preview in the first.
- [ ] Commit.

### Task 8: Verify, CI and release

**Files:** `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `README.md`

- [ ] Run the real app and check each behavior in the spec.
- [ ] CI builds and tests on push and pull request. Release builds on a `v*` tag and uploads the exe with its SHA-256.
- [ ] Push, get CI green, tag and publish.
