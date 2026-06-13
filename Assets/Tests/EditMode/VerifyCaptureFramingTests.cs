using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Regression guards for the verify-capture FRAMING math (ticket 86ca8fevz) — the bug CLASS that made
    /// the shipped verify captures unreliable evidence (the false-green soak-rejection class): NON-
    /// DETERMINISTIC framing + clipping the subject at the frame edge.
    ///
    /// These pin the PURE math (VerifyCaptureFraming.ComputeFrame) so a future change that reintroduces
    /// run-to-run variance, a too-close distance, or an off-centre lookAt fails loud in headless CI:
    ///   - DETERMINISM: identical inputs -> identical frame (the height 0.50-vs-0.80 variance class).
    ///   - FULLY FRAMED: the derived distance fits the FULL bounds with margin (the clipped-axe class).
    ///   - CENTRED: the camera looks at the bounds centre from the requested side (no rear/cropped shots).
    /// The actual SHIPPED frame is judged from the built exe (the capture gate); these guard the math
    /// that drives it so it can't silently regress the frame the gate captures.
    /// </summary>
    public class VerifyCaptureFramingTests
    {
        const float Fov = 40f;
        const float Aspect = 16f / 9f;

        // The bug INSTANCE: the same code produced height 0.50 then 0.80 across runs. The bug CLASS:
        // the framing was not a deterministic function of its inputs. Pin determinism directly.
        [Test]
        public void ComputeFrame_IsDeterministic_SameInputsSameFrame()
        {
            var center = new Vector3(1.2f, 0.49f, -3.4f);
            var size = new Vector3(0.79f, 1.00f, 0.92f);
            var viewDir = new Vector3(0.18f, 0.12f, 1.0f);

            var a = VerifyCaptureFraming.ComputeFrame(center, size, viewDir, Fov, Aspect, 0.70f);
            var b = VerifyCaptureFraming.ComputeFrame(center, size, viewDir, Fov, Aspect, 0.70f);

            Assert.AreEqual(a.distance, b.distance, 1e-6f, "distance must be identical for identical inputs");
            Assert.That(Vector3.Distance(a.position, b.position), Is.LessThan(1e-6f), "camera position must be identical");
            Assert.That(Quaternion.Angle(a.rotation, b.rotation), Is.LessThan(1e-4f), "camera rotation must be identical");
        }

        // A DIFFERENT (already-settled) height must give a DIFFERENT distance — proving the distance tracks
        // the real bounds, so the fix (settle valid bounds, no Mathf.Max floor) is what makes runs match,
        // not a constant that would hide a bad bounds read.
        [Test]
        public void ComputeFrame_DistanceTracksBoundsHeight()
        {
            var center = Vector3.zero;
            var viewDir = Vector3.forward;
            var small = VerifyCaptureFraming.ComputeFrame(center, new Vector3(0.5f, 0.50f, 0.5f), viewDir, Fov, Aspect, 0.70f);
            var tall = VerifyCaptureFraming.ComputeFrame(center, new Vector3(0.5f, 1.00f, 0.5f), viewDir, Fov, Aspect, 0.70f);
            Assert.That(tall.distance, Is.GreaterThan(small.distance + 0.1f),
                "a taller settled bounds must push the camera back — distance must follow real geometry");
        }

        // The clipped-axe class: the derived distance must put the FULL subject on-screen with the
        // requested margin. Project the bounds extents onto the view plane and assert they fit within
        // `fill` of the frame at that distance.
        [Test]
        public void ComputeFrame_FullyFramesSubject_WithMargin()
        {
            var center = new Vector3(0.20f, 0.58f, 5.74f);
            var size = new Vector3(0.16f, 0.26f, 0.43f); // the held-hatchet world bounds Tess measured
            var viewDir = new Vector3(1.0f, 0.45f, 0.2f);
            float fill = 0.62f;

            var f = VerifyCaptureFraming.ComputeFrame(center, size, viewDir, Fov, Aspect, fill);

            // Vertical half-extent the frame covers at this distance.
            float vHalf = f.distance * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float hHalf = vHalf * Aspect;
            float subjV = Mathf.Max(size.y, 0.0001f) * 0.5f;
            float subjH = Mathf.Max(Mathf.Max(size.x, size.z), 0.0001f) * 0.5f;

            // The subject's larger extent must fit within `fill` of the frame -> there IS margin (it does
            // not ride the edge). Allow a small epsilon.
            Assert.That(subjV / vHalf, Is.LessThanOrEqualTo(fill + 1e-3f),
                "subject height must fit within the fill fraction (margin, not clipped at the top)");
            Assert.That(subjH / hHalf, Is.LessThanOrEqualTo(fill + 1e-3f),
                "subject width must fit within the fill fraction (margin, not clipped at the side)");
            // And it must actually FILL most of that budget (not framed so far the prop reads tiny — the
            // other half of the NIT: the axe was 'barely in frame').
            float dominant = Mathf.Max(subjV / vHalf, subjH / hHalf);
            Assert.That(dominant, Is.GreaterThan(fill * 0.7f),
                "subject must substantially fill the frame (not tiny) — the binding extent ~= the fill target");
        }

        // Centred + viewed from the requested side: the camera looks AT the bounds centre, and sits on the
        // +viewDir side of it (so a +Z viewDir frames the +Z front, never the rear).
        [Test]
        public void ComputeFrame_LooksAtCentre_FromRequestedSide()
        {
            var center = new Vector3(0f, 0.5f, 0f);
            var size = new Vector3(0.8f, 1.0f, 0.9f);
            var viewDir = new Vector3(0.18f, 0.12f, 1.0f); // +Z front

            var f = VerifyCaptureFraming.ComputeFrame(center, size, viewDir, Fov, Aspect, 0.70f);

            // lookAt is the centre.
            Assert.That(Vector3.Distance(f.lookAt, center), Is.LessThan(1e-5f), "must look at the bounds centre");
            // Camera is on the +viewDir side (front), not behind.
            Vector3 camToCentre = (center - f.position);
            Assert.That(Vector3.Dot(camToCentre.normalized, viewDir.normalized), Is.LessThan(-0.9f),
                "camera must sit on the +viewDir (front) side and look back toward the subject");
            Assert.That(f.position.z, Is.GreaterThan(center.z),
                "for a +Z front viewDir the camera must be in front (+Z) of the subject — never the rear");
            // The forward axis points roughly toward the subject centre.
            Assert.That(Vector3.Dot(f.rotation * Vector3.forward, camToCentre.normalized), Is.GreaterThan(0.99f),
                "camera forward must aim at the subject");
        }

        // Wide subject: a subject wider than it is tall must be framed by the HORIZONTAL fit, so the width
        // is not clipped (the aspect-aware fix). Distance for a wide subject >= distance for a tall one of
        // the same dominant extent only when width is the binding constraint.
        [Test]
        public void ComputeFrame_WideSubject_FramedByWidth_NotClipped()
        {
            var center = Vector3.zero;
            var viewDir = Vector3.forward;
            // Wide+shallow: x=1.2 dominates; at 16:9 the horizontal budget is larger, but x must still fit.
            var wide = VerifyCaptureFraming.ComputeFrame(center, new Vector3(1.2f, 0.3f, 0.2f), viewDir, Fov, Aspect, 0.7f);
            float vHalf = wide.distance * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float hHalf = vHalf * Aspect;
            Assert.That((1.2f * 0.5f) / hHalf, Is.LessThanOrEqualTo(0.7f + 1e-3f),
                "a wide subject must be fully framed by the horizontal FOV (width not clipped)");
        }

        // A near-zero viewDir must not throw or NaN — it falls back to a defined frame (defensive).
        [Test]
        public void ComputeFrame_DegenerateViewDir_FallsBackDefined()
        {
            var f = VerifyCaptureFraming.ComputeFrame(Vector3.zero, new Vector3(0.5f, 1f, 0.5f), Vector3.zero, Fov, Aspect, 0.7f);
            Assert.IsFalse(float.IsNaN(f.distance), "distance must be defined for a degenerate viewDir");
            Assert.IsFalse(float.IsNaN(f.position.x) || float.IsNaN(f.position.y) || float.IsNaN(f.position.z),
                "position must be defined for a degenerate viewDir");
        }
    }
}
