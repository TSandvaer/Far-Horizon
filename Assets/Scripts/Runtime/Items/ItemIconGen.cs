using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Procedural, code-baked slot ICONS for the PoC resource items (ticket 86caa4bya AC5 + the #90 soak
    /// fix for BUG 3 — chopped wood showed only a bare "W" letter-chip because its ItemDef icon was null).
    /// Draws small warm/low-poly glyphs into a runtime Texture2D + wraps each in a Sprite. Until a 3D
    /// render-the-prop IconBaker lands (Uma's gameplay-UI direction), these give every resource a
    /// recognizable read instead of a letter — and the downstream stone/berry tickets inherit a stand-in.
    ///
    /// REPRODUCIBLE-FROM-CODE (unity-conventions.md §recolor-must-be-reproducible): the icon is regenerated
    /// every run from these pixel routines — no hand-edited PNG that silently reverts on re-import. A
    /// caller-supplied baked Sprite (ItemCatalog.BuildDefaults's icon args) always wins over these.
    ///
    /// Runtime-generated textures/sprites work in the shipped IL2CPP player (no AssetDatabase needed). NO
    /// statics held (each call mints a fresh sprite) — the StaticStateResetTests audit stays green.
    /// </summary>
    public static class ItemIconGen
    {
        private const int Size = 64;

        // Warm low-poly palette (art-direction.md: warm/lush, faceted, soft).
        private static readonly Color Bark = new Color(0.52f, 0.36f, 0.21f);   // log side
        private static readonly Color BarkDark = new Color(0.38f, 0.25f, 0.14f);
        private static readonly Color Endgrain = new Color(0.80f, 0.62f, 0.38f); // sawn log end
        private static readonly Color StoneCol = new Color(0.62f, 0.63f, 0.66f);
        private static readonly Color StoneDark = new Color(0.46f, 0.47f, 0.51f);
        private static readonly Color Leaf = new Color(0.36f, 0.55f, 0.27f);
        private static readonly Color Berry = new Color(0.74f, 0.22f, 0.30f);
        private static readonly Color Clear = new Color(0, 0, 0, 0);

        /// <summary>A small bundle of two stacked sawn logs (the chopped-wood icon — BUG 3 #90).</summary>
        public static Sprite WoodBundle()
        {
            var px = NewCanvas();
            // Two horizontal logs, the lower one offset, each with a lighter sawn-end disc on the right.
            DrawLog(px, cy: 26, x0: 8, x1: 50);
            DrawLog(px, cy: 40, x0: 14, x1: 56);
            return ToSprite(px, "icon_wood");
        }

        /// <summary>A small pile of three faceted stones (downstream stone ticket stand-in).</summary>
        public static Sprite StonePile()
        {
            var px = NewCanvas();
            DrawBlob(px, 24, 26, 12, StoneCol, StoneDark);
            DrawBlob(px, 42, 28, 11, StoneDark, StoneCol);
            DrawBlob(px, 33, 40, 14, StoneCol, StoneDark);
            return ToSprite(px, "icon_stone");
        }

        /// <summary>A cluster of red berries on a leaf (downstream berry ticket stand-in).</summary>
        public static Sprite BerryCluster()
        {
            var px = NewCanvas();
            DrawBlob(px, 20, 24, 9, Leaf, Leaf);     // leaf hint
            DrawBlob(px, 26, 38, 8, Berry, BerryDarker());
            DrawBlob(px, 40, 34, 8, Berry, BerryDarker());
            DrawBlob(px, 34, 46, 8, Berry, BerryDarker());
            return ToSprite(px, "icon_berry");
        }

        private static Color BerryDarker() => new Color(0.55f, 0.14f, 0.22f);

        // ---- pixel helpers ----

        private static Color[] NewCanvas()
        {
            var px = new Color[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            return px;
        }

        // A capsule-ish log: a rounded horizontal bar (bark) with a lighter sawn-end disc at the right end.
        private static void DrawLog(Color[] px, int cy, int x0, int x1)
        {
            int half = 8;
            for (int x = x0; x <= x1; x++)
            for (int y = cy - half; y <= cy + half; y++)
            {
                if (!In(x, y)) continue;
                // rounded ends
                int dxEnd = x < x0 + half ? (x0 + half) - x : (x > x1 - half ? x - (x1 - half) : 0);
                int dy = y - cy;
                if (dxEnd * dxEnd + dy * dy > half * half && dxEnd > 0) continue;
                bool top = dy < -2;
                px[y * Size + x] = top ? BarkDark : Bark;
            }
            // sawn end disc near the right cap
            int ex = x1 - half + 1, ey = cy;
            DrawBlob(px, ex, ey, 6, Endgrain, BarkDark);
        }

        // A filled faceted blob (low-poly stone/berry): a disc with a darker lower-right shade.
        private static void DrawBlob(Color[] px, int cx, int cy, int r, Color top, Color bottom)
        {
            for (int x = cx - r; x <= cx + r; x++)
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (!In(x, y)) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > r * r) continue;
                px[y * Size + x] = (dx + (cy - y)) > 0 ? bottom : top;
            }
        }

        private static bool In(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

        private static Sprite ToSprite(Color[] px, string name)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(px);
            tex.Apply(false, false);
            var sp = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sp.name = name;
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }
    }
}
