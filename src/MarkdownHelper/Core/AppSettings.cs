using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace MarkdownHelper.Core
{
    public enum NotifyMode
    {
        Popup,
        TrayOnly,
        OpenPreview,
        ShortcutOnly
    }

    public enum EditorMode
    {
        Edit,
        RemovePieces
    }

    [DataContract]
    public sealed class AppSettings
    {
        public const int MinPopupSeconds = 2;
        public const int MaxPopupSeconds = 30;
        public const string DefaultHotkey = "Ctrl+Alt+M";

        [DataMember] public NotifyMode NotifyMode { get; set; } = NotifyMode.Popup;
        [DataMember] public int PopupSeconds { get; set; } = 6;
        [DataMember] public string Hotkey { get; set; } = DefaultHotkey;
        [DataMember(Name = "IgnoredAppList")] public List<IgnoredApp> IgnoredApps { get; set; } = new List<IgnoredApp>();
        [DataMember] public bool StartWithWindows { get; set; } = true;
        [DataMember] public bool ShowMarkdown { get; set; }
        [DataMember] public EditorMode LastMode { get; set; } = EditorMode.Edit;

        /// <summary>Repairs values a hand-edited or older settings file may have left out of range.</summary>
        public void Normalize()
        {
            if (!Enum.IsDefined(typeof(NotifyMode), NotifyMode)) NotifyMode = NotifyMode.Popup;
            if (!Enum.IsDefined(typeof(EditorMode), LastMode)) LastMode = EditorMode.Edit;
            PopupSeconds = Math.Max(MinPopupSeconds, Math.Min(MaxPopupSeconds, PopupSeconds));
            if (!HotkeyGesture.TryParse(Hotkey, out _)) Hotkey = DefaultHotkey;
            if (IgnoredApps == null) IgnoredApps = new List<IgnoredApp>();
            IgnoredApps = IgnoredApps
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.ProcessName))
                .GroupBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        [OnDeserializing]
        private void SetDefaults(StreamingContext context)
        {
            NotifyMode = NotifyMode.Popup;
            PopupSeconds = 6;
            Hotkey = DefaultHotkey;
            IgnoredApps = new List<IgnoredApp>();
            StartWithWindows = true;
            LastMode = EditorMode.Edit;
        }
    }
}
