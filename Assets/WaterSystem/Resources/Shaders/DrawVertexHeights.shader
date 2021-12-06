Shader "Hidden/Water System/Draw Vertex Heights"
{
    SubShader
    {
        Pass
        {
            Blend One Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 relativeHeight : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float relativeHeight : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.relativeHeight = v.relativeHeight.y; // Height is in Y component
                return o;
            }

            float frag(v2f i) : SV_Target
            {
                return i.relativeHeight * 0.5 + 0.5; // Shifting to <0, 1> range
            }
            ENDCG
        }
    }
}
