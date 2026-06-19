namespace FarHorizon
{
    /// <summary>Which slot array a <see cref="SlotRef"/> addresses (item-model contract §4).</summary>
    public enum SlotArea
    {
        Inventory,
        Belt,
    }

    /// <summary>
    /// Addresses a slot across BOTH arrays (item-model contract §4) — the area (inventory grid vs belt
    /// hotbar) + the index within it. Drag/drop source+target use this; <see cref="InventoryModel.TryMove"/>
    /// consumes a from/to pair.
    /// </summary>
    public readonly struct SlotRef
    {
        /// <summary>Inventory grid or belt hotbar.</summary>
        public readonly SlotArea Area;

        /// <summary>0-based index within the area's slot array.</summary>
        public readonly int Index;

        public SlotRef(SlotArea area, int index)
        {
            Area = area;
            Index = index;
        }

        public static SlotRef Inventory(int index) => new SlotRef(SlotArea.Inventory, index);
        public static SlotRef Belt(int index) => new SlotRef(SlotArea.Belt, index);

        public override string ToString() => $"{Area}[{Index}]";
    }
}
