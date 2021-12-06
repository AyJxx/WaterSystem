#ifndef WATER_PHYSICS
#define WATER_PHYSICS

#include "WaterUniforms.hlsl"

struct DynamicWaveData
{
    int enabled;
    float2 direction;
    float steepness;
    float length;
};

StructuredBuffer<DynamicWaveData> _DynamicWavesData : register(t1);

struct InteractionWaveData
{
	float3 waveHitPos;
	float waveStrength;
	float waveSpread;
	float waveFrequency;
};

StructuredBuffer<InteractionWaveData> _InteractionWavesData : register(t2);

float Test(float4 wave, float3 p, float speed, float k, inout float3 binormal, inout float3 tangent)
{
    float pi = 3.14159265358979;
    
    float2 dir = normalize(wave.xy);
    float L = wave.w;
    float A = wave.z;
    
    //dir = normalize(p.xz - 0.0);
    float w = 2 / L;
    float s = speed * (2 / L);
    
    float h = wave.z * sin(dot(dir, p.xz) * w - _Time.y * s);
    
    float bi = w * dir.x * A * cos(dot(dir, p.xz) * w - _Time.y * s);
    float ta = w * dir.y * A * cos(dot(dir, p.xz) * w - _Time.y * s);
    
    // Improved version
    
    float sinF = sin(dot(dir, p.xz) * w - _Time.y * s) + 1;
    float res = pow(sinF / 2, k);
    res *= 2 * A;
    
    float cosF = cos(dot(dir, p.xz) * w - _Time.y * s);
    float brackets = pow(sinF / 2, k - 1);
    
    float biX = k * dir.x * w * A * brackets * cosF;
    float taX = k * dir.y * w * A * brackets * cosF;
    
    //binormal = float3(1, binormal.y - bi, 0);
    //tangent = float3(0, tangent.y + ta, 1);
    
    binormal = float3(binormal.x, binormal.y - biX, binormal.z);
    tangent = float3(tangent.x, tangent.y + taX, tangent.z);
    
    return res;
}

float3 GerstnerWave(float2 waveDir, float steepness, float wavelength, float2 vertexPos, inout float3 tangent, inout float3 bitangent)
{
    float pi = 3.14159265358979;
    waveDir = normalize(waveDir);
    float w = (2 * pi) / wavelength;
    float s = sqrt(9.8 * w);
    
    float A = _DynamicWaveAmplitude;
    float Q = steepness / (w * A * max(1, _DynamicWavesCount));
    
    float WA = w * A;
    float sinF = sin(w * dot(waveDir, vertexPos.xy) - s * _Time.y); // TODO: Change to _Time.y
    float cosF = cos(w * dot(waveDir, vertexPos.xy) - s * _Time.y);
    
    float3 pos = float3(
                        Q * A * waveDir.x * cosF,
                        A * sinF,
                        Q * A * waveDir.y * cosF
    );
    
    bitangent -= float3(
                        -(Q * waveDir.x * waveDir.y * WA * sinF),
                        waveDir.y * WA * cosF,
                        -(Q * waveDir.y * waveDir.y * WA * sinF)
    );
    
    tangent += float3(
                      -(Q * waveDir.x * waveDir.x * WA * sinF),
                      waveDir.x * WA * cosF,
                      -(Q * waveDir.x * waveDir.y * WA * sinF)
    );
    
    //float3 pos = float3(
    //                    Q * waveDir.x * cosF,
    //                    Q * sinF,
    //                    Q * waveDir.y * cosF
    //);
    
    //bitangent -= float3(
    //                    -(waveDir.x * waveDir.y * steepness * sinF),
    //                    waveDir.y * WA * cosF,
    //                    -(waveDir.y * waveDir.y * steepness * sinF)
    //);
    
    //tangent += float3(
    //                  -(waveDir.x * waveDir.x * steepness * sinF),
    //                  waveDir.x * WA * cosF,
    //                  -(waveDir.x * waveDir.y * steepness * sinF)
    //);
    
    return pos;
}

void CalculateWaves(inout float3 vertexPos, inout float3 vertexNormal, inout float3 vertexTangent, 
					out float2 waveStrength, float2 uv, out float2 posOffsetXZ)
{
    float3 bitangent = cross(vertexNormal, vertexTangent);
    float3 binormal = cross(vertexTangent, vertexNormal);
    
    waveStrength.xy = 0.0;
    
    float3 finalVertexPos = vertexPos;
    float3 finalTangent = vertexTangent;
    float3 finalBitangent = bitangent;

    // Dynamic waves
#ifdef _DYNAMIC_WAVES
	
#ifdef _WAVES_DEBUG
    
    for (int i = 0; i < _DynamicWavesCount; i++)
    {
        if (i == 0)
            finalVertexPos += GerstnerWave(_WaveA.xy, _WaveA.z, _WaveA.w, vertexPos.xz, finalTangent, finalBitangent);
            //finalVertexPos.y += Test(_WaveA, pos, 2.5, 2.5, bitangent, vertexTangent);
        else if (i == 1)
            finalVertexPos += GerstnerWave(_WaveB.xy, _WaveB.z, _WaveB.w, vertexPos.xz, finalTangent, finalBitangent);
        else if (i == 2)
            finalVertexPos += GerstnerWave(_WaveC.xy, _WaveC.z, _WaveC.w, vertexPos.xz, finalTangent, finalBitangent);
    }
    
#else
    
#ifndef _VERTEX_WAVES_TEXTURE
    for (int i = 0; i < _DynamicWavesCount; i++)
    {
        DynamicWaveData waveData = _DynamicWavesData[i];
        if (waveData.enabled == 0)
            continue;
    	
        finalVertexPos += GerstnerWave(waveData.direction, waveData.steepness, waveData.length, vertexPos.xz, finalTangent, finalBitangent);
    }
#else
    for (int i = 0; i < _DynamicWavesCount; i++)
    {
        DynamicWaveData waveData = _DynamicWavesData[i];
        if (waveData.enabled == 0)
            continue;
    	
        GerstnerWave(waveData.direction, waveData.steepness, waveData.length, vertexPos.xz, finalTangent, finalBitangent);
    }
    
    float3 vertexWaves = tex2Dlod(_WaterHeightMap, float4(uv, 0, 0));
    vertexWaves.x = _MinWorldPos.x + _WaterHeightMapScaleFactorXZ * vertexWaves.x;
    vertexWaves.y = _MinWorldPos.y + _WaterHeightMapScaleFactorY * vertexWaves.y;
    vertexWaves.z = _MinWorldPos.z + _WaterHeightMapScaleFactorXZ * vertexWaves.z;
    
    finalVertexPos = vertexWaves;
#endif
    
#endif

#endif

	
    float vertexYOffset = 0.0;
    float tangentYOffset = 0.0;
    float bitangentYOffset = 0.0;

    float3 posPlusTangent = finalVertexPos + finalTangent * 0.01;
    float3 posPlusBitangent = finalVertexPos + finalBitangent * 0.01;
	
    // Waves created by interaction with water
#ifdef _INTERACTION_WAVES
    for (int i = 0; i < _InteractionWavesCount; i++)
    {
        int index = i;
        InteractionWaveData waveData = _InteractionWavesData[index];
    	
        float hitDistanceToPos = distance(waveData.waveHitPos, float3(finalVertexPos.x, 0.0, finalVertexPos.z));
        float hitDistanceToTangent = distance(waveData.waveHitPos, float3(posPlusTangent.x, 0.0, posPlusTangent.z));
        float hitDistanceToBitangent = distance(waveData.waveHitPos, float3(posPlusBitangent.x, 0.0, posPlusBitangent.z));

        float spread = waveData.waveSpread * _InteractionWaveSpeed;
        float frequency = waveData.waveFrequency * _InteractionWaveFrequency;
        float amplitude = waveData.waveStrength * _InteractionWaveAmplitude;

        float vOffset = sin((hitDistanceToPos - spread) * frequency) * amplitude;
        vOffset *= smoothstep(0, hitDistanceToPos, waveData.waveSpread);

        float tOffset = sin((hitDistanceToTangent - spread) * frequency) * amplitude;
        tOffset *= smoothstep(0, hitDistanceToTangent, waveData.waveSpread);

        float bOffset = sin((hitDistanceToBitangent - spread) * frequency) * amplitude;
        bOffset *= smoothstep(0, hitDistanceToBitangent, waveData.waveSpread);

        vertexYOffset += vOffset;
        tangentYOffset += tOffset;
        bitangentYOffset += bOffset;

        waveStrength.x = saturate(waveStrength.x + waveData.waveStrength * saturate(waveData.waveSpread - hitDistanceToPos));
        //waveStrength.x = saturate(waveStrength.x + smoothstep(0, 1, waveData.waveSpread - hitDistanceToPos) * waveData.waveStrength);
    }
#endif

    finalVertexPos.y += vertexYOffset;
    posPlusTangent.y += tangentYOffset;
    posPlusBitangent.y += bitangentYOffset;

    float3 modifiedTangent = normalize(posPlusTangent - finalVertexPos);
    float3 modifiedBitangent = normalize(posPlusBitangent - finalVertexPos);
    float3 modifiedNormal = normalize(cross(modifiedTangent, modifiedBitangent));
    
    binormal = normalize(binormal);
    float3 finalNormal = cross(modifiedTangent, modifiedBitangent); // Removed normalization
    finalTangent = normalize(modifiedTangent - finalNormal * dot(finalNormal, modifiedTangent));

	// Calculating wave strength by vertex displacement
    float displacementMin = max(0.1 * (_DynamicWaveAmplitude / 0.5), 0.01);
    float displacementMax = max(1.0 * (_DynamicWaveAmplitude / 0.5), 1.0);
    waveStrength.y = smoothstep(displacementMin, displacementMax, distance(vertexPos, finalVertexPos));
    
    posOffsetXZ = (finalVertexPos - vertexPos).xz;
    
    vertexPos = finalVertexPos;
    vertexNormal = finalNormal;
    vertexTangent = finalTangent;
    
    //
    //float noise = UnityGradientNoise(uv * _Amount);
    //float staticWaveVertexYOffset = sin(_Time.y * _WaveSpeed - vertexPos.z * _WaveFrequency) * _WaveAmplitude * noise;
    //vertexYOffset += staticWaveVertexYOffset;
    //tangentYOffset += sin(_Time.y * _WaveSpeed - vertexPos.z * _WaveFrequency) * _WaveAmplitude * noise;
    //bitangentYOffset += sin(_Time.y * _WaveSpeed - vertexPos.z * _WaveFrequency) * _WaveAmplitude * noise;
	
    //waveStrength.y = smoothstep(0.0, _WaveAmplitude * 0.5, staticWaveVertexYOffset);
}

#endif