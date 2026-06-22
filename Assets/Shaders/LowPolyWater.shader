Shader "FarHorizon/LowPolyWater"
{
    // TRANSPARENT depth-fade FOAM water (ticket 86caamnmb — Erik R&D §B / Rank 3; Cyanilux depth
    // tutorials + Daniel Ilett scene-intersections + ameye.dev stylized water). A FORK of
    // FarHorizon/LowPolyVertexColor that adds intersection foam: a near-white warm band that reads
    // wherever the water surface meets ANY opaque object (beach waterline, rocks, stumps, future pier),
    // fading to clear in open water. It catches the DYNAMIC intersections the static baked FoamEdge band
    // (LowPolyZoneGen vertex-color) cannot.
    //
    // ⚠ WHY A SEPARATE SHADER, NOT AN EDIT TO LowPolyVertexColor (the hard prerequisite):
    // depth-fade foam needs to SAMPLE the camera depth texture, and an OPAQUE shader cannot sample the
    // depth it is itself writing (it writes depth in the same opaque pass it would read it). So this
    // shader is on the TRANSPARENT queue (`Queue=Transparent` + `Blend SrcAlpha OneMinusSrcAlpha` +
    // `ZWrite Off`), where the opaque scene depth (beach/rocks/seabed) is already resolved into the
    // `_CameraDepthTexture` before this draws. The opaque LowPolyVertexColor.shader STAYS for every
    // non-water surface (terrain/canopy/rock/mountain) — this is ADDITIVE, not a replacement
    // (unity-conventions.md §Build stripping "Opaque-queue water is the FPS-protecting CHOICE").
    //
    // ⚠⚠ FOG-CAP MIGRATION (the load-bearing risk): the opaque water composed with URP's Exp^2 distance
    // fog via the engine fog path AND used the in-shader `_FogCap` FLOOR to keep the far sea's teal at the
    // horizon (else the fog, coloured == SkyHorizon, washes the sea to the sky colour and the sea↔sky
    // horizon disappears). The Transparent queue REOPENS that — so the `_FogCap` fog-floor logic is PORTED
    // VERBATIM into this frag (the multi_compile_fog + ComputeFogFactor/ComputeFogIntensity + the floored
    // MixFog lerp are identical to LowPolyVertexColor). Verify the sea stays teal at the horizon via the
    // `-seaDiag` horizon-PIXEL sampler (SeaVerifyCapture.SampleHorizonBands), NOT a normal/metric.
    //
    // ⚠ OVERDRAW: full overdraw on the ~600u+ ocean extent (every transparent water pixel re-shaded over
    // whatever is behind it) — a real desktop-FPS cost. AC5 A/Bs FPS before/after on the SHIPPED dev build.
    //
    // The swell vert displacement + vertex-color teal gradient + lit/SH-ambient shading are carried over
    // unchanged from LowPolyVertexColor so the sea reads identically aside from the new foam. All
    // properties live inside CBUFFER_START(UnityPerMaterial) for SRP-Batcher compliance
    // (unity-conventions.md §SRP-Batcher). Registered in AlwaysIncludedShaders at bootstrap
    // (WorldBootstrap.EnsureShaderAlwaysIncluded) so the build does not strip it.
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _WaveAmp ("Wave Amplitude", Float) = 0
        _WaveLen ("Wave Wavelength", Float) = 14
        _WaveSpeed ("Wave Speed", Float) = 0.9
        // FOG-CAP (ported from LowPolyVertexColor, ticket 86ca9yn57). Floors this material's fog visibility
        // so the far sea keeps >= _FogCap of its own teal at the horizon (distinct from the pale sky).
        // DEFAULTS TO 0 (== full fog == bit-identical MixFog) — the water material raises it to ~0.5.
        _FogCap ("Fog Factor Floor (0=full fog)", Range(0,1)) = 0
        // DEPTH-FADE FOAM (ticket 86caamnmb AC2). _FoamColor: the warm near-white the surface lerps toward at
        // an intersection. Tuned to MATCH LowPolyZoneGen.FoamEdge (#E8E2D0, sub-1.0 so it never blooms) so
        // where the static baked band and this dynamic foam overlap they read as ONE foam line, no double-
        // bright seam (AC4). _FoamDistance: the eye-depth gap (in world units) over which the foam fades from
        // full (at the intersection) to clear (open water) — ~1.5u per Erik. Both in the cbuffer for SRP-Batcher.
        _FoamColor ("Foam Color", Color) = (0.91, 0.89, 0.82, 1)
        _FoamDistance ("Foam Distance (u)", Float) = 1.5
        // Foam alpha bias: the open water's base alpha (1 = fully opaque sea where there's no intersection).
        // Kept at 1 so the sea reads as solid teal (the transparency exists ONLY to enable depth sampling +
        // a soft foam edge — this is NOT see-through water). The foam mask only LIGHTENS the colour; alpha
        // stays 1 everywhere so the overdraw is the only transparency cost, not a visible see-through sea.
        _WaterAlpha ("Water Base Alpha", Range(0,1)) = 1
    }
    SubShader
    {
        // TRANSPARENT queue (AC1): renders AFTER opaque, so _CameraDepthTexture holds the resolved opaque
        // scene depth (beach/rocks/seabed) this frag subtracts its own depth from to find intersections.
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            // AC1: alpha-blend over the opaque scene, and DO NOT write depth (a transparent surface must not
            // occlude what's behind it in the depth buffer — and it cannot sample depth it writes).
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // DeclareDepthTexture gives SampleSceneDepth(screenUV) — the resolved OPAQUE scene depth this
            // transparent frag reads to find where the water surface meets opaque geometry (the foam mask).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _WaveAmp;
                float _WaveLen;
                float _WaveSpeed;
                float _FogCap;
                float4 _FoamColor;     // ticket 86caamnmb — depth-fade foam target colour (matches FoamEdge)
                float _FoamDistance;   // eye-depth gap over which the foam fades to clear
                float _WaterAlpha;     // base sea alpha (1 = solid teal; transparency only enables foam edge)
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float4 color       : COLOR;
                float  fogCoord    : TEXCOORD2;
                // The clip-space position carried to the frag so its W (the fragment's own eye/view depth)
                // can be subtracted from the sampled scene depth for the depth-fade foam mask. (SV_POSITION
                // is screen-space in the frag — use this copy for the original-clip-W.)
                float4 screenPos   : TEXCOORD3;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                // GENTLE SWELL (carried from LowPolyVertexColor — the water material sets _WaveAmp > 0). Two
                // crossed large-wavelength sines keyed off WORLD XZ displace Y; reads as a calm rolling sea.
                float3 posOS = IN.positionOS.xyz;
                if (_WaveAmp > 0.0)
                {
                    float3 posWS0 = TransformObjectToWorld(posOS);
                    float k = 6.2831853 / max(_WaveLen, 0.001);
                    float t = _Time.y * _WaveSpeed;
                    float wave = sin(posWS0.x * k * 0.7 + t)
                               + sin(posWS0.z * k + t * 1.3) * 0.8;
                    posOS.y += wave * _WaveAmp * 0.5;
                }

                VertexPositionInputs pos = GetVertexPositionInputs(posOS);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = nrm.normalWS;
                OUT.color       = IN.color;
                OUT.fogCoord    = ComputeFogFactor(pos.positionCS.z);
                OUT.screenPos   = ComputeScreenPos(pos.positionCS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 albedo = IN.color.rgb * _Tint.rgb;

                // smooth interpolated vertex normal (water rolls smooth — no flat-shading on the sea)
                float3 normalWS = normalize(IN.normalWS);

                // main directional light + shadow
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float3 lit = albedo * mainLight.color * (ndotl * mainLight.shadowAttenuation);

                // ambient (SH) so shadowed facets aren't black
                float3 ambient = SampleSH(normalWS) * albedo;

                float3 finalCol = lit + ambient;

                // DEPTH-FADE FOAM (ticket 86caamnmb AC2). Sample the resolved OPAQUE scene depth at this
                // fragment's screen position and convert to a linear EYE depth; subtract this fragment's OWN
                // eye depth (the screenPos.w carried from the vertex clip-W). The result is how far behind the
                // water surface the nearest opaque object is — SMALL where the water grazes an object (beach
                // waterline, rock, stump), LARGE in open water (the seabed is far below). Divide by
                // _FoamDistance + saturate → a 0→1 foam mask: 1 at the intersection, 0 in open water. Lerp the
                // sea colour toward the warm near-white _FoamColor by that mask. This catches DYNAMIC
                // intersections the static baked FoamEdge band cannot (AC2/AC4 complementary). _FoamColor is
                // tuned == FoamEdge so the static band + this dynamic foam read as ONE line where they overlap.
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEye = IN.screenPos.w;                 // this fragment's own eye depth (clip W)
                float depthGap = sceneEye - surfaceEye;            // distance to the opaque object behind
                float foam = saturate(1.0 - depthGap / max(_FoamDistance, 0.001));
                finalCol = lerp(finalCol, _FoamColor.rgb, foam);

                // FOG-CAP (ported VERBATIM from LowPolyVertexColor, ticket 86ca9yn57 — the load-bearing
                // migration risk). The Transparent queue loses the engine's opaque fog composition, so the
                // _FogCap floor is applied here manually: ComputeFogIntensity is the scene-visibility factor
                // (1 near, 0 far); flooring it at _FogCap keeps the far sea >= _FogCap of its own teal so it
                // stays distinct from the pale sky at the horizon (sea↔sky separation preserved, AC3). When
                // _FogCap=0 this is bit-identical to MixFog. Mirrors URP MixFogColor's UUM-61728 form.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    half fogIntensity = ComputeFogIntensity(IN.fogCoord);
                    fogIntensity = max(fogIntensity, _FogCap);
                    finalCol = finalCol * fogIntensity + unity_FogColor.rgb * (half(1.0) - fogIntensity);
                #endif

                // The sea is rendered transparent ONLY to enable depth sampling + the soft foam edge — it is
                // NOT see-through. Keep the base alpha at _WaterAlpha (1 = solid teal) so over-the-seabed
                // pixels are opaque; the foam mask can push alpha to 1 too (foam is opaque white). max() so a
                // sub-1 _WaterAlpha never makes the foam line see-through.
                float alpha = max(_WaterAlpha, foam);
                return half4(finalCol, alpha);
            }
            ENDHLSL
        }
        // No ShadowCaster pass: the sea does not cast shadows (its MeshRenderer sets ShadowCastingMode.Off
        // in BuildIslandWater) and a transparent ZWrite-Off surface has no meaningful shadow contribution.
    }
    // NO Fallback. The opaque URP/Lit fallback appends its own SubShaders (all Surface=Opaque →
    // Queue=Geometry/2000); Unity then resolves Shader.renderQueue (and a fresh Material's queue) to that
    // 2000 instead of this shader's authored Transparent SubShader — proven by the [water-queue-trace]
    // (subshaderCount=4, SubShader0.Queue='Transparent', yet shader.renderQueue=2000). The authored
    // ForwardLit pass is self-sufficient (Core+Lighting+DeclareDepthTexture, byte-aligned with the proven
    // LowPolyVertexColor lit path); a fallback to an OPAQUE Lit shader would also break the depth-fade
    // anyway, so dropping it is correct, not a regression (ticket 86caamnmb).
}
