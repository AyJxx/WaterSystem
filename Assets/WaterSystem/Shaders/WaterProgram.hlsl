// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

#ifndef WATER_PROGRAM
#define WATER_PROGRAM

#include "WaterVertexData.hlsl"
#include "WaterUniforms.hlsl"
#include "WaterPhysics.hlsl"
#include "WaterVisual.hlsl"

Attributes LitPassVertex(Attributes input)
{
    return input; // Just forwarding vertices to hull stage
}

GeometryOutput CalculateVertex(VertexOutput input)
{
    GeometryOutput output;
    CalculateWaves(input.positionOS.xyz, input.normalOS, input.tangentOS.xyz, output.waveStrength.xy, input.uv.xy, output.posOffsetXZ);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    // Computes fog factor per-vertex.
    float fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv.xy = input.uv;
    output.uv.zw = input.uvLM.xy * unity_LightmapST.xy + unity_LightmapST.zw; // Lightmap UV

    output.positionWSAndFogFactor = float4(vertexInput.positionWS, fogFactor);
    output.normalWS = vertexNormalInput.normalWS;
    output.tangentWS = vertexNormalInput.tangentWS;
    output.bitangentWS = vertexNormalInput.bitangentWS;

#ifdef _MAIN_LIGHT_SHADOWS
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif
    
    output.screenPos = ComputeScreenPos(vertexInput.positionCS);
    output.positionCS = vertexInput.positionCS;
    return output;
}

[maxvertexcount(3)]
void LitPassGeometry(uint primitiveID : SV_PrimitiveID, triangle VertexOutput input[3], inout TriangleStream<GeometryOutput> triStream)
{
    GeometryOutput output1 = CalculateVertex(input[0]);
    GeometryOutput output2 = CalculateVertex(input[1]);
    GeometryOutput output3 = CalculateVertex(input[2]);

    triStream.Append(output1);
    triStream.Append(output2);
    triStream.Append(output3);
}

half4 LitPassFragment(GeometryOutput input) : SV_Target
{
    float2 uvUnscaled = input.uv.xy;
    float2 uvLM = input.uv.zw;
    float3 positionWS = input.positionWSAndFogFactor.xyz;
    float fogFactor = input.positionWSAndFogFactor.w;
    half3 viewDirectionWS = SafeNormalize(GetCameraPositionWS() - positionWS);

    const float flowScale = 0.05;
    float2 flowMap = tex2D(_FlowMap, uvUnscaled).rg;
    float2 flowVector = flowMap * 2.0 - 1.0; // Shifting to the -1 to 1 range
    flowVector *= flowScale;

    float2 uvsFlow[4];
    float2x2 rotationMatrices[4];
    uvsFlow[0] = CalculateUvFlow(uvUnscaled, float2(0, 0), rotationMatrices[0]);
    uvsFlow[1] = CalculateUvFlow(uvUnscaled, float2(1, 0), rotationMatrices[1]);
    uvsFlow[2] = CalculateUvFlow(uvUnscaled, float2(0, 1), rotationMatrices[2]);
    uvsFlow[3] = CalculateUvFlow(uvUnscaled, float2(1, 1), rotationMatrices[3]);

    float weights[4];
    CalculateFlowWeights(uvUnscaled, weights);
    
    SurfaceData surfaceData;
    InitializeSurfaceData(uvUnscaled, flowVector, uvsFlow, weights, surfaceData);

    half3 normalWS = TransformTangentToWorld(surfaceData.normalTS, half3x3(input.tangentWS, input.bitangentWS, input.normalWS));
    normalWS = normalize(normalWS);

#ifdef LIGHTMAP_ON
    // Normal is required in case Directional lightmaps are baked
    half3 bakedGI = SampleLightmap(uvLM, normalWS);
#else
    // Samples SH fully per-pixel. SampleSHVertex and SampleSHPixel functions are also defined in case you want to sample some terms per-vertex.
    half3 bakedGI = SampleSH(normalWS);
#endif

    // BRDFData holds energy conserving diffuse and specular material reflections and its roughness.
    BRDFData brdfData;
    InitializeBRDFDataCustom(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);

    // Light struct contains light direction, color, distanceAttenuation and shadowAttenuation.
#ifdef _MAIN_LIGHT_SHADOWS
    Light mainLight = GetMainLight(input.shadowCoord);
#else
    Light mainLight = GetMainLight();
#endif

    // Mix diffuse GI with environment reflections.
    half3 waterColor = GlobalIllumination(brdfData, bakedGI, surfaceData.occlusion, normalWS, viewDirectionWS);

    // LightingPhysicallyBased computes direct light contribution.
    waterColor += LightingPhysicallyBased(brdfData, mainLight, normalWS, viewDirectionWS);

#ifdef _ADDITIONAL_LIGHTS
    int additionalLightsCount = GetAdditionalLightsCount();
    for (int i = 0; i < additionalLightsCount; ++i)
    {
        // Light struct. If _ADDITIONAL_LIGHT_SHADOWS is defined it will also compute shadows.
        Light light = GetAdditionalLight(i, positionWS);

        // Same functions used to shade the main light.
        waterColor += LightingPhysicallyBased(brdfData, light, normalWS, viewDirectionWS);
    }
#endif

    float shoreLineFactor;
    half3 underwaterColor = CalculateUnderwaterColorAndAlpha(input.screenPos, positionWS, surfaceData.normalTS, uvUnscaled, shoreLineFactor, surfaceData.alpha);
    half3 foamColor = CalculateFoamColor(uvUnscaled, positionWS, surfaceData.normalTS, flowVector, shoreLineFactor, input.waveStrength.xy, input.posOffsetXZ,
        uvsFlow, weights);
	
    // Combine water color and underwater color
    half3 color = lerp(underwaterColor, waterColor, _WaterOpacity);
	
    // Combine with foam color
    color = saturate(color + foamColor);
	
    // Combine with fog (if any)
    color = MixFog(color, fogFactor);
    
    return half4(color, surfaceData.alpha);
}

#endif