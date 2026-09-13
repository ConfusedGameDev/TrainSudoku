// XR-PRD 5.6: the invisible surface under the board that lets the board, its pieces, the train and the player's hands
// cast shadows onto the real table. It draws nothing but the main light's shadow: black, with its alpha scaled by how
// much of that light is blocked, so over passthrough it darkens the table where a shadow falls and leaves it untouched
// everywhere else. Hidden behind the player's real body like the board, so a shadow never draws over a hand.
// Single-pass instanced stereo safe, as Quest renders both eyes in one pass.
Shader "TrainSudoku/XR/Shadow Catcher"
{
    Properties
    {
        _ShadowColor ("Shadow Colour", Color) = (0, 0, 0, 0.45)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ShadowCatcher"
            Tags { "LightMode" = "UniversalForward" }

            // Straight alpha for colour, additive coverage for alpha, so the eye buffer's alpha (what passthrough is
            // blended by) comes out as the shadow's opacity rather than its square.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ XR_HARD_OCCLUSION XR_SOFT_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "XROcclusion.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                XRClipOccluded(input.positionWS);
                half lit = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
                return half4(_ShadowColor.rgb, _ShadowColor.a * (1.0h - lit));
            }
            ENDHLSL
        }
    }
}
