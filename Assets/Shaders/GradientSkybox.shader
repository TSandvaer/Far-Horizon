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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dir         : TEXCOORD0;
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
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
