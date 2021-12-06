#ifndef WATER_TESSELLATION
#define WATER_TESSELLATION

#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL) || defined(SHADER_API_PSSL)
#define UNITY_CAN_COMPILE_TESSELLATION 1
#define UNITY_domain                 domain
#define UNITY_partitioning           partitioning
#define UNITY_outputtopology         outputtopology
#define UNITY_patchconstantfunc      patchconstantfunc
#define UNITY_outputcontrolpoints    outputcontrolpoints
#endif

struct TessellationFactors
{
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

float TessellationEdgeFactor(float3 p1, float3 p2)
{
#ifdef _TESSELLATION_EDGEFACTOR
    return _TessellationEdgeFactor;
#else
    float edgeLength = distance(p1, p2);

    float3 edgeCenter = (p1 + p2) * 0.5;
    float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);
    
    return edgeLength * _ScreenParams.y / (_TessellationEdgeLength * viewDistance);
#endif
}

TessellationFactors PatchConstantFunction(InputPatch<Attributes, 3> patch)
{
    TessellationFactors f;
    
    float3 worldPos1 = mul(unity_ObjectToWorld, patch[0].positionOS).xyz;
    float3 worldPos2 = mul(unity_ObjectToWorld, patch[1].positionOS).xyz;
    float3 worldPos3 = mul(unity_ObjectToWorld, patch[2].positionOS).xyz;
    
    f.edge[0] = TessellationEdgeFactor(worldPos2, worldPos3);
    f.edge[1] = TessellationEdgeFactor(worldPos3, worldPos1);
    f.edge[2] = TessellationEdgeFactor(worldPos1, worldPos2);
    f.inside = (TessellationEdgeFactor(worldPos2, worldPos3) + TessellationEdgeFactor(worldPos3, worldPos1) + TessellationEdgeFactor(worldPos1, worldPos2)) 
                / 3.0;
    return f;
}

[UNITY_domain("tri")]
[UNITY_outputcontrolpoints(3)]
[UNITY_outputtopology("triangle_cw")]
[UNITY_partitioning("fractional_odd")] // integer/fractional_odd/fractional_even
[UNITY_patchconstantfunc("PatchConstantFunction")]
Attributes HullPass(InputPatch<Attributes, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

VertexOutput TessellateVertex(Attributes v)
{
    VertexOutput output;
    output.positionOS = v.positionOS;
    output.normalOS = v.normalOS;
    output.tangentOS = v.tangentOS;
    output.uv = v.uv;
    output.uvLM = v.uvLM;
    return output;
}

[UNITY_domain("tri")]
VertexOutput DomainPass(TessellationFactors factors, OutputPatch<Attributes, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
{
    VertexOutput v;

    // Interpolating 3 vertices by weights set in barycentricCoordinates
#define DOMAIN_PROGRAM_INTERPOLATION(fieldName) v.fieldName = \
		patch[0].fieldName * barycentricCoordinates.x + \
		patch[1].fieldName * barycentricCoordinates.y + \
		patch[2].fieldName * barycentricCoordinates.z;

	DOMAIN_PROGRAM_INTERPOLATION(positionOS)
	DOMAIN_PROGRAM_INTERPOLATION(normalOS)
	DOMAIN_PROGRAM_INTERPOLATION(tangentOS)
	DOMAIN_PROGRAM_INTERPOLATION(uv)
	DOMAIN_PROGRAM_INTERPOLATION(uvLM)

    return TessellateVertex(v);
}

#endif