Shader "FarHorizon/GradientSkybox"
{
    // A 3-STOP VERTICAL GRADIENT SKYBOX (world-look polish, ticket 86ca8t9pq — Uma world-look brief §3).
    //
    // The Sponsor-approved Zone-D look shipped a 2-color Skybox/Procedural (a sun-disc atmospheric model
    // with only _SkyTint + _GroundColor — NOT a true 3-stop gradient). Uma §3 specifies a clean 3-stop
    // warm-bright vertical gradient (zenith -> mid -> warm horizon) — the open-day toy-diorama sky the
    // clouds float in and the landmasses dissolve into. This shader IS that: a pure vertical gradient
    // keyed off the skybox view ray's world-Y, three colour stops lerped across the dome.
    //
    // SKYBOX STRUCTURE (the load-bearing fix — first impl drew OVER geometry): a material assigned to
    // RenderSettings.skybox is drawn by Unity's SKYBOX PASS, which renders a unit skydome whose OBJECT-
    // SPACE vertex position IS the view direction. The shader must use the standard skybox render state
    // — Cull Off, ZWrite Off, and depth handled by the skybox pass (it draws at the far plane AFTER
    // opaque) — and must NOT force depth via a positionCS.xyww trick (that combined with a Background-
    // queue opaque SubShader made the gradient draw as a full-screen fill OVER the terrain — washed the
    // whole frame to the horizon colour; isolated via a -flatSky probe that rendered the scene correctly).
    // This uses the SAME idiom as the built-in Skybox/Procedural: UnityObjectToClipPos on the dome vertex
    // + the object-space position as the direction.
    //
    // WARM HORIZON IS LOAD-BEARING (Uma §3): the horizon stop is a warm pale cream-blue (#DCE8E4), not a
    // cold white. The farthest vista range + the distance fog tint both blend INTO this horizon stop (the
    // seamless sky-dissolve, Uma §2). All stop colours are sub-1.0 (HDR-clamp-safe).
    //
    // BUILD NOTE: a custom shader is STRIPPED from the standalone build unless registered in
    // GraphicsSettings AlwaysIncludedShaders (unity-conventions.md §Build stripping). QualityPassGen
    // registers it at bootstrap time.
    Properties
    {
        // W4 nicer-sky soak-fix (86ca8t9pq) — cheerful saturated blue zenith -> clean mid -> warm pale
        // horizon. Kept in sync with FarHorizon.WorldLookPalette (the bootstrap sets these explicitly;
        // these defaults match so the asset reads the same look if opened raw).
        _ZenithColor  ("Zenith Color",  Color) = (0.38, 0.62, 0.85, 1)
        _MidColor     ("Mid Color",     Color) = (0.60, 0.78, 0.90, 1)
        _HorizonColor ("Horizon Color", Color) = (0.88, 0.90, 0.84, 1)
        // Where the MID stop sits (0 = horizon, 1 = zenith). Uma §3: mid ~ horizon+30deg.
        _MidPoint ("Mid Point", Range(0.05, 0.95)) = 0.35
        // Softness of the two transitions (smoothstep width).
        _Softness ("Softness", Range(0.01, 1.0)) = 0.6

        // ---- SUN DISK (ticket 86cabc743 — Erik low-poly-sky research, POC item 1) ----
        // A small additive warm sun disk where the view ray faces the directional Sun. Additive (col +=)
        // so the post-stack Bloom lifts a soft warm corona for free. Defaults are the SPONSOR-ACCEPTED soft
        // warm-white values (soak 55bde02, ticket 86cag25az; QualityPassGen sets these explicitly at
        // bootstrap, kept in sync).
        _SunColor    ("Sun Color",    Color)            = (0.98, 0.86, 0.86, 1)
        // Disk edge position in dot-product space: higher = SMALLER disk (1.0 = a point). 0.95 = the LARGEST
        // disk the range allows — the Sponsor-accepted chunky board-scale sun (was 0.992 ~7°; the original
        // 0.9985 rendered a sub-pixel pinpoint dot in the first capture).
        _SunSize     ("Sun Size",     Range(0.95, 0.9999)) = 0.95
        // pow exponent on the disk falloff — higher = crisper edge. ~60 reads as a clean low-poly disk with
        // a touch of edge softness for the bloom corona.
        _SunHardness ("Sun Hardness", Range(8.0, 400.0))   = 60.0
        // World-space direction TOWARD the Sun (the sun disk centre). Set by QualityPassGen from the actual
        // Sun light transform at bootstrap — NOT read from the URP _MainLightPosition global, which is NOT
        // bound in the Background/skybox pass (verified empirically — see the CBUFFER note below). Raw default
        // matches the Sponsor-accepted LOWERED elev-18 Sun (ticket 86cag25az; was elev-25 (0.520,0.423,-0.742),
        // earlier elev-48 (0.38,0.74,-0.55)); the bootstrap bakes the real value over this, so this only
        // matters if the asset is opened raw.
        _SunDirection ("Sun Direction (world, to-sun)", Vector) = (0.546, 0.309, -0.779, 0)

        // ---- SUN-AZIMUTH HORIZON WARMTH (SKY-1, ticket 86cahhfkc — plan §5 Tier-1 item 7) ----
        // A SUBTLE warm glow biased into the horizon band AROUND the sun's azimuth: the accepted low sun
        // (elev-18) should tie into the sky with a touch of warmth where it meets the horizon, not sit on a
        // uniform cool horizon. This is a FRAG-ONLY lerp of the RENDERED sky colour toward _SunColor near
        // the horizon + sun azimuth — it does NOT touch the _HorizonColor PROPERTY, so the fog==_HorizonColor
        // single-constant SEAM-KILL is preserved (fog colour still equals the horizon stop; only the sky's
        // own low-band pixels warm slightly toward the sun). Bounded to _SunHorizonWarmth (≤~0.06) so even at
        // the sun azimuth the sky-vs-fog difference at the seam stays below the eye's threshold — the vista/
        // fog dissolve is not reopened. Default 0.06 = the spec ceiling; 0 = today's uniform horizon.
        _SunHorizonWarmth ("Sun Horizon Warmth (0 = off, <=0.06)", Range(0, 0.06)) = 0.06
    }
    SubShader
    {
        // Standard skybox render state: drawn in the Background queue by the skybox pass, never culled,
        // never writes depth (so it can't occlude — and the skybox pass draws it at the far plane AFTER
        // opaque geometry, so geometry is in front of it).
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _MidColor;
                float4 _HorizonColor;
                float  _MidPoint;
                float  _Softness;
                float4 _SunColor;
                float  _SunSize;
                float  _SunHardness;
                float4 _SunDirection;
                float  _SunHorizonWarmth;   // SKY-1 — bounded sun-azimuth horizon warmth (frag-only; seam-safe)
            CBUFFER_END

            // SUN DIRECTION — the OPEN QUESTION Erik flagged (research §"Recommended dev POC", evidence-
            // strength summary): does GetMainLight() / _MainLightPosition bind inside the URP SKYBOX
            // (Background-queue) pass? VERDICT (verified EMPIRICALLY in the shipped exe via -verifySky):
            // NO. The first attempt used the URP global _MainLightPosition; the shipped capture showed the
            // frame-centre pixel was PLAIN SKY BLUE (0.33,0.60,0.89) with NO warm disk — sunMask was ~0
            // because _MainLightPosition is NOT populated in the lightweight Background/skybox pass (it is
            // set up for the opaque/transparent forward passes, not the skybox dome draw). GetMainLight()
            // would fail the same way (it reads the same global). So neither the include nor the global works
            // here. FIX (the robust path): a _SunDirection MATERIAL property set from C# at bootstrap from the
            // actual Sun light transform — it lives in UnityPerMaterial (SRP-Batcher safe) and is ALWAYS
            // bound for this material regardless of which URP pass draws it. The Sun is static, so a baked
            // material direction is exact + has zero per-frame cost.

            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dir         : TEXCOORD0; // OBJECT-space dome dir (gradient uses .y — validated)
                float3 dirWS       : TEXCOORD1; // WORLD-space view ray (the sun-disk dot needs this)
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                // Skybox pass: the dome vertex's OBJECT-SPACE position IS the view direction (the skybox
                // mesh is a unit cube/sphere centred on the camera). Transform to clip space the normal
                // way (the skybox pass already sets up the camera-anchored matrices); the object-space
                // position carries the direction for the vertical gradient.
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
                // WORLD-space view ray for the sun disk: the skybox dome's object space is ROTATED by the
                // camera (object != world here), so dotting the OBJECT-space dir against the WORLD-space
                // _SunDirection NEVER aligns (verified empirically: the first material-property attempt also
                // rendered NO disk for exactly this reason). TransformObjectToWorld maps the dome vertex into
                // world space; minus the camera position gives the true world view direction the sun dot needs.
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.dirWS = posWS - _WorldSpaceCameraPos;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Vertical gradient param: 0 at the horizon, 1 at the zenith. Use the dome direction's Y.
                float3 dir = normalize(IN.dir);
                float t = saturate(dir.y); // 0 horizon .. 1 straight up

                // Two smoothstep transitions: horizon->mid below _MidPoint, mid->zenith above it.
                float halfW = max(_Softness * 0.5, 0.001);
                float3 col;
                if (t <= _MidPoint)
                {
                    float k = smoothstep(max(_MidPoint - halfW, 0.0), _MidPoint, t);
                    col = lerp(_HorizonColor.rgb, _MidColor.rgb, k);
                }
                else
                {
                    float k = smoothstep(_MidPoint, min(_MidPoint + halfW, 1.0), t);
                    col = lerp(_MidColor.rgb, _ZenithColor.rgb, k);
                }

                // ---- SUN-AZIMUTH HORIZON WARMTH (SKY-1, ticket 86cahhfkc) ----
                // Bias the horizon band toward the warm sun colour AROUND the sun's azimuth, so the low sun
                // ties into the sky with a touch of warmth at the horizon (not a uniformly cool horizon).
                // AZIMUTH alignment: dot the view ray and the sun direction on the HORIZONTAL plane (drop Y)
                // so the warmth follows the sun's compass bearing, widest at the horizon and narrowing as the
                // eye pans away in azimuth. HORIZON weight: strongest at t≈0 (the horizon line), fading out
                // by t≈0.30 so only the low band warms (the zenith is untouched). The product × the bounded
                // _SunHorizonWarmth is a TINY lerp of the RENDERED colour toward _SunColor — it never touches
                // the _HorizonColor PROPERTY, so fog==_HorizonColor stays exact (seam-kill preserved). Both
                // weights are computed off the same viewWS the disk uses (world ray), normalized on the plane.
                float3 sunDirH = normalize(float3(_SunDirection.x, 0.0, _SunDirection.z));
                float3 viewH   = normalize(float3(IN.dirWS.x, 0.0, IN.dirWS.z));
                float  azimuthAlign = saturate(dot(viewH, sunDirH));          // 1 toward the sun bearing, 0 away
                azimuthAlign = azimuthAlign * azimuthAlign;                    // tighten the warm arc around the sun
                float  horizonBand  = 1.0 - smoothstep(0.0, 0.30, saturate(dir.y)); // 1 at horizon -> 0 by ~17deg up
                col = lerp(col, _SunColor.rgb, _SunHorizonWarmth * azimuthAlign * horizonBand);

                // ---- SUN DISK (ticket 86cabc743, Erik POC item 1) ----
                // _SunDirection.xyz is the WORLD-space direction TOWARD the Sun (baked from the Sun light at
                // bootstrap — NOT the URP _MainLightPosition global, which is unbound in this pass). Dot it
                // against the WORLD-space view ray dirWS (NOT the object-space `dir` — they differ by the
                // camera rotation in the skybox pass). The dot is 1.0 looking straight at the Sun, falling
                // toward 0 away from it. Remap the [_SunSize..1] band to [0..1], pow-sharpen for a crisp toy
                // disk, then add the warm colour. Additive so the post Bloom lifts a soft warm corona.
                float3 viewWS = normalize(IN.dirWS);
                float sunDot  = saturate(dot(viewWS, normalize(_SunDirection.xyz)));
                float sunBand = saturate((sunDot - _SunSize) / max(1.0 - _SunSize, 1e-4));
                float sunMask = pow(sunBand, _SunHardness);
                // Composite the disk so its HUE SURVIVES: a pure additive (col += sun) onto the already-bright
                // blue sky clips ALL channels to ~1 at the core → the disk reads WHITE, not gold (verified in
                // the shipped capture). Instead LERP the sky toward the warm sun colour over the disk CORE
                // (the core BECOMES gold, replacing the blue) and ADD only a faint outer GLOW for the bloom
                // corona. mask^3 concentrates the lerp into the solid core; the linear mask feeds the glow.
                float core = saturate(sunMask * sunMask * sunMask);
                col = lerp(col, _SunColor.rgb, core);          // solid warm-gold disk core (hue preserved)
                col += _SunColor.rgb * sunMask * 0.25;          // faint additive corona for the bloom halo

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
