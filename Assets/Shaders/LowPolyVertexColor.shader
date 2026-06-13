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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _WaveAmp;
                float _WaveLen;
                float _WaveSpeed;
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
                float3 normalWS = normalize(IN.normalWS);

                // main directional light + shadow
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float3 lit = albedo * mainLight.color * (ndotl * mainLight.shadowAttenuation);

                // ambient (SH) so shadowed facets aren't black — carries the cool ambient fill
                float3 ambient = SampleSH(normalWS) * albedo;

                float3 finalCol = lit + ambient;
                finalCol = MixFog(finalCol, IN.fogCoord);
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
