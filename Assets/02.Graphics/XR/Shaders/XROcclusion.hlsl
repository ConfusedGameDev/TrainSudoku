// XR-PRD 5.6: hides a fragment behind the player's real hands, arms and furniture, using the headset's environment
// depth. AR Foundation's ARShaderOcclusion publishes the depth texture, the per-eye matrices that take tracking space
// into it, and the parameters that turn its depth into metres; it also turns on XR_HARD_OCCLUSION once depth arrives.
// XRDepthOcclusion adds _XRWorldToTrackables, because the matrices are in tracking space and shaders work in world
// space, and _XROcclusionBias, the tolerance that keeps the depth map's fuzz from nibbling at the board.
#ifndef TSUGI_XR_OCCLUSION_INCLUDED
#define TSUGI_XR_OCCLUSION_INCLUDED

#if defined(XR_HARD_OCCLUSION) || defined(XR_SOFT_OCCLUSION)

TEXTURE2D_ARRAY(_EnvironmentDepthTexture);
SAMPLER(sampler_EnvironmentDepthTexture);
float4x4 _EnvironmentDepthProjectionMatrices[2];
float4 _NdcLinearConversionParameters;
float4x4 _XRWorldToTrackables;
float _XROcclusionBias;

uint XROcclusionEye()
{
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    return unity_StereoEyeIndex;
#else
    return 0;
#endif
}

// Symmetric-range NDC depth ([-1, 1]) to metres from the depth camera.
float XRDepthInMetres(float symmetricNdcDepth)
{
    return _NdcLinearConversionParameters.x / (symmetricNdcDepth + _NdcLinearConversionParameters.y);
}

// Discards the fragment when something real is nearer to the eye than it, by more than the bias.
void XRClipOccluded(float3 positionWS)
{
    uint eye = XROcclusionEye();
    float4 depthCS = mul(_EnvironmentDepthProjectionMatrices[eye], mul(_XRWorldToTrackables, float4(positionWS, 1.0)));
    if (depthCS.w <= 0.0) return;

    float3 ndc = depthCS.xyz / depthCS.w;
    float2 uv = ndc.xy * 0.5 + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0)) return;

    // The texture holds non-symmetric [0, 1] depth; the fragment's own depth is already symmetric NDC.
    float environment = SAMPLE_TEXTURE2D_ARRAY(_EnvironmentDepthTexture, sampler_EnvironmentDepthTexture, uv, (float)eye).r;
    clip(XRDepthInMetres(environment * 2.0 - 1.0) - XRDepthInMetres(ndc.z) + _XROcclusionBias);
}

#else

void XRClipOccluded(float3 positionWS) {}

#endif

#endif
