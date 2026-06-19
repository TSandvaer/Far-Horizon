namespace FarHorizon
{
    /// <summary>
    /// What occupies a single slot (ticket 86caa4bya / item-model contract §4). A value-type struct: the
    /// def the slot holds + the count in it. <c>Def == null</c> is the canonical empty test
    /// (<see cref="IsEmpty"/>).
    ///
    /// Invariants the model maintains: a Tool stack is always Count==1 (tools never stack, contract §2);
    /// a Resource stack is 1..Def.MaxStack (overflow spills to the next free slot — AC7); Count==0 is
    /// only transient before the slot clears back to <see cref="Empty"/>.
    /// </summary>
    public struct ItemStack
    {
        /// <summary>The item this slot holds; null == empty slot.</summary>
        public ItemDef Def;

        /// <summary>How many are in this slot; 1..Def.MaxStack (0 only transiently before clearing to empty).</summary>
        public int Count;

        /// <summary>The canonical empty stack: { Def = null, Count = 0 }.</summary>
        public static readonly ItemStack Empty = new ItemStack { Def = null, Count = 0 };

        /// <summary>True when this slot holds nothing (Def == null) — the canonical empty test.</summary>
        public bool IsEmpty => Def == null;

        public ItemStack(ItemDef def, int count)
        {
            Def = def;
            Count = count;
        }
    }
}
