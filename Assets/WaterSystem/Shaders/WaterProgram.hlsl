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

    // VertexPositionInputs contains position in multiple spaces (world, view, homogeneous clip space)
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // Getting normal, tangent and bitangent in world space.
    VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    // Computes fog factor per-vertex.
    float fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv.xy = input.uv; // Explore TRANSFORM_TEX()
    output.uv.zw = input.uvLM.xy * unity_LightmapST.xy + unity_LightmapST.zw; // Lightmap UV

    output.positionWSAndFogFactor = float4(vertexInput.positionWS, fogFactor);
    output.normalWS = vertexNormalInput.normalWS;
    output.tangentWS = vertexNormalInput.tangentWS;
    output.bitangentWS = vertexNormalInput.bitangentWS;

#ifdef _MAIN_LIGHT_SHADOWS
    // shadow coord for the main light is computed in vertex.
    // If cascades are enabled, LWRP will resolve shadows in screen space
    // and this coord will be the uv coord of the screen space shadow texture.
    // Otherwise LWRP will resolve shadows in light space (no depth pre-pass and shadow collect pass)
    // In this case shadowCoord will be the position in light space.
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

// TODO: Remove this section after proper testing!
float2 DirectionalFlow(float2 uv, float3 flowAndSpeed, float tiling, out float2x2 rotationMatrix)
{
    //Counterclockwise Rotation Matrix 2x2, for clockwise signs of sin need to be switched
    //cos, -sin
    //sin, cos
    
    float2 dir = normalize(flowAndSpeed.xy);
    //uv = uv * 2 - 1;
    rotationMatrix = float2x2(dir.y, -dir.x, dir.x, dir.y);
    uv = mul(rotationMatrix, uv); // But rotating UV means that addition of uv is scrolling down, so in the end for clockwise direction we need to use clockwise matrix
    //uv = uv * 0.5 + 0.5;
    uv.y -= _Time.y * flowAndSpeed.z * _FlowSpeed;
    
    return uv * tiling;
}

float3 FlowCell(float2 uv, float2 offset)
{
    const float flowScale = 0.05;
	
    float2 shift = 1 - offset; // Shifting tile where is not an offset
    shift *= 0.5;
    offset *= 0.5;
    
    float2 uvTiled = (floor(uv * _GridResolution + offset) + shift) / _GridResolution;
    
    float3 flow = tex2D(_FlowMap, uvTiled).rga;
    flow.xy = flow.xy * 2.0 - 1.0;
    flow.z = length(flow.xy) * flowScale;
    
    float tilingModulated = 50;
    float tiling = flow.z * tilingModulated + _Tiling;
    float2x2 rotationMatrix;
    //float2 uvFlow = DirectionalFlow(input.uv.xy, float2(sin(_Time.y * 0.3), cos(_Time.y * 0.3)), _Tiling, rotationMatrix);
    float2 uvFlow = DirectionalFlow(uv + offset, flow, tiling, rotationMatrix);
    
    half3 dh = UnpackDerivativeHeight(tex2D(_Test, uvFlow));
    dh.xy = mul(rotationMatrix, dh.xy);
    // dh *= flow.z * HeightScaleModulated + HeightScale;
    
    return dh;
}

float3 GetDH(float2 uv)
{
    float2 t = frac(uv * _GridResolution);
    t = abs(2 * t - 1);
    float wA = (1 - t.x) * (1 - t.y);
    float wB = t.x * (1 - t.y);
    float wC = (1 - t.x) * t.y;
    float wD = t.x * t.y;
    
    float3 dhA = FlowCell(uv, float2(0, 0));
    float3 dhB = FlowCell(uv, float2(1, 0));
    float3 dhC = FlowCell(uv, float2(0, 1));
    float3 dhD = FlowCell(uv, float2(1, 1));
    float3 dh = dhA * wA + dhB * wB + dhC * wC + dhD * wD;
    
    return dh;
}
// TODO: Remove this section after proper testing!

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
    
    //
    //float3 dh = GetDH(input.uv.xy);
    //surfaceData.normalTS = normalize(float3(-dh.xy, 1));
    //surfaceData.albedo *= dh.z * dh.z;
    
    //surfaceData.albedo = half3(1, 0, 0);
    //surfaceData.normalTS = float3(0, 0, 1);
    //

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
    // Main light is the brightest directional light.
    // It is shaded outside the light loop and it has a specific set of variables and shading path
    // so we can be as fast as possible in the case when there's only a single directional light
    // You can pass optionally a shadowCoord (computed per-vertex). If so, shadowAttenuation will be
    // computed.
    Light mainLight = GetMainLight(input.shadowCoord);
#else
    Light mainLight = GetMainLight();
#endif

    // Mix diffuse GI with environment reflections.
    half3 waterColor = GlobalIllumination(brdfData, bakedGI, surfaceData.occlusion, normalWS, viewDirectionWS);

    // LightingPhysicallyBased computes direct light contribution.
    waterColor += LightingPhysicallyBased(brdfData, mainLight, normalWS, viewDirectionWS);

#ifdef _ADDITIONAL_LIGHTS
    // Returns the amount of lights affecting the object being renderer.
    // These lights are culled per-object in the forward renderer
    int additionalLightsCount = GetAdditionalLightsCount();
    for (int i = 0; i < additionalLightsCount; ++i)
    {
        // Similar to GetMainLight, but it takes a for-loop index. This figures out the
        // per-object light index and samples the light buffer accordingly to initialized the
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