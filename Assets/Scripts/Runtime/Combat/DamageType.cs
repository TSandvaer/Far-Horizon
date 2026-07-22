namespace FarHorizon.Combat
{
    /// <summary>
    /// The damage-TYPE discriminator (Combat POC 86cah7xxp, AC4/AC8). Every hit carries ONE of these; a
    /// target's <see cref="ResistanceProfile"/> maps a type to a resist/weak/neutral multiplier ("pierce
    /// beats X" real — AC8a). An ENUM (not a bool/string) so the type is a single stable vocabulary the
    /// player weapon lane (axe = <see cref="Slash"/>, spear = <see cref="Pierce"/>) AND the enemy lane
    /// (snake 86caaz4vn) both bind to — per the parallel-shared-concept naming discipline: this POC LANDS
    /// this enum + <see cref="Health"/> FIRST and OWNS the vocabulary; the snake POC references these names.
    ///
    /// Appended-only ordering (Blunt LAST) so the serialized int of Slash=0 / Pierce=1 never shifts when a
    /// fourth type (fire/etc.) is added later — mirrors the ItemKind append discipline.
    /// </summary>
    public enum DamageType
    {
        Slash,   // axe — a cutting swing (medium reach, hits harder up close)
        Pierce,  // spear — a thrust (long reach); "pierce beats X" is the AC8a matchup
        Blunt,   // reserved (club/rock); no weapon ships it in the POC, but the seam is data-driven
    }
}
