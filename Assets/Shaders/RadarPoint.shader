Shader "Signalsight/RadarPoint"
{
    // 測距点を加算合成のソフトな円ビルボードで描く unlit シェーダ。
    // CPU からは 1 点 1 エントリの StructuredBuffer<PointData> だけが渡され、
    // 頂点シェーダが SV_VertexID から「どの点のどの角か」を引いて 4 角形に展開する。

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5   // StructuredBuffer<T> を頂点シェーダで使うのに必要
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct PointData
            {
                float3 worldPos;     // 12 byte
                int sensorId;        // 4 byte
                float timestampRel;  // 4 byte（C# 側の _epochTime からの相対秒）
                // stride 20 byte
            };

            StructuredBuffer<PointData> _Points;

            CBUFFER_START(UnityPerMaterial)
                float4 _CamRight;        // xyz = ワールド系のカメラ右方向
                float4 _CamUp;           // xyz = ワールド系のカメラ上方向
                float _PointSize;        // ビルボード半径 [m]
                float _Brightness;       // 表示ゲイン
                float _Now;              // 現在時刻（_epochTime からの相対秒）
                float _TDecay;           // 残像減衰時間 [s]
                float4 _SensorColors[16];
            CBUFFER_END

            // 1 クアッドは 6 頂点（2 三角形）。SV_VertexID % 6 で角インデックス。
            static const float2 cornerOffsets[6] = {
                float2(-1, -1),
                float2( 1, -1),
                float2( 1,  1),
                float2(-1, -1),
                float2( 1,  1),
                float2(-1,  1)
            };
            static const float2 cornerUVs[6] = {
                float2(0, 0),
                float2(1, 0),
                float2(1, 1),
                float2(0, 0),
                float2(1, 1),
                float2(0, 1)
            };

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4  color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                uint quad = IN.vertexID / 6u;
                uint corner = IN.vertexID % 6u;
                PointData p = _Points[quad];

                float2 c = cornerOffsets[corner];
                float3 worldPos = p.worldPos + (_CamRight.xyz * c.x + _CamUp.xyz * c.y) * _PointSize;
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.uv = cornerUVs[corner];

                float fade = saturate(1.0 - (_Now - p.timestampRel) / _TDecay);
                int idx = clamp(p.sensorId, 0, 15);
                OUT.color = half4(_SensorColors[idx].rgb * fade, 1.0);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 d = IN.uv - 0.5;
                half disc = saturate(1.0 - dot(d, d) * 4.0); // 中心1→縁0 のソフト円
                return half4(IN.color.rgb * disc * _Brightness, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
