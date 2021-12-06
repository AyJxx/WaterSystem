#ifndef WATER_UNIFORMS
#define WATER_UNIFORMS

sampler2D _Test;

sampler2D _CameraDepthNormalsTexture;
float4x4 _ViewToWorldMatrix;

float _Tiling;

half4 _WaterColor1;
half4 _WaterColor2;

float _WaterOpacity;

sampler2D _NormalMap;
float _NormalScale;

half4 _UnderwaterColor;
float _UnderwaterDensity;
float _RefractionStrength;

sampler2D _FlowMap;
half _FlowSpeed;

sampler2D _InteractionFoamTex;
float4 _InteractionFoamTexTilingOffset;
float _InteractionFoamStrength;

sampler2D _DynamicFoamTex;
float4 _DynamicFoamTexTilingOffset;
float _DynamicFoamStrength;

sampler2D _StaticFoamTex;
float4 _StaticFoamTexTilingOffset;
float _StaticFoamStrength;

sampler2D _EdgeFoamTex;
float4 _EdgeFoamTexTilingOffset;
float _EdgeFoamStrength;
sampler2D _FoamWaveTex;
int _FoamWavesCount;

float _GridResolution;

float _ShoreBlendFactor;
half _EdgeFadingX;
half _EdgeFadingY;

int _DynamicWavesCount;
float _DynamicWaveAmplitude;
float4 _WaveA;
float4 _WaveB;
float4 _WaveC;

int _InteractionWavesCount;
float _InteractionWaveAmplitude;
float _InteractionWaveSpeed;
float _InteractionWaveFrequency;

sampler2D _CausticsTex;
half4 _CausticsScaleSpeed1;
half4 _CausticsScaleSpeed2;
float _CausticsStrength;
half _CausticsDepth;

sampler2D _WaterHeightMap;
float _WaterHeightMapScaleFactorXZ;
float _WaterHeightMapScaleFactorY;
float3 _MinWorldPos;

float _TessellationEdgeFactor;
float _TessellationEdgeLength;

#endif