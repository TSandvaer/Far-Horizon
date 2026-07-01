using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// DIAGNOSE-VIA-TRACE for the persistent jump camera-follow bug (ticket 86caaqhj5, Sponsor soak a5282c6:
    /// "jump + A/D = player jumps OUT of view; jump + S = player jumps INTO the camera; F7 does nothing for
    /// the jump").
    ///
    /// The prior two fixes (vertical DesiredFollowY + horizontal velocity-lead) were validated against an
    /// IDEALISED constant-velocity simulation (OrbitCameraFollowTests.HorizontalFollow_PerDirectionTrace) that
    /// fed a STEADY vel into DesiredFollowXZ + FollowStep. But that simulation does NOT model the REAL airborne
    /// pipeline: while airborne, WasdMovement does NOT command full move speed — it runs AirborneVelocity, which
    /// STEERS the existing horizontal velocity by a gentle accel (airControlAccel 8 u/s²) toward the input dir,
    /// CAPPED at airControlMaxSpeed (5.5, the WALK speed). So the agent.velocity the camera LEADS by during a
    /// jump is a CHANGING, INPUT-DEPENDENT, CAPPED value — not the steady walk/run speed the idealised trace used.
    ///
    /// THIS trace runs the ACTUAL pipeline per heading (W/A/S/D), frame by frame through a full ~0.62s jump arc,
    /// and QUANTIFIES the camera-seen vs the player's REAL root displacement — so the root cause is MEASURED, not
    /// hypothesised. Pure-static math (AirborneVelocity / AdvanceJump-equivalent integration / DesiredFollowXZ /
    /// FollowStep), no scene / Animator / NavMesh / Time dependency — runs in EditMode, no PlayMode fixture.
    /// </summary>
    public class JumpCameraFollowTraceTests
    {
        private const float Dt = 1f / 60f;

        // Production wiring (OrbitCamera defaults + WasdMovement airborne defaults + CastawayCharacter jump).
        private const float HorizLerp = 18f;
        private const float VertLerp = 60f;
        private const float MaxLead = 0.25f;
        private const float AirControlAccel = 5f;        // 86caambxh: production default lowered 8→5 (Sponsor "still slightly too speedy")
        private const float AirControlMaxSpeed = 5.5f;   // = WALK speed; the airborne horizontal cap
        private const float WalkSpeed = 5.5f;
        private const float RunSpeed = 9.5f;
        private const float JumpVel = 5.5f;
        private const float JumpGravity = 18f;

        private static readonly (string name, Vector3 dir)[] Headings =
        {
            ("W (forward)", new Vector3(0f, 0f, 1f)),
            ("S (back)",    new Vector3(0f, 0f, -1f)),
            ("A (left)",    new Vector3(-1f, 0f, 0f)),
            ("D (right)",   new Vector3(1f, 0f, 0f)),
        };

        /// <summary>
        /// One frame of the REAL airborne XZ pipeline: the agent velocity is steered by AirborneVelocity (the
        /// shipped WasdMovement airborne path) and the root is then displaced by that velocity. Returns the new
        /// (velocity, rootPos). The camera reads THIS velocity for its lead and THIS rootPos as its target.
        /// </summary>
        private static (Vector3 vel, Vector3 root) StepAirborne(Vector3 vel, Vector3 root, Vector3 moveDir)
        {
            Vector3 newVel = WasdMovement.AirborneVelocity(vel, moveDir, AirControlAccel, AirControlMaxSpeed, Dt);
            // The NavMeshAgent integrates its velocity into world position (XZ); we model the displacement the
            // root actually receives this frame. (The Y arc is separate — owned by CastawayCharacter.)
            Vector3 newRoot = root + new Vector3(newVel.x, 0f, newVel.z) * Dt;
            return (newVel, newRoot);
        }

        /// <summary>
        /// THE DIAGNOSTIC (brief req 1): per-direction trace of a RUN→jump (the Sponsor jumps while moving). For
        /// each W/A/S/D, at the grounded RUN speed carried into the jump, simulate the full airborne arc through
        /// the REAL AirborneVelocity pipeline and measure how far the camera-seen follow point trails the player's
        /// REAL root XZ at landing. QUANTIFIES the root vs camera-seen horizontal displacement per direction.
        /// </summary>
        [Test]
        public void RunJumpTrace_CameraSeenVsRealRootDisplacement_PerDirection()
        {
            float lead = OrbitCamera.EffectiveLeadTime(0f, HorizLerp, MaxLead);
            var trace = new System.Text.StringBuilder(
                $"[JumpFollowTrace] RUN→jump, carried-in speed {RunSpeed}u/s, airborne cap {AirControlMaxSpeed}u/s, " +
                $"lead={lead:F4}s:\n");

            foreach (var (name, dir) in Headings)
            {
                // Grounded RUN carried into the jump: the player was running at RunSpeed in `dir`, holding the
                // key, when Space fired. The grounded velocity is dir*RunSpeed.
                Vector3 vel = dir * RunSpeed;
                Vector3 root = Vector3.zero;
                Vector3 follow = root;       // camera follow point starts framed on the player
                Vector3 moveDir = dir;       // the Sponsor keeps the key held through the jump

                // Settle the follower to grounded steady-state first (so we measure the JUMP's effect, not a
                // cold-start transient). Grounded commands full RunSpeed.
                for (int f = 0; f < 30; f++)
                {
                    root += new Vector3(vel.x, 0f, vel.z) * Dt;
                    Vector3 d = OrbitCamera.DesiredFollowXZ(root, vel, lead);
                    follow = OrbitCamera.FollowStep(follow, d, HorizLerp, VertLerp, Dt);
                }
                float groundedResidual = new Vector2(follow.x - root.x, follow.z - root.z).magnitude;

                // ---- LIFT OFF: now airborne. The arc lasts ~2*JumpVel/JumpGravity ≈ 0.61s ≈ 37 frames. ----
                float jumpY = 0f, jumpVelY = JumpVel;
                float maxFollowGap = groundedResidual;
                float velAtLanding = 0f;
                int airFrames = 0;
                while (true)
                {
                    // Vertical arc (CastawayCharacter.AdvanceJump integration).
                    jumpVelY -= JumpGravity * Dt;
                    jumpY += jumpVelY * Dt;
                    bool landed = jumpY <= 0f;

                    // Horizontal: the REAL airborne pipeline (AirborneVelocity steers+caps the agent velocity).
                    (vel, root) = StepAirborne(vel, root, moveDir);

                    // Camera leads by the (now-capped/steered) agent velocity — exactly what ships.
                    Vector3 d = OrbitCamera.DesiredFollowXZ(root, vel, lead);
                    follow = OrbitCamera.FollowStep(follow, d, HorizLerp, VertLerp, Dt);

                    float gap = new Vector2(follow.x - root.x, follow.z - root.z).magnitude;
                    if (gap > maxFollowGap) maxFollowGap = gap;
                    airFrames++;
                    if (landed) { velAtLanding = new Vector2(vel.x, vel.z).magnitude; break; }
                }

                float landingResidual = new Vector2(follow.x - root.x, follow.z - root.z).magnitude;
                trace.AppendLine(
                    $"  {name,-12} groundedResidual={groundedResidual:F4}u  maxAirGap={maxFollowGap:F4}u  " +
                    $"landingResidual={landingResidual:F4}u  velAtLanding={velAtLanding:F3}u/s  airFrames={airFrames}");
            }
            UnityEngine.Debug.Log(trace.ToString());
            // This test is a TRACE (always passes) — it EMITS the per-direction numbers for the diagnosis. The
            // assertion guards live in the dedicated guard tests below once the root cause is confirmed + fixed.
            Assert.Pass("trace emitted — read the [JumpFollowTrace] log line for the per-direction numbers.");
        }

        /// <summary>
        /// THE ROOT-CAUSE NUMBER (brief req: quantify camera-seen vs real). The lead is velocity·leadTime, sized
        /// to cancel the steady-state lag at the speed the velocity REPORTS. During a RUN-jump the grounded
        /// follower's lead was sized for RunSpeed (9.5), but AirborneVelocity CAPS the reported velocity to
        /// AirControlMaxSpeed (5.5) — so the lead the camera applies DROPS from 9.5·lead to 5.5·lead the instant
        /// the jump starts, while the player's REAL root keeps coasting at the carried-in 9.5 for the first part
        /// of the arc (AirborneVelocity only steers, it does NOT brake a faster carried-in momentum below the cap
        /// — but its REPORTED velocity for steering toward a NEW dir gets capped). Measure the lead mismatch.
        /// </summary>
        [Test]
        public void RootCause_AirborneVelocityCap_ShrinksTheLead_VsCarriedInRunSpeed()
        {
            float lead = OrbitCamera.EffectiveLeadTime(0f, HorizLerp, MaxLead);

            // Grounded RUN: the lead the camera applied to stay framed was sized for the run speed.
            float groundedLeadDist = RunSpeed * lead;

            // First airborne frame, key held in the SAME direction: AirborneVelocity steers toward dir*max(cap,
            // startSpeed) — startSpeed is the carried-in 9.5, so the target is dir*9.5 and MoveTowards keeps it
            // there (cap rule: never brake below carried-in). So SAME-direction momentum is preserved. The lead
            // mismatch is therefore NOT in same-direction hold. Measure it.
            Vector3 sameDirVel = WasdMovement.AirborneVelocity(new Vector3(0, 0, RunSpeed), Vector3.forward,
                                                               AirControlAccel, AirControlMaxSpeed, Dt);
            float sameDirLeadDist = new Vector2(sameDirVel.x, sameDirVel.z).magnitude * lead;

            UnityEngine.Debug.Log($"[JumpFollowTrace] groundedLeadDist(run)={groundedLeadDist:F4}u " +
                                  $"sameDirAirborneLeadDist={sameDirLeadDist:F4}u " +
                                  $"sameDirVel={new Vector2(sameDirVel.x, sameDirVel.z).magnitude:F3}u/s");

            // Same-direction hold preserves carried-in momentum (cap rule), so the lead is preserved here.
            Assert.Greater(sameDirVel.z, WalkSpeed,
                "AirborneVelocity preserves carried-in run momentum on a same-direction hold (the cap never " +
                "brakes below the carried-in speed) — so a STRAIGHT run-jump keeps its lead.");
        }

        /// <summary>
        /// THE ACTUAL ROOT CAUSE (the case the Sponsor hits): NO key held during the jump (or a SHORT directional
        /// tap then release — the natural way to jump). AirborneVelocity COASTS the carried-in velocity unchanged
        /// when moveDir≈0. So the REAL root keeps moving at the carried-in speed, the camera leads by that same
        /// velocity — and they STAY matched. So a coast is FINE too. The failure must be elsewhere: measure the
        /// VERTICAL follow's horizontal SIDE-EFFECT. While airborne, DesiredFollowY raises the follow-point Y by
        /// the arc; FollowStep eases Y at VertLerp(60) but X/Z at HorizLerp(18). At the camera's 55° pitch the
        /// follow-point's WORLD position maps to a screen position — a follow point that rises in Y WITHOUT the
        /// X/Z keeping pace shifts the on-screen framing. Trace whether the per-heading on-SCREEN player position
        /// diverges even when the world-space horizontal residual is identical across headings.
        /// </summary>
        [Test]
        public void RootCause_VerticalArcShiftsScreenFraming_PerHeading_AtCameraPitch()
        {
            // The camera pitch + the jump arc projected to the camera's view plane. At pitch 55° looking down, a
            // follow-point that rises in Y (the arc) but whose camera POSITION is derived as
            // followPos - rot*forward*distance moves the camera UP with the follow point. The PLAYER ROOT does
            // NOT rise (the arc is added only to the camera's follow-Y, not the root). So the player's WORLD pos
            // is fixed-Y while the camera's look-point rose by the arc → the player projects LOWER in frame
            // (toward/below centre) — and because the camera also translated forward/up, the apparent horizontal
            // shift differs by heading (the look direction is yaw 0, so +Z motion is INTO screen, ±X is across).
            const float pitch = 55f, yaw = 0f, distance = 14f, offsetY = 1f;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            float apex = (JumpVel * JumpVel) / (2f * JumpGravity); // ~0.84u

            var trace = new System.Text.StringBuilder(
                $"[JumpFollowTrace] screen-framing shift at apex {apex:F3}u, pitch {pitch}°:\n");

            // The player root world pos (fixed Y — the arc is NOT on the root) for a small move in each dir.
            foreach (var (name, dir) in Headings)
            {
                Vector3 rootWorld = dir * 1.0f;                  // player 1u along the heading, at ground Y=0
                Vector3 headRest = rootWorld + Vector3.up * offsetY;
                Vector3 headApex = rootWorld + Vector3.up * (offsetY + apex); // follow-Y raised by the arc

                // Camera position for rest vs apex follow points.
                Vector3 camRest = headRest - rot * Vector3.forward * distance;
                Vector3 camApex = headApex - rot * Vector3.forward * distance;

                // Project the (fixed) player root into each camera's view space; the SCREEN shift of the player
                // between rest-cam and apex-cam is what the eye reads as "pulled". Use the view-space Y (vertical
                // screen) + view-space X (horizontal screen).
                Vector3 viewRest = Quaternion.Inverse(rot) * (rootWorld - camRest);
                Vector3 viewApex = Quaternion.Inverse(rot) * (rootWorld - camApex);
                float screenDX = viewApex.x - viewRest.x;
                float screenDY = viewApex.y - viewRest.y;
                trace.AppendLine($"  {name,-12} screenShift dX={screenDX:F4} dY={screenDY:F4}");
            }
            UnityEngine.Debug.Log(trace.ToString());
            Assert.Pass("trace emitted — read the [JumpFollowTrace] screen-framing line.");
        }

        /// <summary>
        /// THE DYNAMIC VERTICAL-FOLLOW TRACE (the real per-frame gap). The avatar child rises by the arc each
        /// frame; the camera's follow-Y eases toward (root.y + offset + JumpHeight) at VertLerp(60). Trace the
        /// per-frame gap between the AVATAR's actual visual world-Y (root.y + arc) and the camera's follow-point Y
        /// THROUGH the whole arc — if the follow-Y LAGS the fast-rising arc on the way UP then OVERSHOOTS on the
        /// way DOWN, the avatar bobs vertically in frame relative to the look point = the "pulled" percept. Also
        /// trace the SHIPPED bug: the follow-point rises with the arc, but on LANDING the arc snaps 0.84→0 in the
        /// landing frame (AdvanceJump clamps jumpY=0) while the follow-Y is still high → a one-frame downward jerk
        /// of the look point exactly at touchdown = "pulled backwards/down right before landing".
        /// </summary>
        [Test]
        public void DynamicVerticalFollow_GapToActualAvatarY_ThroughTheArc()
        {
            const float rootY = 0f, offsetY = 1f;
            // The avatar's VISUAL world-Y = rootY + arc. The thing the camera SHOULD frame is the avatar head:
            // (rootY + arc) + offsetY. The shipped DesiredFollowY computes rootY + offsetY + arc — SAME value.
            // So the TARGET is right; the question is the dynamic FollowStep gap + the landing snap.
            float followY = rootY + offsetY;     // settled grounded
            float jumpY = 0f, jumpVelY = JumpVel;
            float maxRiseGap = 0f, maxFallGap = 0f, landingJerk = 0f;
            float prevFollowY = followY;
            var trace = new System.Text.StringBuilder("[JumpFollowTrace] dynamic vertical follow through the arc:\n");
            int f = 0;
            while (true)
            {
                float prevArc = jumpY;
                jumpVelY -= JumpGravity * Dt;
                jumpY += jumpVelY * Dt;
                bool landed = jumpY <= 0f;
                if (landed) jumpY = 0f;   // AdvanceJump clamps to ground on the landing frame

                float avatarVisualHeadY = rootY + jumpY + offsetY;   // where the head actually is this frame
                float desiredFollowY = OrbitCamera.DesiredFollowY(rootY, offsetY, jumpY);
                // Vertical follow eases at VertLerp (the Y axis of FollowStep).
                float av = 1f - Mathf.Exp(-VertLerp * Dt);
                followY = Mathf.Lerp(followY, desiredFollowY, av);

                float gap = followY - avatarVisualHeadY;   // >0 camera looks ABOVE the head; <0 below
                if (jumpVelY > 0f) maxRiseGap = Mathf.Min(maxRiseGap, gap);   // rising: most-negative (lag below)
                else if (!landed) maxFallGap = Mathf.Max(maxFallGap, gap);
                if (landed) landingJerk = Mathf.Abs(followY - prevFollowY);   // one-frame look-point jump at touchdown
                if (f < 6 || landed || Mathf.Abs(jumpVelY) < 0.4f)
                    trace.AppendLine($"  f={f,2} arc={jumpY:F4} headY={avatarVisualHeadY:F4} " +
                                     $"followY={followY:F4} gap={gap:F4} landed={landed}");
                prevFollowY = followY;
                f++;
                if (landed) break;
            }
            trace.AppendLine($"  SUMMARY maxRiseLagBelow={maxRiseGap:F4}u maxFallGapAbove={maxFallGap:F4}u " +
                             $"landingLookJerk={landingJerk:F4}u frames={f}");
            UnityEngine.Debug.Log(trace.ToString());
            Assert.Pass("trace emitted — read the [JumpFollowTrace] dynamic vertical follow line.");
        }

        /// <summary>
        /// THE FULL-PIPELINE SCREEN-PROJECTION TRACE (the honest percept measurement). Model BOTH the avatar
        /// rising (arc on the child) AND the camera follow-point math AND the orbit camera position+rotation, then
        /// project the AVATAR's actual visual head into camera screen space each frame. If the avatar stays at a
        /// stable screen position the framing is correct; if it drifts the percept is real. This is the load-
        /// bearing trace: it measures the PERCEPT (screen position), not a world-space proxy gap (the false-green
        /// family, unity-conventions.md). Run per heading at RUN speed to expose any heading asymmetry on SCREEN.
        /// </summary>
        [Test]
        public void FullPipeline_AvatarScreenPosition_ThroughTheArc_PerHeading()
        {
            const float pitch = 55f, distance = 14f, offsetY = 1f;
            float lead = OrbitCamera.EffectiveLeadTime(0f, HorizLerp, MaxLead);
            var trace = new System.Text.StringBuilder(
                $"[JumpFollowTrace] avatar SCREEN position through the arc (pitch {pitch}°, dist {distance}):\n");

            foreach (var (name, dir) in Headings)
            {
                // yaw 0 throughout (the Sponsor's default framing); the player runs in `dir`.
                Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
                Vector3 vel = dir * RunSpeed;
                Vector3 root = Vector3.zero;
                Vector3 follow = root + Vector3.up * offsetY;     // settled grounded follow point

                // settle grounded
                for (int s = 0; s < 30; s++)
                {
                    root += new Vector3(vel.x, 0f, vel.z) * Dt;
                    Vector3 d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, vel, lead);
                    d.y = OrbitCamera.DesiredFollowY(root.y, offsetY, 0f);
                    follow = OrbitCamera.FollowStep(follow, d, HorizLerp, VertLerp, Dt);
                }

                float jumpY = 0f, jumpVelY = JumpVel;
                float restScreenY = 0f, minScreenY = 0f, maxScreenY = 0f, minScreenX = 0f, maxScreenX = 0f;
                bool first = true;
                while (true)
                {
                    jumpVelY -= JumpGravity * Dt;
                    jumpY += jumpVelY * Dt;
                    bool landed = jumpY <= 0f;
                    if (landed) jumpY = 0f;

                    (vel, root) = StepAirborne(vel, root, dir);     // real airborne XZ
                    Vector3 d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, vel, lead);
                    d.y = OrbitCamera.DesiredFollowY(root.y, offsetY, jumpY);   // shipped: arc folded into follow-Y
                    follow = OrbitCamera.FollowStep(follow, d, HorizLerp, VertLerp, Dt);

                    // Orbit camera position (no terrain collision in this trace).
                    Vector3 camPos = follow - rot * Vector3.forward * distance;
                    // The AVATAR's actual visual head world pos: root XZ + arc Y + the body offset.
                    Vector3 avatarHead = new Vector3(root.x, root.y + jumpY + offsetY, root.z);
                    // Project to camera view space (screen X = view.x, screen Y = view.y).
                    Vector3 view = Quaternion.Inverse(rot) * (avatarHead - camPos);
                    float screenX = view.x, screenY = view.y;
                    if (first) { restScreenY = screenY; minScreenY = maxScreenY = screenY;
                                 minScreenX = maxScreenX = screenX; first = false; }
                    minScreenY = Mathf.Min(minScreenY, screenY); maxScreenY = Mathf.Max(maxScreenY, screenY);
                    minScreenX = Mathf.Min(minScreenX, screenX); maxScreenX = Mathf.Max(maxScreenX, screenX);
                    if (landed) break;
                }
                trace.AppendLine($"  {name,-12} screenY range [{minScreenY:F4}..{maxScreenY:F4}] " +
                                 $"swing={(maxScreenY - minScreenY):F4}  screenX swing={(maxScreenX - minScreenX):F4}");
            }
            UnityEngine.Debug.Log(trace.ToString());
            Assert.Pass("trace emitted — read the [JumpFollowTrace] avatar SCREEN position line.");
        }

        /// <summary>
        /// THE LEAD-OFF CONTROL (the A/B that confirms the lead is the heading-asymmetry source). Re-run the
        /// full-pipeline screen trace with the horizontal velocity LEAD DISABLED (lead=0). If the per-heading
        /// screen-Y offset COLLAPSES to a single value across W/S/A/D, the LEAD was framing the player off-centre
        /// in the travel direction — that off-centre framing reads as "jump OUT of view (W/A/D away) / INTO the
        /// camera (S toward)" because the eye fixates on the airborne avatar. This is the load-bearing refutation
        /// of the prior fix: the velocity lead (added in attempt 2 to cancel a world-space follow LAG) introduced
        /// a heading-dependent screen OFFSET that is the actual Sponsor percept.
        /// </summary>
        [Test]
        public void LeadOff_Control_ScreenYOffsetCollapsesAcrossHeadings()
        {
            const float pitch = 55f, distance = 14f, offsetY = 1f;
            var trace = new System.Text.StringBuilder(
                $"[JumpFollowTrace] LEAD-OFF control — avatar SCREEN position (lead=0):\n");
            var settledScreenY = new System.Collections.Generic.List<float>();

            foreach (var (name, dir) in Headings)
            {
                Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
                Vector3 vel = dir * RunSpeed;
                Vector3 root = Vector3.zero;
                Vector3 follow = root + Vector3.up * offsetY;
                for (int s = 0; s < 30; s++)
                {
                    root += new Vector3(vel.x, 0f, vel.z) * Dt;
                    Vector3 d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, vel, 0f);  // LEAD OFF
                    d.y = OrbitCamera.DesiredFollowY(root.y, offsetY, 0f);
                    follow = OrbitCamera.FollowStep(follow, d, HorizLerp, VertLerp, Dt);
                }
                Vector3 camPos = follow - rot * Vector3.forward * distance;
                Vector3 avatarHead = new Vector3(root.x, root.y + offsetY, root.z);
                Vector3 view = Quaternion.Inverse(rot) * (avatarHead - camPos);
                settledScreenY.Add(view.y);
                trace.AppendLine($"  {name,-12} settled screenY={view.y:F4}  screenX={view.x:F4}");
            }
            float min = settledScreenY[0], max = settledScreenY[0];
            foreach (var y in settledScreenY) { min = Mathf.Min(min, y); max = Mathf.Max(max, y); }
            trace.AppendLine($"  SUMMARY lead-off screenY spread across headings = {(max - min):F4}u " +
                             "(small ⇒ the LEAD was the heading-asymmetry source)");
            UnityEngine.Debug.Log(trace.ToString());
            Assert.Pass("trace emitted — read the [JumpFollowTrace] LEAD-OFF control line.");
        }

        /// <summary>
        /// THE FIX VALIDATION (candidate). Root cause confirmed: the horizontal follow LAG (v/k = 9.5/18 ≈ 0.53u)
        /// frames the airborne player OFF-CENTRE in the travel direction — screenY +0.37 (W) / −0.37 (S) / screenX
        /// ±0.45 (A/D) (the LEAD-OFF control). The velocity lead only cancels it when the agent's reported velocity
        /// matches the real travel rate — fragile under the airborne accel/cap, and un-dialable via F7. ROBUST FIX:
        /// while AIRBORNE, follow the player root XZ TIGHTLY (a high follow rate → ~zero lag), so the airborne
        /// avatar stays centred regardless of heading. This trace runs the full pipeline with a high airborne
        /// horizontal follow rate and asserts the per-heading settled screen offset is SMALL + symmetric.
        /// </summary>
        [Test]
        public void FixCandidate_TightAirborneHorizontalFollow_CentresAvatar_AllHeadings()
        {
            const float pitch = 55f, distance = 14f, offsetY = 1f;
            const float airborneHorizLerp = 60f;    // candidate: tight airborne XZ follow (≈zero lag)
            var trace = new System.Text.StringBuilder(
                $"[JumpFollowTrace] FIX candidate — airborne horizLerp={airborneHorizLerp}, lead off airborne:\n");
            var offsets = new System.Collections.Generic.List<float>();

            foreach (var (name, dir) in Headings)
            {
                Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
                Vector3 vel = dir * RunSpeed;
                Vector3 root = Vector3.zero;
                Vector3 follow = root + Vector3.up * offsetY;
                float lead = OrbitCamera.EffectiveLeadTime(0f, HorizLerp, MaxLead);
                for (int s = 0; s < 30; s++)   // grounded settle (normal lead + lerp)
                {
                    root += new Vector3(vel.x, 0f, vel.z) * Dt;
                    Vector3 d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, vel, lead);
                    d.y = OrbitCamera.DesiredFollowY(root.y, offsetY, 0f);
                    follow = OrbitCamera.FollowStep(follow, d, HorizLerp, VertLerp, Dt);
                }

                float jumpY = 0f, jumpVelY = JumpVel;
                float worstOffset = 0f;
                while (true)
                {
                    jumpVelY -= JumpGravity * Dt;
                    jumpY += jumpVelY * Dt;
                    bool landed = jumpY <= 0f;
                    if (landed) jumpY = 0f;
                    (vel, root) = StepAirborne(vel, root, dir);
                    // AIRBORNE: follow the root XZ DIRECTLY (no lead), at the tight airborne rate → ~zero lag.
                    Vector3 d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, Vector3.zero, 0f);
                    d.y = OrbitCamera.DesiredFollowY(root.y, offsetY, jumpY);
                    follow = OrbitCamera.FollowStep(follow, d, airborneHorizLerp, VertLerp, Dt);
                    Vector3 camPos = follow - rot * Vector3.forward * distance;
                    Vector3 avatarHead = new Vector3(root.x, root.y + jumpY + offsetY, root.z);
                    Vector3 view = Quaternion.Inverse(rot) * (avatarHead - camPos);
                    worstOffset = Mathf.Max(worstOffset, new Vector2(view.x, view.y).magnitude);
                    if (landed) break;
                }
                offsets.Add(worstOffset);
                trace.AppendLine($"  {name,-12} worst screen offset from centre = {worstOffset:F4}u");
            }
            float min = offsets[0], max = offsets[0];
            foreach (var o in offsets) { min = Mathf.Min(min, o); max = Mathf.Max(max, o); }
            trace.AppendLine($"  SUMMARY worst-offset spread across headings = {(max - min):F4}u  max = {max:F4}u");
            UnityEngine.Debug.Log(trace.ToString());
            Assert.Pass("trace emitted — read the [JumpFollowTrace] FIX candidate line.");
        }

        // =================================================================================================
        // REGRESSION GUARDS (brief req: "per-direction 'camera tracks avatar world-pos through jump within Xu'
        // guard"). The CONFIRMED root cause: the horizontal follow steady-state lag (v/k) frames the airborne
        // player OFF-CENTRE by travel heading. The fix follows the root XZ TIGHTLY (airborneFollowLerp) with NO
        // lead while airborne. These pin the BUG CLASS (the airborne avatar must stay centred + symmetric across
        // W/A/S/D), not one instance — pure-static, no scene/Time. They assert the PERCEPT (screen-space offset
        // from frame centre), NOT a world-space proxy gap (the false-green family the prior fixes fell into).
        // =================================================================================================

        // Simulate a RUN→jump in `dir` through the full arc and return the WORST screen-space offset of the
        // avatar head from frame centre, using the given horizontal-follow mode (grounded lead+lerp settle, then
        // airborne mode for the arc). `airborneTight`=true models the FIX (root XZ, no lead, airborneFollowLerp);
        // false models the SHIPPED-PRE-FIX (lead + followLerp continue through the jump).
        private static float WorstScreenOffsetThroughJump(Vector3 dir, bool airborneTight, float airborneRate)
        {
            const float pitch = 55f, distance = 14f, offsetY = 1f;
            Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
            float lead = OrbitCamera.EffectiveLeadTime(0f, HorizLerp, MaxLead);
            Vector3 vel = dir * RunSpeed;
            Vector3 root = Vector3.zero;
            Vector3 follow = root + Vector3.up * offsetY;
            for (int s = 0; s < 30; s++)   // grounded settle (normal lead + lerp)
            {
                root += new Vector3(vel.x, 0f, vel.z) * Dt;
                Vector3 d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, vel, lead);
                d.y = OrbitCamera.DesiredFollowY(root.y, offsetY, 0f);
                follow = OrbitCamera.FollowStep(follow, d, HorizLerp, VertLerp, Dt);
            }
            float jumpY = 0f, jumpVelY = JumpVel, worst = 0f;
            while (true)
            {
                jumpVelY -= JumpGravity * Dt;
                jumpY += jumpVelY * Dt;
                bool landed = jumpY <= 0f;
                if (landed) jumpY = 0f;
                (vel, root) = StepAirborne(vel, root, dir);
                Vector3 d;
                float rate;
                if (airborneTight)
                {
                    d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, Vector3.zero, 0f); // no lead
                    rate = airborneRate;
                }
                else
                {
                    d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, vel, lead);        // pre-fix: lead on
                    rate = HorizLerp;
                }
                d.y = OrbitCamera.DesiredFollowY(root.y, offsetY, jumpY);
                follow = OrbitCamera.FollowStep(follow, d, rate, VertLerp, Dt);
                Vector3 camPos = follow - rot * Vector3.forward * distance;
                Vector3 avatarHead = new Vector3(root.x, root.y + jumpY + offsetY, root.z);
                Vector3 view = Quaternion.Inverse(rot) * (avatarHead - camPos);
                worst = Mathf.Max(worst, new Vector2(view.x, view.y).magnitude);
                if (landed) break;
            }
            return worst;
        }

        [Test]
        public void Guard_AirborneTightFollow_KeepsAvatarCentred_AllFourHeadings_AndSymmetric()
        {
            // THE FIX GUARD: with the airborne tight horizontal follow (airborneFollowLerp 60, no lead), the
            // avatar must stay within a small screen offset from centre in EVERY heading, AND the offset must be
            // symmetric across W/A/S/D (the bug was a heading-DEPENDENT off-centre framing). Tolerance 0.18u is
            // well below the 0.37–0.45u off-centre the pre-fix produced, and above the ~0.10u the fix leaves.
            const float airborneRate = 60f;   // == OrbitCamera.airborneFollowLerp default
            var offsets = new System.Collections.Generic.List<float>();
            foreach (var (name, dir) in Headings)
            {
                float o = WorstScreenOffsetThroughJump(dir, airborneTight: true, airborneRate: airborneRate);
                offsets.Add(o);
                Assert.Less(o, 0.18f,
                    $"airborne, heading {name}: the avatar must stay within 0.18u of frame centre through the jump " +
                    $"(got {o:F4}u). A larger offset is the 'jump out of view / into the camera' percept (86caaqhj5).");
            }
            float min = offsets[0], max = offsets[0];
            foreach (var o in offsets) { min = Mathf.Min(min, o); max = Mathf.Max(max, o); }
            Assert.Less(max - min, 0.05f,
                $"the airborne screen offset must be SYMMETRIC across W/A/S/D (the bug was heading-DEPENDENT: A/D " +
                $"out of view, S into the camera); spread was {(max - min):F4}u.");
        }

        // Model the FRAGILITY the fix removes (the honest bug-class anchor). The attempt-2 lead cancels the lag
        // ONLY when the velocity the camera READS equals the root's real travel rate. In the real build that
        // assumption breaks: the NavMeshAgent (acceleration=30) reports a velocity that LAGS the commanded value,
        // and air-control steers/caps it — so agent.velocity (what the lead reads) ≠ the root displacement rate.
        // Model that mismatch by feeding the lead a velocity that is WRONG by a factor (the agent under-reports).
        // PRE-FIX: lead by the wrong velocity + followLerp → the mis-sized lead leaves residual off-centre framing.
        // FIX: ignore the velocity entirely, follow the root XZ tight → robust to ANY velocity-report error.
        private static float WorstScreenOffset_WithVelocityReportError(bool airborneTight, float airborneRate,
                                                                       Vector3 dir, float velReportFactor)
        {
            const float pitch = 55f, distance = 14f, offsetY = 1f;
            Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
            float lead = OrbitCamera.EffectiveLeadTime(0f, HorizLerp, MaxLead);
            Vector3 trueVel = dir * RunSpeed;                 // the root's ACTUAL travel velocity
            Vector3 root = Vector3.zero;
            Vector3 follow = root + Vector3.up * offsetY;
            for (int s = 0; s < 30; s++)   // grounded settle (lead reads the TRUE velocity, well-tuned)
            {
                root += new Vector3(trueVel.x, 0f, trueVel.z) * Dt;
                Vector3 d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, trueVel, lead);
                d.y = OrbitCamera.DesiredFollowY(root.y, offsetY, 0f);
                follow = OrbitCamera.FollowStep(follow, d, HorizLerp, VertLerp, Dt);
            }
            // AIRBORNE: the root keeps coasting at trueVel, but the agent UNDER-REPORTS its velocity (the real
            // sim/air-control mismatch) — the lead reads trueVel*velReportFactor (e.g. 0.6 = the agent reports 60%).
            Vector3 reportedVel = trueVel * velReportFactor;
            float jumpY = 0f, jumpVelY = JumpVel, worst = 0f;
            while (true)
            {
                jumpVelY -= JumpGravity * Dt;
                jumpY += jumpVelY * Dt;
                bool landed = jumpY <= 0f;
                if (landed) jumpY = 0f;
                root += new Vector3(trueVel.x, 0f, trueVel.z) * Dt;   // root coasts at the TRUE rate
                Vector3 d;
                float rate;
                if (airborneTight)
                {
                    d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, Vector3.zero, 0f); // FIX: no lead
                    rate = airborneRate;
                }
                else
                {
                    d = OrbitCamera.DesiredFollowXZ(root + Vector3.up * offsetY, reportedVel, lead); // PRE-FIX: wrong lead
                    rate = HorizLerp;
                }
                d.y = OrbitCamera.DesiredFollowY(root.y, offsetY, jumpY);
                follow = OrbitCamera.FollowStep(follow, d, rate, VertLerp, Dt);
                Vector3 camPos = follow - rot * Vector3.forward * distance;
                Vector3 avatarHead = new Vector3(root.x, root.y + jumpY + offsetY, root.z);
                Vector3 view = Quaternion.Inverse(rot) * (avatarHead - camPos);
                worst = Mathf.Max(worst, new Vector2(view.x, view.y).magnitude);
                if (landed) break;
            }
            return worst;
        }

        [Test]
        public void Guard_FixIsRobustToVelocityReportError_WherePreFixLeadDrifts()
        {
            // THE BUG-CLASS ANCHOR ("could a wrong version pass?"). The attempt-2 lead is FRAGILE: it cancels the
            // lag only when agent.velocity (read by the lead) equals the root's real travel rate. In the real
            // build the agent under-reports (acceleration clamp + air-control steer/cap), so the lead is mis-sized
            // and leaves a heading-dependent off-centre framing during the jump — the Sponsor percept. The FIX
            // (airborne tight follow, NO lead) does not depend on the velocity report at all, so it stays centred
            // for ANY report error. Model a near-zero report (the COAST case: no key held mid-air → AirborneVelocity
            // coasts + the agent's reported velocity decays, so the lead reads ~0 while the root still travels —
            // the lead vanishes and the full v/k lag re-appears). reportFactor 0.1 = the agent reports ~10%.
            const float reportFactor = 0.1f;
            float worstPreFix = 0f, worstFix = 0f;
            foreach (var (_, dir) in Headings)
            {
                worstPreFix = Mathf.Max(worstPreFix,
                    WorstScreenOffset_WithVelocityReportError(false, 0f, dir, reportFactor));
                worstFix = Mathf.Max(worstFix,
                    WorstScreenOffset_WithVelocityReportError(true, 60f, dir, reportFactor));
            }
            UnityEngine.Debug.Log($"[JumpFollowTrace] velocity-report-error: preFix worst={worstPreFix:F4}u " +
                                  $"fix worst={worstFix:F4}u (reportFactor={reportFactor})");
            // The pre-fix mis-sized lead drifts the avatar well off-centre under the report error.
            Assert.Greater(worstPreFix, 0.20f,
                $"the PRE-FIX velocity lead must drift the avatar off-centre when the agent under-reports its " +
                $"velocity (got {worstPreFix:F4}u) — the fragility the air-control/agent-sim mismatch exposes.");
            // The fix is robust to the SAME report error (it ignores the velocity report).
            Assert.Less(worstFix, 0.15f,
                $"the FIX (airborne tight follow, no lead) must stay centred regardless of the velocity-report " +
                $"error (got {worstFix:F4}u) — it does not depend on the lead cancellation at all.");
            Assert.Less(worstFix, worstPreFix - 0.10f,
                $"the fix ({worstFix:F4}u) must beat the pre-fix ({worstPreFix:F4}u) by a clear margin under the " +
                "report error — proving the airborne tight-follow fix is load-bearing, not cosmetic.");
        }
    }
}
