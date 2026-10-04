// VisionOS-Agent, 2026-09-23: the surface a world-space UI Toolkit panel is shown on, on Vision Pro.
//
// On this headset a world-space UIDocument does not render in world space at all — it comes out as a flat overlay
// locked to the head, while the sign's posts (ordinary meshes) stand correctly on the platform. So the panel is
// rendered into a texture instead and drawn here, on a real mesh, which is stereo-correct and writes depth like any
// other object. That also settles the reprojection question by itself: an opaque, depth-writing surface is exactly
// what visionOS's compositor needs, with no separate depth proxy.
//
// Alpha-clipped rather than blended, so the card's rounded corners are cut away while every surviving pixel still
// writes depth. Blending would put the card back in the transparent pass with no depth, which is what shimmered.
//
// Single-pass instanced stereo safe: visionOS renders both eyes in one pass into a two-slice target, and a shader
// without the stereo macros writes slice 0 only — the left eye.
Shader "TrainSudoku/VisionOS/Panel Surface"
{
    Properties
    {
        // [MainTexture] is not decoration: without it Material.mainTexture writes _MainTex, which this shader does
        // not declare, and _BaseMap keeps its "white" default — a solid white card. Every URP shader tags _BaseMap
        // the same way for the same reason.
        [MainTexture] _BaseMap ("Panel", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "PanelSurface"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off   // the mesh carries both faces, each with its own UVs, so the card reads from either side

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 panel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(panel.a - _Cutoff);
                return half4(panel.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
