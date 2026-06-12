Shader "FarHorizon/BlobShadowVertexColor"
{
    // A tiny UNLIT, TRANSPARENT, vertex-color shader for the castaway's contact/blob shadow
    // (ticket 86ca8ca1m — "blob shadow re-fit"). The blob-shadow disc mesh (LowPolyMeshes.BlobShadowDisc)
    // bakes a soft radial falloff into per-vertex ALPHA (opaque core -> transparent rim); this shader
    // renders that as a soft dark ground decal under the chunky castaway. UNLIT so the shadow reads the
    // same regardless of the key light (a fake contact shadow, not a lit surface); TRANSPARENT so the
    // vertex-alpha falloff blends over the ground.
    //
    // BUILD NOTE (same load-bearing reason as LowPolyVertexColor.shader): a custom shader is STRIPPED
    // from the standalone build unless registered in GraphicsSettings AlwaysIncludedShaders
    // (unity-conventions.md "Build stripping & shaders" — the magenta failure class). The scene author
    // (MovementCameraScene) calls EnsureShaderAlwaysIncluded on it so it ships.
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // RGB = shadow tone * tint; ALPHA = the disc's baked radial falloff * tint alpha.
                return half4(IN.color.rgb * _Tint.rgb, IN.color.a * _Tint.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
