// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

#ifndef WATER_VERTEX_DATA
#define WATER_VERTEX_DATA

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 uv           : TEXCOORD0;
    float2 uvLM         : TEXCOORD1;
};

struct VertexOutput
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 uv           : TEXCOORD0;
    float2 uvLM         : TEXCOORD1;
};

struct GeometryOutput
{
    float4 uv                       : TEXCOORD0; // XY: uv (unscaled), zw: uvLM
    float4 positionWSAndFogFactor   : TEXCOORD1; // XYZ: positionWS, w: vertex fog factor
    half3 normalWS                  : TEXCOORD2;
    half3 tangentWS                 : TEXCOORD3;
    half3 bitangentWS               : TEXCOORD4;

#ifdef _MAIN_LIGHT_SHADOWS
    float4 shadowCoord              : TEXCOORD5; // Compute shadow coord per-vertex for the main light
#endif
    float4 screenPos                : TEXCOORD6;
    float2 waveStrength             : TEXCOORD7;
    float2 posOffsetXZ              : TEXCOORD8; // Offset of vertex position caused by waves on XZ plane
    float4 positionCS               : SV_POSITION;
};

#endif