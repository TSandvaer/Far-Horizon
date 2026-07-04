using NUnit.Framework;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cahnmjv soak-239-v2 FOUNDATION-GAP regression guard: the grip curl must engage whenever a weapon is
    /// actually SHOWN in the hand — NOT only when a held-visual weapon is the selected belt item.
    ///
    /// THE BUG THIS PINS: the Sponsor reported "the finger does not wrap around the handle" while holding the
    /// SWORD via the [B] debug cycle. The sword + knife have NO belt items (they are [B]-only look-soak
    /// weapons), and the debug cycle can only SHOW them with no axe/spear selected — so the old
    /// belt-selection-only gate (IsAxeSelectedInBelt || IsSpearSelectedInBelt) was FALSE while the sword was in
    /// hand → the curl never ran → the hand stayed open (thumb straight out). <see cref="CastawayFingerCurl.
    /// ShouldGrip"/> now ORs in the debug-view flag; this test pins that contract so a re-narrowing to
    /// belt-selection-only (the exact regression) reds HEADLESSLY, with no scene/build. Pairs with the
    /// PlayMode integration test (CastawayFingerCurlPlayModeTests) that drives the REAL debug-cycle seam.
    /// </summary>
    public class CastawayFingerCurlGateTests
    {
        [Test]
        public void ShouldGrip_ClosedHandOnlyWhenAWeaponIsShown()
        {
            // Empty-handed, no debug view, no override → OPEN hand (the natural clip pose must stand).
            Assert.IsFalse(CastawayFingerCurl.ShouldGrip(alwaysCurl: false, selectionGrip: false, debugWeaponShown: false),
                "empty-handed with no debug view: the curl must NOT fire (open clip pose stands)");

            // A SELECTED belt weapon (axe/spear) → grip. (The pre-existing behaviour, still covered.)
            Assert.IsTrue(CastawayFingerCurl.ShouldGrip(false, selectionGrip: true, debugWeaponShown: false),
                "a selected belt weapon (axe/spear) must grip");

            // THE FOUNDATION-GAP GUARD: a [B] debug-view weapon (sword/knife — no belt item) → grip, even
            // though NO belt weapon is selected. A gate that ignores the debug view reds HERE.
            Assert.IsTrue(CastawayFingerCurl.ShouldGrip(false, selectionGrip: false, debugWeaponShown: true),
                "a [B] debug-view weapon (the sword the Sponsor judged) must grip — belt selection is FALSE here, " +
                "so a belt-selection-only gate would leave the hand open (the soak-239-v2 defect)");

            // Both true → grip. The diagnostic alwaysCurl override → grip regardless.
            Assert.IsTrue(CastawayFingerCurl.ShouldGrip(false, true, true), "selection + debug view both shown → grip");
            Assert.IsTrue(CastawayFingerCurl.ShouldGrip(alwaysCurl: true, selectionGrip: false, debugWeaponShown: false),
                "the alwaysCurl diagnostic override forces a grip");
        }
    }
}
