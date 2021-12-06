// Copyright (c) Adam Jůva.
// Licensed under the MIT License.

Shader "Water System/Water Surface"
{
    Properties
    {
        [Header(Water)]
        [Space(10)]
        [HideInInspector] _WaterScale ("Water Scale", Float) = 1.0
        [HideInInspector] _WaterColor1 ("Water Color 1", Color) = (1, 1, 1, 1)
        [HideInInspector] _WaterColor2 ("Water Color 2", Color) = (1, 1, 1, 1)

        [HideInInspector] _WaterOpacity ("Water Opacity", Range(0.0, 1.0)) = 0.75

        [HideInInspector] _UnderwaterColor("Underwater Color", Color) = (0, 0, 0, 0)
        [HideInInspector] _UnderwaterDensity("Underwater Density", Range(0, 5)) = 0.75
        [HideInInspector] _RefractionStrength("Refraction Strength", Range(0, 1)) = 0.25

        [Space(10)]
        [HideInInspector] _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _SpecColor("Specular Color", Color) = (0.2, 0.2, 0.2)

        [HideInInspector] [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        [HideInInspector] _NormalScale("Normal Scale", Float) = 1.0
        [Space(10)]
		
        [Header(Flow)]
        [Space(10)]
		[HideInInspector] [NoScaleOffset] _FlowMap("Flow (R, G)", 2D) = "bump" {}
		[HideInInspector] _Tiling("Global Tiling", Float) = 1
        [HideInInspector] _GridResolution("Grid Resolution", Float) = 5.0
		[HideInInspector] _FlowSpeed("Flow Speed", Float) = 1
        [Space(10)]

        [Header(Foam)]
        [Space(10)]
        [HideInInspector] [Toggle(_INTERACTION_FOAM)] _InteractionFoam("Interaction Foam", Float) = 1
        [HideInInspector] _InteractionFoamTex("Interaction Foam", 2D) = "black" {}
        [HideInInspector] _InteractionFoamStrength("Interaction Foam Strength", Range(0, 1)) = 0.75

        [Space(10)]
        [HideInInspector] [Toggle(_DYNAMIC_FOAM)] _DynamicFoam("Dynamic Foam", Float) = 1
        [HideInInspector] _DynamicFoamTex("Dynamic Foam", 2D) = "black" {}
        [HideInInspector] _DynamicFoamStrength("Dynamic Foam Strength", Range(0, 1)) = 0.5

        [Space(10)]
        [HideInInspector] [Toggle(_STATIC_FOAM)] _StaticFoam("Static Foam", Float) = 1
        [HideInInspector] _StaticFoamTex("Static Foam", 2D) = "black" {}
        [HideInInspector] _StaticFoamStrength("Static Foam Strength", Range(0, 1)) = 0.5

        [Space(10)]
        [HideInInspector] [Toggle(_EDGE_FOAM)] _EdgeFoam("Edge Foam", Float) = 1
		[HideInInspector] _EdgeFoamTex ("Edge Foam", 2D) = "black" {}
		[HideInInspector] _EdgeFoamStrength ("Edge Foam Strength", Range(0, 1)) = 0.1
		
        [Space(10)]
        [HideInInspector] [Toggle(_FOAM_WAVES)] _FoamWaves("Foam Waves", Float) = 1
        [HideInInspector] [NoScaleOffset]_FoamWaveTex ("Foam Wave", 2D) = "black" {}
        [HideInInspector] [Space(10)]

        [Header(Caustics)]
        [Space(10)]
        [HideInInspector] [Toggle(_CAUSTICS)] _Caustics("Caustics", Float) = 1
        [HideInInspector] [NoScaleOffset]_CausticsTex("Caustics", 2D) = "black" {}
        [HideInInspector] _CausticsScaleSpeed1("Caustics Scale (X, Y), Speed (Z, W)", Vector) = (1, 1, 0.15, 0.15)
        [HideInInspector] _CausticsScaleSpeed2("Caustics Scale (X, Y), Speed (Z, W)", Vector) = (2, 2, -0.25, -0.25)
        [HideInInspector] _CausticsStrength("Caustics Strength", Range(0, 100)) = 5.0
        [HideInInspector] _CausticsDepth("Caustics Depth", Float) = 5.0
        [Space(10)]

        [Header(Edge Blending)]
        [Space(10)]
		[HideInInspector] _ShoreBlendFactor ("Shore Blend Factor", Range(1, 100)) = 1.0

        [HideInInspector] [Toggle(_EDGE_FADING)] _EdgeFading("Edge Fading", Float) = 0
        [HideInInspector] _EdgeFadingX("Edge Fading X", Range(0, 10.0)) = 0.0
        [HideInInspector] _EdgeFadingY("Edge Fading Y", Range(0, 10.0)) = 0.0
        [Space(10)]

        [Header(Waves)]
        [Space(10)]
        [HideInInspector] [Toggle(_DYNAMIC_WAVES)] _DynamicWaves("Dynamic Waves", Float) = 1
		[HideInInspector] _DynamicWaveAmplitude ("Dynamic Wave Amplitude", Float) = 1.0

        [Space(10)]
        [HideInInspector] [Toggle(_INTERACTION_WAVES)] _InteractionWaves("Interaction Waves", Float) = 1
		[HideInInspector] _InteractionWaveAmplitude ("Interaction Wave Amplitude", Float) = 0.4
		[HideInInspector] _InteractionWaveFrequency ("Interaction Wave Frequency", Float) = 10.0
		[HideInInspector] _InteractionWaveSpeed ("Interaction Wave Speed", Float) = 1.25
        [Space(10)]

        [Header(Tessellation)]
        [Space(10)]
        [HideInInspector] [KeywordEnum(EdgeFactor, EdgeLength)] _Tessellation ("Tessellation", Float) = 0
        [HideInInspector] _TessellationEdgeFactor ("Tesselation Edge Factor", Float) = 1.0
        [HideInInspector] _TessellationEdgeLength ("Tesselation Edge Length", Float) = 1.0

        [Space(20)]
        [HideInInspector] [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [HideInInspector] [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        [HideInInspector] [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1.0

        [Space(10)]
        [HideInInspector] [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Face", Float) = 2.0
    }

    SubShader
    {
        Tags{"Queue" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline" "IgnoreProjector" = "True"}
        LOD 300

        Pass
        {
            Name "StandardLit"
            Tags{"LightMode" = "UniversalForward"}

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma require geometry
            #pragma require tessellation tessHW
            
            #pragma target 3.5

            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature _RECEIVE_SHADOWS_OFF

            // Universal Render Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #pragma shader_feature _INTERACTION_FOAM
            #pragma shader_feature _DYNAMIC_FOAM
            #pragma shader_feature _STATIC_FOAM
            #pragma shader_feature _EDGE_FOAM
            #pragma shader_feature _FOAM_WAVES
            #pragma shader_feature _EDGE_FADING
            #pragma shader_feature _DYNAMIC_WAVES
            #pragma shader_feature _INTERACTION_WAVES
            #pragma shader_feature _CAUSTICS
            #pragma shader_feature _TESSELLATION_EDGEFACTOR _TESSELLATION_EDGELENGTH

            #pragma vertex LitPassVertex
            #pragma geometry LitPassGeometry
            #pragma fragment LitPassFragment
            #pragma hull HullPass
            #pragma domain DomainPass

            // Includes Unity built-in shader variables (except the lighting variables) https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Includes lighting shader variables, lighting and shadow functions.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "LitInput.hlsl"
            #include "WaterUtilities.hlsl"
            #include "WaterVertexData.hlsl"
            #include "WaterTessellation.hlsl"
            #include "WaterPhysics.hlsl"
            #include "WaterVisual.hlsl"
            #include "WaterProgram.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma shader_feature _ALPHATEST_ON

            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "LitInput.hlsl"
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #include "LitInput.hlsl"
            #include "DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // Used for Baking GI. This pass is stripped from build.
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
}