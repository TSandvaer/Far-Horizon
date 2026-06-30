using UnityEngine;
using UnityEngine.UIElements;

namespace FarHorizon.Settings
{
    /// <summary>
    /// The dev-console PANEL POSITION (ticket 86cabeqj9 AC4). The #83 panel was screen-CENTERED, which
    /// covers the player while the Sponsor tweaks WHILE he plays (AC2). This lets him park the console in a
    /// CORNER so it never obscures gameplay; the chosen corner PERSISTS across runs (PlayerPrefs, alongside
    /// the dialed values). DEFAULT = top-left — a corner that does NOT cover the player (never center).
    ///
    /// Pure C# (the enum + load/save) so the persistence contract is EditMode-testable with no render; the
    /// <see cref="Apply"/> helper positions a VisualElement (the panel) into the chosen corner of its parent
    /// (the full-screen scrim) by setting the scrim's flex justify/align — exercised by the panel + the
    /// shipped-build capture, not the headless test (UI Toolkit layout is unreliable in EditMode).
    /// </summary>
    public enum ConsoleCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    public static class ConsolePosition
    {
        /// <summary>The PlayerPrefs key the chosen corner persists under (AC4 — alongside the dialed values).
        /// Double-underscored so it never collides with a SettingEntry id-derived key (fh.settings.&lt;id&gt;).</summary>
        public const string PrefsKey = "fh.settings.__console_corner";

        /// <summary>The default corner — top-left, OFF the player (AC4: never screen-center).</summary>
        public const ConsoleCorner Default = ConsoleCorner.TopLeft;

        /// <summary>Load the persisted corner (AC4). Returns <see cref="Default"/> when unset or out of range.</summary>
        public static ConsoleCorner Load()
        {
            if (!PlayerPrefs.HasKey(PrefsKey)) return Default;
            int v = PlayerPrefs.GetInt(PrefsKey);
            if (v < 0 || v > (int)ConsoleCorner.BottomRight) return Default;
            return (ConsoleCorner)v;
        }

        /// <summary>Persist the chosen corner (AC4 — survives a relaunch).</summary>
        public static void Save(ConsoleCorner corner)
        {
            PlayerPrefs.SetInt(PrefsKey, (int)corner);
            PlayerPrefs.Save();
        }

        /// <summary>Cycle to the next corner (TopLeft → TopRight → BottomLeft → BottomRight → TopLeft).</summary>
        public static ConsoleCorner Next(ConsoleCorner corner)
            => (ConsoleCorner)(((int)corner + 1) % 4);

        /// <summary>The flex justify-content (vertical axis) that parks a column-laid scrim's child at this corner's row.</summary>
        public static Justify JustifyFor(ConsoleCorner corner)
            => (corner == ConsoleCorner.TopLeft || corner == ConsoleCorner.TopRight)
                ? Justify.FlexStart : Justify.FlexEnd;

        /// <summary>The flex align-items (horizontal axis) that parks a column-laid scrim's child at this corner's side.</summary>
        public static Align AlignFor(ConsoleCorner corner)
            => (corner == ConsoleCorner.TopLeft || corner == ConsoleCorner.BottomLeft)
                ? Align.FlexStart : Align.FlexEnd;

        /// <summary>
        /// Position the panel into the chosen corner of its parent scrim (AC4). The scrim is the full-screen
        /// flex container; setting its justify-content + align-items parks the single panel child into the
        /// corner. A small inset keeps the panel off the very edge. Null-safe (no-op on a null scrim).
        /// </summary>
        public static void Apply(VisualElement scrim, ConsoleCorner corner)
        {
            if (scrim == null) return;
            scrim.style.justifyContent = JustifyFor(corner);
            scrim.style.alignItems = AlignFor(corner);
        }

        /// <summary>A short human label for the corner (shown on the picker button — "TL" / "TR" / "BL" / "BR").</summary>
        public static string ShortLabel(ConsoleCorner corner)
        {
            switch (corner)
            {
                case ConsoleCorner.TopLeft:     return "TL";
                case ConsoleCorner.TopRight:    return "TR";
                case ConsoleCorner.BottomLeft:  return "BL";
                default:                        return "BR";
            }
        }
    }
}
