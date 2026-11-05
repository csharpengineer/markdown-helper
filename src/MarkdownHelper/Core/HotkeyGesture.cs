using System;
using System.Collections.Generic;
using System.Linq;

namespace MarkdownHelper.Core
{
    /// <summary>A global shortcut such as Ctrl+Alt+M. Needs at least one modifier and one key.</summary>
    public sealed class HotkeyGesture
    {
        public HotkeyGesture(bool ctrl, bool alt, bool shift, bool win, string key)
        {
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
            Win = win;
            Key = key;
        }

        public bool Ctrl { get; }
        public bool Alt { get; }
        public bool Shift { get; }
        public bool Win { get; }
        public string Key { get; }

        public IEnumerable<string> Parts()
        {
            if (Ctrl) yield return "Ctrl";
            if (Alt) yield return "Alt";
            if (Shift) yield return "Shift";
            if (Win) yield return "Win";
            yield return Key;
        }

        public override string ToString() => string.Join("+", Parts());

        public static bool TryParse(string text, out HotkeyGesture gesture)
        {
            gesture = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (parts.Count < 2) return false;

            bool ctrl = false, alt = false, shift = false, win = false;
            foreach (var part in parts.Take(parts.Count - 1))
            {
                switch (part.ToLowerInvariant())
                {
                    case "ctrl": case "control": ctrl = true; break;
                    case "alt": alt = true; break;
                    case "shift": shift = true; break;
                    case "win": case "windows": win = true; break;
                    default: return false;
                }
            }

            string key = parts[parts.Count - 1];
            bool isModifier = new[] { "ctrl", "control", "alt", "shift", "win", "windows" }.Contains(key.ToLowerInvariant());
            if (isModifier) return false;

            gesture = new HotkeyGesture(ctrl, alt, shift, win, key.Length == 1 ? key.ToUpperInvariant() : key);
            return true;
        }
    }
}
