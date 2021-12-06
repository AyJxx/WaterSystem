﻿#ifndef WATER_UTILITIES
#define WATER_UTILITIES

float2 UnityGradientNoiseDir(float2 p)
{
    p = p % 289;
    float x = (34 * p.x + 1) * p.x % 289 + p.y;
    x = (34 * x + 1) * x % 289;
    x = frac(x / 41) * 2 - 1;
    return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
}

float UnityGradientNoise(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    float d00 = dot(UnityGradientNoiseDir(ip), fp);
    float d01 = dot(UnityGradientNoiseDir(ip + float2(0, 1)), fp - float2(0, 1));
    float d10 = dot(UnityGradientNoiseDir(ip + float2(1, 0)), fp - float2(1, 0));
    float d11 = dot(UnityGradientNoiseDir(ip + float2(1, 1)), fp - float2(1, 1));
    fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
    return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x);
}

float3 DecodeNormal(float4 enc)
{
    float kScale = 1.7777;
    float3 nn = enc.xyz * float3(2 * kScale, 2 * kScale, 0) + float3(-kScale, -kScale, 1);
    float g = 2.0 / dot(nn.xyz, nn.xyz);
    float3 n;
    n.xy = g * nn.xy;
    n.z = g - 1;
    return n;
}

#endif