Shader "Hidden/Water System/DrawFlowMap"
{
    SubShader
    {
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _NoiseTex;
            float2 _FlowCoordinate;
            float2 _FlowDirection;
            half _FlowRadius;
            half _FlowSpeed;
            half _FlowBlending;
            int _GlobalFlow;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            float rand(float3 coord)
            {
                return frac(sin(dot(coord.xyz, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float noise = tex2D(_NoiseTex, i.uv);

                half2 flowVector = _FlowDirection * _FlowSpeed * 1;
                flowVector = flowVector * 0.5 + 0.5;
                half2 globalFlowVector = _FlowDirection * _FlowSpeed * 1;
                globalFlowVector = globalFlowVector * 0.5 + 0.5;

                half smoothEdge = smoothstep(_FlowRadius, 0, distance(i.uv, _FlowCoordinate));
                smoothEdge = saturate(smoothEdge * _FlowBlending);
                //half hardEdge = 1 - step(_FlowRadius, distance(i.uv, _FlowCoordinate));
                //smoothEdge = lerp(hardEdge, smoothEdge, _FlowStrength);

                fixed4 finalFlow = fixed4(flowVector, 0, smoothEdge) * (1 - _GlobalFlow);
                fixed4 globalFlow = fixed4(globalFlowVector, 0, 1) * _GlobalFlow;

                return finalFlow + globalFlow;
            }
            ENDCG
        }
    }
}
