# Markdown Helper design

Date: 2026-11-05

## Why this exists

AI tools produce markdown. Pasting it into Teams or Discord loses or mangles the
formatting. The current workaround is pasting into a new VS Code file and copying
out of a preview, which takes too many steps and sometimes renders wrongly. The
worst failure: the preview treats dollar signs as math, so any text with two
prices in it turns into garbled equation text with stray `**` markers.

Markdown Helper is a small Windows tray tool. It notices markdown on the
clipboard, lets you check and trim it, and puts a version on the clipboard that
pastes correctly in both Teams and Discord.

## Goals

- One copy works in both destinations. The clipboard carries HTML (which Teams
  uses) and plain text (which Discord uses, and renders as markdown itself).
- A dollar sign is always a dollar sign. There is no math support of any kind.
- See the formatted result before copying, and delete what should not be pasted.
- Stay out of the way: never take focus without being asked.
- Everything runs on the PC. Nothing is sent anywhere, and clipboard text is
  never written to disk.

## Non-goals

- Automatic formatting with no review. It was considered and rejected: it skips
  review and silently replaces clipboard content.
- Rich formatting for social sites that accept only plain text.
- Sync, accounts, cloud features, telemetry or auto-update.
- Splitting long text into several Discord messages. The tool only warns.

## Anti-goals

- Interrupting typing, stealing focus, or firing on ordinary copied text.
- Requiring anything to be installed besides what Windows ships with.

## Constraints

- Windows only. Native, with no third-party runtime dependencies.
- C# and WPF on .NET Framework 4.8, which ships with Windows 10 (1903 and later)
  and Windows 11. The result is one `.exe`. (Claude's recommendation, accepted.)
- The markdown parser is written here instead of using a library, per the
  "avoid dependencies" request. Test-only packages are allowed.
- Public repository, GitHub Actions CI, and a published release build. Sample
  text in the repo must be generic, with no content from real work.

## Behavior

### Getting attention

The tool lives in the tray and starts with Windows. A setting picks what happens
when markdown is copied:

1. **Show a small popup** (default). Appears bottom-right above the tray with
   Preview and Copy formatted buttons, fades after a configurable time (default 6
   seconds), pauses while hovered, and never takes focus.
2. **Only mark the tray icon.** A dot appears on the icon. Clicking opens the
   preview.
3. **Open the preview window.** Goes straight to the window.
4. **Shortcut only.** The tool stops watching the clipboard.

A global shortcut (default Ctrl+Alt+M, changeable) opens the preview with the
current clipboard text at any time, whatever the setting.

The tray menu has Open preview, Settings and Quit.

### What counts as markdown

- At least two different clear signs, such as a heading plus bold text.
- A table or a fenced code block counts as two on its own.
- One stray asterisk does not count. Source code such as `**kwargs` or `# comment`
  must not trigger it.
- The same text does not trigger twice in a row.
- Copies from apps on the ignore list are skipped (defaults: VS Code, terminals).
- Clipboard content marked private by password managers is skipped.
- Copies the tool makes itself are skipped.

### Preview window

- **Edit mode:** the formatted text works like a document. Click in and change it.
- **Remove pieces mode:** hovering highlights a heading, paragraph, list item,
  table, quote or code block with a Remove button. One click drops it.
- **Markdown switch:** independently shows or hides the source beside the
  formatted text in either mode. Edits on either side appear on the other.
- **Undo** works across both modes and the source pane.
- **Status line:** character count of the Discord version, with a warning over
  Discord's 2,000-character limit.
- **Copy** puts both versions on the clipboard and leaves the window open.
- The window opens in whichever mode was last used.

### Output

- **HTML (Teams):** semantic HTML using the Windows `HTML Format` clipboard
  format, UTF-8 encoded.
- **Plain text (Discord):** the markdown with Discord's gaps filled. Tables become
  aligned text in a code block, headings deeper than three levels become bold,
  horizontal rules become a line of characters, and task lists use checkbox
  characters.

### Markdown coverage

Headings, paragraphs, bold, italic, strikethrough, inline code, fenced and
indented code, nested lists, ordered lists, task lists, quotes, tables with
alignment, links, autolinks, images (shown as links), horizontal rules, hard line
breaks and backslash escapes. Raw HTML is shown as literal text, never interpreted.

### Appearance

Windows 11 styling per the approved mockup. Light and dark themes follow Windows
automatically and switch live, with no theme setting. High contrast mode uses
system colors. Mica backdrop where Windows supports it, solid colors elsewhere.

## Calls made by Claude, not the user

- Default shortcut Ctrl+Alt+M, default popup time of 6 seconds.
- MIT license.
- Fixed Windows-blue accent from the mockup, not the system accent color.
- The preview shows the rich version only. There is no separate Discord preview.
- Editing in formatted mode rewrites the markdown in a normalized style (for
  example `_x_` becomes `*x*`). Meaning is preserved; spelling of the markup is not.
- Starting with Windows is on by default, as shown in the mockup.

## Left to the builder

File layout, class design, the test framework, and the CI workflow details.
