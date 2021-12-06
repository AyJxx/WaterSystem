#ifndef WATER_VISUAL
#define WATER_VISUAL

#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

#include "WaterUtilities.hlsl"
#include "WaterUniforms.hlsl"

struct FoamWaveData
{
    float2 pos;
    float amount;
    float speed;
    float min;
    float max;
    float2 uvMin;
};

StructuredBuffer<FoamWaveData> _FoamWavesData : register(t0);

half3 UnpackScaleNormal(half4 packednormal, half bumpScale)
{
    half3 normal;
    normal.xy = (packednormal.wy * 2 - 1);
	normal.xy *= bumpScale;
    normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
    return normal;
}

float3 UnpackDerivativeHeight(float4 textureData)
{
    float3 dh = textureData.agb;
    dh.xy = dh.xy * 2 - 1; // Normals need to be shifted to range (-1, 1)
    return dh;
}

void CalculateFlowWeights(float2 uv, out float weights[4])
{
    float2 t = frac(uv * _GridResolution);
    t = abs(2 * t - 1);
    weights[0] = (1 - t.x) * (1 - t.y);
    weights[1] = t.x * (1 - t.y);
    weights[2] = (1 - t.x) * t.y;
    weights[3] = t.x * t.y;
}

float3 CalculateFlowVector(float2 uv, float2 offset)
{
    const float flowScale = 0.05;
	
    float2 shift = 1 - offset; // Shifting tile where is not an offset
    shift *= 0.5;
    offset *= 0.5;
    
    float2 uvTile = (floor(uv * _GridResolution + offset) + shift) / _GridResolution;
    
    float3 flow = tex2D(_FlowMap, uvTile).rga;
    flow.xy = flow.xy * 2.0 - 1.0;
    flow.z = length(flow.xy) * flowScale;
    
    return flow;
}

float2 CalculateUvFlow(float2 uv, float3 flowAndSpeed, float tiling, out float2x2 rotationMatrix)
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

float2 CalculateUvFlow(float2 uv, float2 offset, out float2x2 rotationMatrix)
{
    float3 flowAndSpeed = CalculateFlowVector(uv, offset);
    float tilingModulated = 50;
    float tiling = flowAndSpeed.z * tilingModulated + _Tiling;
    //float2 uvFlow = DirectionalFlow(input.uv.xy, float2(sin(_Time.y * 0.3), cos(_Time.y * 0.3)), _Tiling, rotationMatrix);
    return CalculateUvFlow(uv + offset, flowAndSpeed, tiling, rotationMatrix);
}

float3 SampleDerivativeHeightMapByFlow(sampler2D sampledTexture, float2 uvsFlow[4], float2x2 rotationMatrices[4], float weights[4])
{
    float3 texData1 = UnpackDerivativeHeight(tex2D(sampledTexture, uvsFlow[0]));
    float3 texData2 = UnpackDerivativeHeight(tex2D(sampledTexture, uvsFlow[1]));
    float3 texData3 = UnpackDerivativeHeight(tex2D(sampledTexture, uvsFlow[2]));
    float3 texData4 = UnpackDerivativeHeight(tex2D(sampledTexture, uvsFlow[3]));

    texData1.xy = mul(rotationMatrices[0], texData1.xy);
    texData2.xy = mul(rotationMatrices[1], texData2.xy);
    texData3.xy = mul(rotationMatrices[2], texData3.xy);
    texData4.xy = mul(rotationMatrices[3], texData4.xy);

    //texData1 *= flow.z * HeightScaleModulated + HeightScale;
	//...
    
    float3 texData = texData1 * weights[0] + texData2 * weights[1] + texData3 * weights[2] + texData4 * weights[3];
    
    return texData;
}

float3 SampleNormalMapByFlow(sampler2D sampledTexture, float normalScale, float2 uvsFlow[4], float2x2 rotationMatrices[4], float weights[4])
{
    float3 normal1 = UnpackScaleNormal(tex2D(sampledTexture, uvsFlow[0]), normalScale);
    float3 normal2 = UnpackScaleNormal(tex2D(sampledTexture, uvsFlow[1]), normalScale);
    float3 normal3 = UnpackScaleNormal(tex2D(sampledTexture, uvsFlow[2]), normalScale);
    float3 normal4 = UnpackScaleNormal(tex2D(sampledTexture, uvsFlow[3]), normalScale);

    normal1.xy = mul(rotationMatrices[0], normal1.xy);
    normal2.xy = mul(rotationMatrices[1], normal2.xy);
    normal3.xy = mul(rotationMatrices[2], normal3.xy);
    normal4.xy = mul(rotationMatrices[3], normal4.xy);

    //normal1 *= flow.z * HeightScaleModulated + HeightScale;
	//...
    
    return normal1 * weights[0] + normal2 * weights[1] + normal3 * weights[2] + normal4 * weights[3];
}

half3 SampleColorTextureByFlow(sampler2D sampledTexture, float4 tilingAndOffset, float2 uvsFlow[4], float weights[4])
{
    half3 color1 = tex2D(sampledTexture, uvsFlow[0] * tilingAndOffset.xy + tilingAndOffset.zw);
    half3 color2 = tex2D(sampledTexture, uvsFlow[1] * tilingAndOffset.xy + tilingAndOffset.zw);
    half3 color3 = tex2D(sampledTexture, uvsFlow[2] * tilingAndOffset.xy + tilingAndOffset.zw);
    half3 color4 = tex2D(sampledTexture, uvsFlow[3] * tilingAndOffset.xy + tilingAndOffset.zw);

    //color1 *= flow.z * HeightScaleModulated + HeightScale;
	//...
    
    return color1 * weights[0] + color2 * weights[1] + color3 * weights[2] + color4 * weights[3];
}

float CalculateWaterColorGradientParameter(float4 tilingAndOffset, float2 uvsFlow[4], float weights[4])
{
	// TODO: Make this parametrized
    half noise1 = smoothstep(0, 1, UnityGradientNoise(uvsFlow[0] * tilingAndOffset.xy + tilingAndOffset.zw));
    half noise2 = smoothstep(0, 1, UnityGradientNoise(uvsFlow[1] * tilingAndOffset.xy + tilingAndOffset.zw));
    half noise3 = smoothstep(0, 1, UnityGradientNoise(uvsFlow[2] * tilingAndOffset.xy + tilingAndOffset.zw));
    half noise4 = smoothstep(0, 1, UnityGradientNoise(uvsFlow[3] * tilingAndOffset.xy + tilingAndOffset.zw));
	
    return noise1 * weights[0] + noise2 * weights[1] + noise3 * weights[2] + noise4 * weights[3];
}

float2 CalculateFlowUV(float2 uv, float2 flowVector, float distortionOffset, half flowSpeed, out float blendingFactor, float noise)
{
    float2 uvFlow;
    float progress = frac(distortionOffset + noise + _Time.y * flowSpeed);
    uvFlow.xy = (uv - flowVector * progress) * _Tiling;
    uvFlow.xy += distortionOffset;
	
    blendingFactor = 1 - abs(1 - 2 * progress);
	
    return uvFlow;
}

half3 CalculateFoamColor(float2 uvUnscaled, float3 positionWS, float3 normalTS, float2 flowVector, float shoreLineFactor, float2 waveStrength, float2 posOffsetXZ, 
    float2 uvsFlow[4], float weights[4])
{
#if _INTERACTION_FOAM
    half3 interactionFoam = waveStrength.x * _InteractionFoamStrength * SampleColorTextureByFlow(_InteractionFoamTex, _InteractionFoamTexTilingOffset, uvsFlow, weights);
#else
    half3 interactionFoam = 0.0;
#endif

	
#if _DYNAMIC_FOAM
    half3 dynamicFoam = waveStrength.y * _DynamicFoamStrength * SampleColorTextureByFlow(_DynamicFoamTex, _DynamicFoamTexTilingOffset, uvsFlow, weights);
#else
    half3 dynamicFoam = 0.0;
#endif

	
#if _STATIC_FOAM
    float noise1 = smoothstep(0, 0.5, UnityGradientNoise(uvsFlow[0] * _StaticFoamTexTilingOffset.xy)) * weights[0];
    float noise2 = smoothstep(0, 0.5, UnityGradientNoise(uvsFlow[1] * _StaticFoamTexTilingOffset.xy)) * weights[1];
    float noise3 = smoothstep(0, 0.5, UnityGradientNoise(uvsFlow[2] * _StaticFoamTexTilingOffset.xy)) * weights[2];
    float noise4 = smoothstep(0, 0.5, UnityGradientNoise(uvsFlow[3] * _StaticFoamTexTilingOffset.xy)) * weights[3];
    float noise = noise1 + noise2 + noise3 + noise4;
	
    half3 staticFoam = noise * _StaticFoamStrength * SampleColorTextureByFlow(_StaticFoamTex, _StaticFoamTexTilingOffset, uvsFlow, weights);
#else
    half3 staticFoam = 0.0;
#endif

	
#if _EDGE_FOAM
    half3 edgeFoam = shoreLineFactor * _EdgeFoamStrength * SampleColorTextureByFlow(_EdgeFoamTex, _EdgeFoamTexTilingOffset, uvsFlow, weights);
#else
    half3 edgeFoam = 0.0;
#endif

	
    //float2 testUV = input.uv;
    //testUV = testUV * 2 - 1;
    //testUV *= 10 * (unity_gradientNoise(input.uv.xy * _Amount) + 0.5);
    //testUV += 5 * (unity_gradientNoise(input.uv.xy * _Amount) + 0.5);
    //float2 flowVector = testUV - 0.0;
    //testUV += flowVector * abs(sin(_Time.y));
    //testUV = testUV * 0.5 + 0.5;
    half3 foamWave = 0.0;

#ifdef _FOAM_WAVES
    for (int i = 0; i < _FoamWavesCount; i++)
    {
        FoamWaveData foam = _FoamWavesData[i];
        float2 pos = foam.pos.xy + posOffsetXZ;
        
        // Distance compared on XZ plane (foam.pos is already in XZ)
        float2 dir = (positionWS.xz - (pos + flowVector * foam.amount * 1 * _FlowSpeed));
        float dist = distance(positionWS.xz, pos + flowVector * foam.amount * 1 * _FlowSpeed);
        
        if (foam.amount > dist)
        {
            float distFactor = dist / foam.amount; // By increasing amount foam wave circle shape is scaled up
            
            float2 uv = 0.0; // This is center, we are in range (-1, 1)
            uv += normalize(dir) * distFactor;
            uv += normalTS.xy * _RefractionStrength * 2; // Applying distortion (offsetting UVs)
            uv = uv * 0.5 + 0.5; // Shifting to (0, 1) range
            
            // Applying offset to sample correct tile in a texture
            uv.x = lerp(foam.uvMin.x, foam.uvMin.x + 0.5, uv.x);
            uv.y = lerp(foam.uvMin.y, foam.uvMin.y + 0.5, uv.y);
            
            half foamCol = tex2D(_FoamWaveTex, uv).a;
            foamCol *= (1 - abs(1 - 2 * smoothstep(foam.min, foam.max, foam.amount))); // Shifting range (0, 1) -> (0, 1, 0)
            foamWave = saturate(foamWave + foamCol);
        }
    }
#endif
    
    return edgeFoam + staticFoam + dynamicFoam + interactionFoam + foamWave;
}

half3 CalculateWaterColor(float2 uvUnscaled, float2 flowVector, float2 uvsFlow[4], float weights[4], out half3 normalTS)
{
    half3 waterCol = 0.0;
    
    //float2 mainUV = uvUnscaled * _Noise_ST.xy + _Noise_ST.zw;
    float2 mainUV = uvUnscaled;

    float noise = smoothstep(0, 0.5, UnityGradientNoise(mainUV));

    half3 gradientParameter = CalculateWaterColorGradientParameter(float4(1, 1, 0, 0), uvsFlow, weights);

    float blendingFactor1, blendingFactor2;
    float2 uvFlow1 = CalculateFlowUV(mainUV, flowVector, 0.0, _FlowSpeed, blendingFactor1, noise); // unscaledUV
    float2 uvFlow2 = CalculateFlowUV(mainUV, flowVector, 0.5, _FlowSpeed, blendingFactor2, noise);

    half water1 = smoothstep(0, 0.5, UnityGradientNoise(uvFlow1)) * blendingFactor1;
    half water2 = smoothstep(0, 0.5, UnityGradientNoise(uvFlow2)) * blendingFactor2;
    waterCol = (water1 + water2) * lerp(_WaterColor2.rgb, _WaterColor1.rgb, gradientParameter);
    
    half3 normal1 = UnpackScaleNormal(tex2D(_NormalMap, uvFlow1), _NormalScale) * blendingFactor1;
    half3 normal2 = UnpackScaleNormal(tex2D(_NormalMap, uvFlow2), _NormalScale) * blendingFactor2;
    normalTS = normalize(normal1 + normal2);
    
    //float3 dh = SampleDerivativeHeightMapByFlow(_Test, uvsFlow, rotationMatrices, weights);
    //normalTS = normalize(float3(-dh.xy, 1));
    
    //normalTS = normalize(float3(SampleNormalMapByFlow(_NormalMap, _NormalScale, uvsFlow, rotationMatrices, weights).xyz));
    
    //waterCol = dh.z * dh.z;
    waterCol = lerp(_WaterColor2.rgb, _WaterColor1.rgb, gradientParameter);
    
    return waterCol;
}

inline void InitializeSurfaceData(float2 uv, float2 flowVector, float2 uvsFlow[4], float weights[4], out SurfaceData outSurfaceData)
{
    float3 normalTS;
    half3 albedo = CalculateWaterColor(uv, flowVector, uvsFlow, weights, normalTS);
    outSurfaceData.alpha = _WaterOpacity;

    outSurfaceData.albedo = albedo;

    outSurfaceData.metallic = 1.0h;
    outSurfaceData.specular = _SpecColor.rgb;
    //outSurfaceData.metallic = _Metallic;
    //outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);

    outSurfaceData.smoothness = _Smoothness;
    outSurfaceData.normalTS = normalTS;
    outSurfaceData.occlusion = 1.0;
    outSurfaceData.emission = 0.0;
	
    outSurfaceData.clearCoatMask = 0.0;
    outSurfaceData.clearCoatSmoothness = 0.0;
}

inline void InitializeBRDFDataCustom(half3 albedo, half metallic, half3 specular, half smoothness, half alpha, out BRDFData outBRDFData)
{
    half reflectivity = ReflectivitySpecular(specular);
    half oneMinusReflectivity = 1.0 - reflectivity;
    outBRDFData.diffuse = albedo * (half3(1.0h, 1.0h, 1.0h) - specular);
    outBRDFData.specular = specular;
    
    //half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
    //half reflectivity = 1.0 - oneMinusReflectivity;
    //outBRDFData.diffuse = albedo * oneMinusReflectivity;
    //outBRDFData.specular = lerp(kDieletricSpec.rgb, albedo, metallic);
    
    outBRDFData.grazingTerm = saturate(smoothness + reflectivity);
    outBRDFData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    outBRDFData.roughness = max(PerceptualRoughnessToRoughness(outBRDFData.perceptualRoughness), HALF_MIN);
    outBRDFData.roughness2 = outBRDFData.roughness * outBRDFData.roughness;

    outBRDFData.normalizationTerm = outBRDFData.roughness * 4.0h + 2.0h;
    outBRDFData.roughness2MinusOne = outBRDFData.roughness2 - 1.0h;

    outBRDFData.reflectivity = 1.0;

#ifdef _ALPHAPREMULTIPLY_ON
    outBRDFData.diffuse *= alpha;
    alpha = alpha * oneMinusReflectivity + reflectivity;
#endif
}

half CalculateAlpha(float depth, float screenPosW, float2 uvUnscaled, out float shoreLineFactor)
{
    float depthDifference = saturate(depth - screenPosW);
    shoreLineFactor = 1.0 - depthDifference;
    half alpha = saturate(depthDifference * _ShoreBlendFactor);
    
#ifdef _EDGE_FADING
	// Calculating edge fading
	float2 fadingXY = 1.0 - abs(uvUnscaled.xy * 2 - 1);
	int enableFadingX = saturate(1.0 - ceil(_EdgeFadingX)); // If edgeFadingX is set to 0 we want to use this variable to push calculated fading value over 1 to not perform fading based on edges
    int enableFadingY = saturate(1.0 - ceil(_EdgeFadingY)); // Same as above but for Y coordinates

    _EdgeFadingX /= 10.0;
    _EdgeFadingY /= 10.0;

	// Taking lower value to perform horizontal or vertical fading
	//fadingXY.x * max(0.0, 10.0 - edgeFadingX) + enableFadingX
	float fading = min(
		smoothstep(_EdgeFadingX, _EdgeFadingX + 0.01, fadingXY.x) + enableFadingX,
		smoothstep(_EdgeFadingY, _EdgeFadingY + 0.01, fadingXY.y) + enableFadingY
	);

    alpha = min(alpha, fading);
#endif

    return alpha;
}

half3 CalculateCaustics(float3 fragPosWS, float3 fragNormalWS)
{
    half3 blend = abs(fragNormalWS);
    blend /= dot(blend, 1.0);
                
    float2 speed1 = float2(_Time.y * _CausticsScaleSpeed1.z, _Time.y * _CausticsScaleSpeed1.w);
    half3 cx1 = tex2D(_CausticsTex, fragPosWS.yz * _CausticsScaleSpeed1.xy + speed1).rgb;
    half3 cy1 = tex2D(_CausticsTex, fragPosWS.xz * _CausticsScaleSpeed1.xy + speed1).rgb;
    half3 cz1 = tex2D(_CausticsTex, fragPosWS.xy * _CausticsScaleSpeed1.xy + speed1).rgb;
    half3 caustics1 = cx1 * blend.x + cy1 * blend.y + cz1 * blend.z;

    float2 speed2 = float2(_Time.y * _CausticsScaleSpeed2.z, _Time.y * _CausticsScaleSpeed2.w);
    half3 cx2 = tex2D(_CausticsTex, fragPosWS.yz * _CausticsScaleSpeed2.xy + speed2).rgb;
    half3 cy2 = tex2D(_CausticsTex, fragPosWS.xz * _CausticsScaleSpeed2.xy + speed2).rgb;
    half3 cz2 = tex2D(_CausticsTex, fragPosWS.xy * _CausticsScaleSpeed2.xy + speed2).rgb;
    half3 caustics2 = cx2 * blend.x + cy2 * blend.y + cz2 * blend.z;

    half3 caustics = min(caustics1, caustics2);

    return caustics;
}

half3 CalculateUnderwaterColorAndAlpha(float4 screenPos, float3 positionWS, half3 normalTS, float2 uvUnscaled, 
										out float shoreLineFactor, out half alpha)
{
    float2 uvOffset = normalTS.xy * _RefractionStrength;
    float2 uv = (screenPos.xy + uvOffset) / screenPos.w; // Final depth texture coordinates, division by screenPos.w is perspective division

    float backgroundDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); // Sampling depth texture and converting values to linear depth relative to screen
    float waterDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(screenPos.z); // Distance between water and screen, converting to linear depth (screenPos.z is interpolated clip space depth)
    float depthDifference = backgroundDepth - waterDepth;

	// Smoothing fragments which are not underwater
    uvOffset *= saturate(depthDifference);
    uv = (screenPos.xy + uvOffset) / screenPos.w;

	// Linear depth of fragment under water
    backgroundDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
    depthDifference = backgroundDepth - waterDepth;

    half3 backgroundColor = SampleSceneColor(uv).rgb;
    float fogFactor = exp2(-_UnderwaterDensity * depthDifference);
    
    alpha = CalculateAlpha(backgroundDepth, screenPos.w, uvUnscaled, shoreLineFactor);
    
    half3 causticsColor = 0.0;

#ifdef _CAUSTICS
    // Caustics related calculations
    float3 fragNormalVS = normalize(DecodeNormal(tex2D(_CameraDepthNormalsTexture, screenPos.xy / screenPos.w)));
    float3 fragNormalWS = mul(_ViewToWorldMatrix, float4(fragNormalVS, 0.0)).xyz;

    // Not rendering caustics on normals facing wrong direction
    float normalDirectionFade = dot(fragNormalWS, float3(0, 1, 0));
    float waterHeightFrag = positionWS.y;

    // Camera ray is used to calculate world position of the fragment under water
    float3 cameraRay = positionWS - GetCameraPositionWS();
    cameraRay /= screenPos.w; // Perspective division
    float3 fragPosWS = GetCameraPositionWS() + cameraRay * backgroundDepth;
	
    // Depth fade for caustics
    float depthFade = 1.0 - saturate((waterHeightFrag - fragPosWS.y) / _CausticsDepth);

    // Calculate caustics color
    if (normalDirectionFade > -0.1)
        causticsColor += CalculateCaustics(fragPosWS, fragNormalWS) * depthFade * _CausticsStrength;
#endif
    
    return lerp(_UnderwaterColor.rgb, backgroundColor + causticsColor, fogFactor);
}

#endif