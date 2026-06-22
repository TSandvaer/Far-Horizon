Shader "FarHorizon/LowPolyVertexColor"
{
    // Ported verbatim (modulo the shader-name namespace: Embergrave -> FarHorizon) from the
    // eval spike (EmbergraveUnitySlice, Assets/Shaders/LowPolyVertexColor.shader, iter-5/8). A tiny
    // URP Lit-ish shader that multiplies the mesh's per-VERTEX COLOR into the albedo, so the
    // procedural low-poly terrain shows its baked sand->grass gradient WITH smooth diffuse lighting.
    // URP/Lit does NOT consume vertex color, so this is the dependency-free way to get a
    // vertex-colored + lit low-poly ground in a STRIPPED standalone build. A single directional
    // light + ambient (Spherical Harmonics) is enough for the smooth-shaded look; the averaged
    // vertex normals on the welded mesh do the rest.
    //
    // BUILD NOTE (the load-bearing reason this is a custom shader, not URP/Lit): a custom shader is
    // STRIPPED from the standalone build unless it is registered in GraphicsSettings
    // AlwaysIncludedShaders (unity-conventions.md "Build stripping & shaders" — the spike's magenta
    // failure class). WorldBootstrap.EnsureShaderAlwaysIncluded registers it at bootstrap time.
    // WATER SWELL (Far Horizon beach ocean, drew/beach-water-scene): a gentle large-wavelength
    // vertical sine displaces verts IN-SHADER (zero per-frame CPU, serializes cleanly per
    // unity-conventions.md §Editor-vs-runtime — no Awake-built/runtime-script geometry). _WaveAmp
    // DEFAULTS TO 0 so the terrain + canopy materials that share this shader are UNAFFECTED; ONLY the
    // water material sets _WaveAmp > 0. Two crossed waves (different wavelengths/directions) read as a
    // calm rolling swell, not a single mechanical ripple (Uma §1: "a breath, not surf").
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _WaveAmp ("Wave Amplitude", Float) = 0
        _WaveLen ("Wave Wavelength", Float) = 14
        _WaveSpeed ("Wave Speed", Float) = 0.9
        // FOG-CAP (ticket 86ca9yn57 — "water reads SAME as sky"). The global Exp^2 distance fog colour ==
        // WorldLookPalette.SkyHorizon (the mountain SEAM-KILL anchor). That same fog washes the FAR SEA all
        // the way to the sky colour -> the sea↔sky horizon disappears (diagnosed via -seaDiag/-verifyCoast:
        // the far-sea band read (0.80,0.88,0.91) == SkyHorizon). Mountains WANT full fog (they dissolve);
        // the SEA must NOT. _FogCap CLAMPS the fog factor for THIS material to a floor so the water never
        // fades more than (1-_FogCap) toward the sky colour, keeping its teal at the horizon. DEFAULTS TO 0
        // (== no clamp == full fog) so terrain/canopy/rock that share this shader are UNAFFECTED; ONLY the
        // water material raises it. This keeps the mountain seam-kill intact (mountains aren't this material's
        // water instance) while giving the sea its own colour — the surgical fix vs changing the global fog.
        _FogCap ("Fog Factor Floor (0=full fog)", Range(0,1)) = 0
        // FLAT-SHADING (ticket 86caamnjb — Erik R&D §A / Rank 2; Hextant Studios flat-low-poly technique).
        // When ON, frag derives the TRUE per-face normal from screen-space derivatives of positionWS
        // (normalize(cross(ddy, ddx))) instead of the interpolated vertex normal — so a WELDED, smooth-
        // normalled mesh renders the FACETED flat-shaded look WITHOUT unwelded verts or manual outward-
        // winding enforcement. DEFAULTS TO 0 (OFF) so terrain/canopy/water (smooth roll = the Zone-D dune
        // look) render BYTE-IDENTICAL to before this property existed — the OFF path is the exact prior
        // `normalize(IN.normalWS)`. Only opt-in PROPS (rocks, future Blender-MCP props) set this ON. A
        // [Toggle] drives the `_FLATSHADING_ON` keyword (a multi_compile_local variant so the ON path always
        // ships in the build — see the pragma note in the pass; the OFF variant carries zero ddx/ddy cost).
        // ADDITIVE — the existing explicit-per-face-
        // normal mesh path (FacetedRock/CloudBlob/FacetedMountain in LowPolyMeshes.cs) is UNCHANGED; this
        // is an alternative for props that prefer a welded mesh. The `_FlatShading` float lives INSIDE the
        // cbuffer below for SRP-Batcher compliance (unity-conventions.md §SRP-Batcher — any new float/color
        // must be in CBUFFER_START(UnityPerMaterial), even one only read via the keyword).
        [Toggle(_FLATSHADING_ON)] _FlatShading ("Flat Shading (per-face ddx/ddy)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            // FLAT-SHADING keyword (ticket 86caamnjb). multi_compile_local (NOT shader_feature_local — see
            // below): both the OFF and ON variants ALWAYS ship in the build, and the keyword stays LOCAL to
            // this material (not the global keyword space — unity6-mastery.md §2 "don't proliferate keywords
            // globally"). The OFF default selects the zero-ddx/ddy variant for terrain/canopy/water; opt-in
            // props switch to the ON variant at material/runtime.
            //   ⚠ WHY multi_compile, NOT shader_feature: shader_feature_local strips any variant NO BAKED
            //   MATERIAL references. This feature is OPT-IN (no material in Boot.unity enables it by default —
            //   per-prop adoption is a SEPARATE ticket), so under shader_feature the ON variant gets stripped
            //   from the build and a runtime EnableKeyword silently falls back to OFF (CONFIRMED: the first
            //   shipped -verifyFlatShading A/B rendered byte-identical OFF==ON — keyword toggled True but no
            //   ON variant existed to switch to; editor-vs-runtime shader-strip trap, the exact class AC5's
            //   shipped-capture gate exists to catch). multi_compile_local ships the ON variant unconditionally
            //   so any prop can opt in in the shipped build. Cost: one extra always-compiled variant (negligible
            //   — one tiny shader). When a baked material adopts the toggle (its own ticket), this could revert
            //   to shader_feature, but multi_compile is the correct, robust choice for the opt-in capability.
            #pragma multi_compile_local _ _FLATSHADING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _WaveAmp;
                float _WaveLen;
                float _WaveSpeed;
                float _FogCap;
                float _FlatShading;   // ticket 86caamnjb — in-cbuffer for SRP-Batcher (read via the keyword)
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
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                // GENTLE SWELL (water only — _WaveAmp 0 elsewhere so this is a no-op for terrain/canopy).
                // Displace Y by two crossed large-wavelength sines keyed off WORLD XZ (so the swell is
                // continuous across the welded grid and independent of the mesh's local origin). Phase
                // advances with _Time.y; the low frequency (long _WaveLen) + small _WaveAmp reads as a
                // calm rolling sea, never surf. Normals are left as the flat-water averaged normals — at
                // this amplitude the lighting shift is negligible and recomputing per-vertex normals
                // in-shader would cost more than the subtle swell is worth.
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
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 albedo = IN.color.rgb * _Tint.rgb;

                // NORMAL: per-face (flat-shaded) when the keyword is ON, else the interpolated vertex normal.
                // FLAT (ticket 86caamnjb): the true geometric face normal is the cross of the screen-space
                // partial derivatives of the world position — every fragment of a triangle shares one normal,
                // so a welded smooth mesh reads as hard faceted planes (Erik §A / Hextant Studios). The sign
                // is normalize(cross(ddy, ddx)) so it points toward the camera for outward (front-facing)
                // triangles under URP Cull Back — winding-inverted faces are simply culled (the whole point:
                // a flipped face is never shaded with a wrong normal, killing the winding-inversion bug class).
                // OFF (default): BYTE-IDENTICAL to the prior shader — the exact `normalize(IN.normalWS)` so
                // terrain/canopy/water are unaffected (no-regression, AC2). The keyword guards out the ddx/ddy
                // ALU entirely for the OFF variant.
                #if defined(_FLATSHADING_ON)
                    float3 normalWS = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS)));
                #else
                    float3 normalWS = normalize(IN.normalWS);
                #endif

                // main directional light + shadow
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float3 lit = albedo * mainLight.color * (ndotl * mainLight.shadowAttenuation);

                // ambient (SH) so shadowed facets aren't black — carries the cool ambient fill
                float3 ambient = SampleSH(normalWS) * albedo;

                float3 finalCol = lit + ambient;
                // FOG-CAP (water-vs-sky separation, ticket 86ca9yn57). URP fog: ComputeFogIntensity(fogCoord)
                // is the SCENE-VISIBILITY factor (1 = near/clear, 0 = far/fully fogged), and MixFog does
                // finalCol*intensity + fogColor*(1-intensity). The global Exp^2 fog drives the far sea's
                // intensity ~0, so the sea fades fully to the sky-coloured fog -> no sea↔sky horizon. _FogCap
                // is a FLOOR on the visibility so the water keeps at least _FogCap of its own teal no matter
                // how far out it is — the far sea stays a distinct colour at the horizon. When _FogCap = 0
                // (terrain/canopy/rock default) this is BIT-IDENTICAL to MixFog (max(i,0)==i) so the mountain
                // seam-kill fog is untouched (mountains still dissolve into the sky). Only the WATER material
                // raises _FogCap. The fog-keyword #if guards the no-fog variant (no fog -> plain colour); the
                // manual lerp mirrors URP MixFogColor's UUM-61728-workaround form so the capped + uncapped
                // paths match exactly (and _FogCap=0 reproduces MixFog bit-for-bit).
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    half fogIntensity = ComputeFogIntensity(IN.fogCoord);
                    fogIntensity = max(fogIntensity, _FogCap);
                    finalCol = finalCol * fogIntensity + unity_FogColor.rgb * (half(1.0) - fogIntensity);
                #endif
                return half4(finalCol, 1.0);
            }
            ENDHLSL
        }

        // shadow caster so the terrain receives + casts in the lit zones
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            V shadowVert (A IN)
            {
                V OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = cs;
                return OUT;
            }
            half4 shadowFrag (V IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
