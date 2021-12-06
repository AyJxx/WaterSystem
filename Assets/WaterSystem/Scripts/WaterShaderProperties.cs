using UnityEngine;

namespace WaterSystem
{
	/// <summary>
	/// Properties for shaders.
	/// </summary>
	public static class WaterShaderProperties
	{
		public const string TessellationEdgeFactorKeyword = "_TESSELLATION_EDGEFACTOR";
		public const string TessellationEdgeLengthKeyword = "_TESSELLATION_EDGELENGTH";
		public static readonly int TessellationEdgeFactor = Shader.PropertyToID("_TessellationEdgeFactor");
		public static readonly int TessellationEdgeLength = Shader.PropertyToID("_TessellationEdgeLength");

		public const string SpecularHighlightsOffKeyword = "_SPECULARHIGHLIGHTS_OFF";
		public const string EnvironmentReflectionsOffKeyword = "_ENVIRONMENTREFLECTIONS_OFF";
		public const string ReceiveShadowsOffKeyword = "_RECEIVE_SHADOWS_OFF";

		public static readonly int CullMode = Shader.PropertyToID("_Cull");

		public static readonly int Time = Shader.PropertyToID("_Time");
		public static readonly int ViewToWorldMatrix = Shader.PropertyToID("_ViewToWorldMatrix");

		public static readonly int WaterHeightInputTexture = Shader.PropertyToID("_WaterHeightInputTexture");
		public static readonly int WaterHeightMap = Shader.PropertyToID("_WaterHeightMap");
		public static readonly int WaterHeightMapScaleFactorXZ = Shader.PropertyToID("_WaterHeightMapScaleFactorXZ");
		public static readonly int WaterHeightMapScaleFactorY = Shader.PropertyToID("_WaterHeightMapScaleFactorY");
		public static readonly int MinWorldPos = Shader.PropertyToID("_MinWorldPos");
		public static readonly int TextureResolution = Shader.PropertyToID("_TextureResolution");

		public static readonly int Tiling = Shader.PropertyToID("_Tiling");

		public static readonly int WaterColor1 = Shader.PropertyToID("_WaterColor1");
		public static readonly int WaterColor2 = Shader.PropertyToID("_WaterColor2");

		public static readonly int WaterOpacity = Shader.PropertyToID("_WaterOpacity");

		public static readonly int UnderwaterColor = Shader.PropertyToID("_UnderwaterColor");
		public static readonly int UnderwaterDensity = Shader.PropertyToID("_UnderwaterDensity");
		public static readonly int RefractionStrength = Shader.PropertyToID("_RefractionStrength");

		public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
		public static readonly int SpecColor = Shader.PropertyToID("_SpecColor");

		public static readonly int NormalMap = Shader.PropertyToID("_NormalMap");
		public static readonly int NormalScale = Shader.PropertyToID("_NormalScale");

		public static readonly int FlowMap = Shader.PropertyToID("_FlowMap");
		public static readonly int GridResolution = Shader.PropertyToID("_GridResolution");
		public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");

		public const string InteractionFoamKeyword = "_INTERACTION_FOAM";
		public static readonly int InteractionFoamTex = Shader.PropertyToID("_InteractionFoamTex");
		public static readonly int InteractionFoamTexTilingOffset = Shader.PropertyToID("_InteractionFoamTexTilingOffset");
		public static readonly int InteractionFoamStrength = Shader.PropertyToID("_InteractionFoamStrength");

		public const string DynamicFoamKeyword = "_DYNAMIC_FOAM";
		public static readonly int DynamicFoamTex = Shader.PropertyToID("_DynamicFoamTex");
		public static readonly int DynamicFoamTexTilingOffset = Shader.PropertyToID("_DynamicFoamTexTilingOffset");
		public static readonly int DynamicFoamStrength = Shader.PropertyToID("_DynamicFoamStrength");

		public const string StaticFoamKeyword = "_STATIC_FOAM";
		public static readonly int StaticFoamTex = Shader.PropertyToID("_StaticFoamTex");
		public static readonly int StaticFoamTexTilingOffset = Shader.PropertyToID("_StaticFoamTexTilingOffset");
		public static readonly int StaticFoamStrength = Shader.PropertyToID("_StaticFoamStrength");

		public const string EdgeFoamKeyword = "_EDGE_FOAM";
		public static readonly int EdgeFoamTex = Shader.PropertyToID("_EdgeFoamTex");
		public static readonly int EdgeFoamTexTilingOffset = Shader.PropertyToID("_EdgeFoamTexTilingOffset");
		public static readonly int EdgeFoamStrength = Shader.PropertyToID("_EdgeFoamStrength");

		public const string FoamWavesKeyword = "_FOAM_WAVES";
		public static readonly int FoamWaveTex = Shader.PropertyToID("_FoamWaveTex");
		public static readonly int FoamWavesData = Shader.PropertyToID("_FoamWavesData");
		public static readonly int FoamWavesCount = Shader.PropertyToID("_FoamWavesCount");

		public const string CausticsKeyword = "_CAUSTICS";
		public static readonly int CausticsTex = Shader.PropertyToID("_CausticsTex");
		public static readonly int CausticsScaleSpeed1 = Shader.PropertyToID("_CausticsScaleSpeed1");
		public static readonly int CausticsScaleSpeed2 = Shader.PropertyToID("_CausticsScaleSpeed2");
		public static readonly int CausticsStrength = Shader.PropertyToID("_CausticsStrength");
		public static readonly int CausticsDepth = Shader.PropertyToID("_CausticsDepth");

		public static int ShoreBlendFactor = Shader.PropertyToID("_ShoreBlendFactor");

		public const string EdgeFadingKeyword = "_EDGE_FADING";
		public static readonly int EdgeFadingX = Shader.PropertyToID("_EdgeFadingX");
		public static readonly int EdgeFadingY = Shader.PropertyToID("_EdgeFadingY");

		public const string DynamicWavesKeyword = "_DYNAMIC_WAVES";
		public static readonly int DynamicWaveAmplitude = Shader.PropertyToID("_DynamicWaveAmplitude");
		public static readonly int DynamicWavesData = Shader.PropertyToID("_DynamicWavesData");
		public static readonly int DynamicWavesCount = Shader.PropertyToID("_DynamicWavesCount");

		public const  string InteractionWavesKeyword = "_INTERACTION_WAVES";
		public static readonly int InteractionWaveAmplitude = Shader.PropertyToID("_InteractionWaveAmplitude");
		public static readonly int InteractionWaveFrequency = Shader.PropertyToID("_InteractionWaveFrequency");
		public static readonly int InteractionWaveSpeed = Shader.PropertyToID("_InteractionWaveSpeed");
		public static readonly int InteractionWavesData = Shader.PropertyToID("_InteractionWavesData");
		public static readonly int InteractionWavesCount = Shader.PropertyToID("_InteractionWavesCount");
	}
}
