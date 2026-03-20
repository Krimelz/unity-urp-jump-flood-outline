Shader "Hidden/Outline"
{
    SubShader
    {
        Tags
        {
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        ZTest LEqual
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        
            #define SNORM16_MAX_FLOAT_MINUS_EPSILON ((float)(32768-2) / (float)(32768-1))
            #define FLOOD_ENCODE_OFFSET float2(1.0, SNORM16_MAX_FLOAT_MINUS_EPSILON)
            #define FLOOD_ENCODE_SCALE float2(2.0, 1.0 + SNORM16_MAX_FLOAT_MINUS_EPSILON)

            #define FLOOD_NULL_POS -1.0
            #define FLOOD_NULL_POS_FLOAT2 float2(FLOOD_NULL_POS, FLOOD_NULL_POS)
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
        ENDHLSL
        
        Pass // 0
        {
            Name "InnerStencil"

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp NotEqual
                Pass Replace
            }

            ColorMask 0
            Blend Zero One

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            void Frag () {}
            
            ENDHLSL
        }

        Pass // 1
        {
            Name "BufferFill"
            
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half Frag() : SV_TARGET
            {
                return 1.0;
            }
            
            ENDHLSL
        }

        Pass // 2
        {
            Name "JumpFloodInit"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float2 Frag(Varyings input) : SV_TARGET
            {
                int2 uvInt = input.positionCS.xy;
                half3x3 values;
                
                UNITY_UNROLL
                for(int u = 0; u < 3; u++)
                {
                    UNITY_UNROLL
                    for(int v = 0; v < 3; v++)
                    {
                        uint2 sampleUV = clamp(uvInt + int2(u - 1, v - 1), int2(0, 0), (int2)_BlitTexture_TexelSize.zw - 1);
                        values[u][v] = _BlitTexture.Load(int3(sampleUV, 0)).r;
                    }
                }

                float2 outPos = input.positionCS.xy * abs(_BlitTexture_TexelSize.xy) * FLOOD_ENCODE_SCALE - FLOOD_ENCODE_OFFSET;

                if (values._m11 > 0.99)
                {
                    return outPos;
                }

                if (values._m11 < 0.01)
                {
                    return FLOOD_NULL_POS_FLOAT2;
                }
                
                float2 dir = -float2(
                    values[0][0] + values[0][1] * 2.0 + values[0][2] - values[2][0] - values[2][1] * 2.0 - values[2][2],
                    values[0][0] + values[1][0] * 2.0 + values[2][0] - values[0][2] - values[1][2] * 2.0 - values[2][2]
                );

                if (abs(dir.x) <= 0.005 && abs(dir.y) <= 0.005)
                {
                    return outPos;
                }

                dir = normalize(dir);

                float2 offset = dir * (1.0 - values._m11);

                return (input.positionCS.xy + offset) * abs(_BlitTexture_TexelSize.xy) * FLOOD_ENCODE_SCALE - FLOOD_ENCODE_OFFSET;
            }
            
            ENDHLSL
        }

        Pass // 3
        {
            Name "JumpFlood"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            int _StepWidth;

            float2 Frag(Varyings input) : SV_TARGET
            {
                int2 uvInt = int2(input.positionCS.xy);
                float bestDist = 1.#INF;
                float2 bestCoord;

                UNITY_UNROLL
                for(int u = -1; u <= 1; u++)
                {
                    UNITY_UNROLL
                    for(int v = -1; v <= 1; v++)
                    {
                        int2 offsetUV = uvInt + int2(u, v) * _StepWidth;

                        offsetUV = clamp(offsetUV, int2(0, 0), (int2)_BlitTexture_TexelSize.zw - 1);

                        float2 offsetPos = (_BlitTexture.Load(int3(offsetUV, 0)).rg + FLOOD_ENCODE_OFFSET) * _BlitTexture_TexelSize.zw / FLOOD_ENCODE_SCALE;

                        float2 disp = input.positionCS.xy - offsetPos;

                        float dist = dot(disp, disp);

                        if (offsetPos.y != FLOOD_NULL_POS && dist < bestDist)
                        {
                            bestDist = dist;
                            bestCoord = offsetPos;
                        }
                    }
                }

                return isinf(bestDist) ? FLOOD_NULL_POS_FLOAT2 : bestCoord * _BlitTexture_TexelSize.xy * FLOOD_ENCODE_SCALE - FLOOD_ENCODE_OFFSET;
            }
            
            ENDHLSL
        }

        Pass // 4
        {
            Name "JumpFloodSingleAxis"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            int2 _AxisWidth;

            half2 Frag(Varyings input) : SV_TARGET
            {
                int2 uvInt = int2(input.positionCS.xy);
                float bestDist = 1.#INF;
                float2 bestCoord;

                UNITY_UNROLL
                for(int u =- 1; u <= 1; u++)
                {
                    int2 offsetUV = uvInt + _AxisWidth * u;

                    offsetUV = clamp(offsetUV, int2(0,0), (int2)_BlitTexture_TexelSize.zw - 1);

                    float2 offsetPos = (_BlitTexture.Load(int3(offsetUV, 0)).rg + FLOOD_ENCODE_OFFSET) * _BlitTexture_TexelSize.zw / FLOOD_ENCODE_SCALE;
                    float2 disp = input.positionCS.xy - offsetPos;

                    float dist = dot(disp, disp);

                    if (offsetPos.x != -1.0 && dist < bestDist)
                    {
                        bestDist = dist;
                        bestCoord = offsetPos;
                    }
                }

                return isinf(bestDist) ? FLOOD_NULL_POS_FLOAT2 : bestCoord * _BlitTexture_TexelSize.xy * FLOOD_ENCODE_SCALE - FLOOD_ENCODE_OFFSET;
            }
            
            ENDHLSL
        }

        Pass // 5
        {
            Name "JumpFloodOutline"

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp NotEqual
                Pass Zero
                Fail Zero
            }
            
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            half4 _OutlineColor;
            float _OutlineWidth;
            
            half4 Frag(Varyings input) : SV_TARGET
            {
                int2 uvInt = int2(input.positionCS.xy);
                float2 encodedPos = _BlitTexture.Load(int3(uvInt, 0)).rg;

                if (encodedPos.y == -1)
                {
                    return half4(0, 0, 0, 0);
                }
                
                float2 nearestPos = (encodedPos + FLOOD_ENCODE_OFFSET) * abs(_ScreenParams.xy) / FLOOD_ENCODE_SCALE;
                float2 currentPos = input.positionCS.xy;

                half dist = length(nearestPos - currentPos);

                float waveParam = (nearestPos.x + nearestPos.y) * 0.05;
                half wave = sin(waveParam - _Time.y * 4.0) * 0.5 + 0.5;

                half dynamicWidth = _OutlineWidth * lerp(0.6, 1.0, wave);
                half outline = saturate(dynamicWidth - dist + 1.0);

                // затухание: 0 у края объекта, 1 у внешнего края — или наоборот
                half edgeFade = saturate(dist / max(dynamicWidth, 0.001));
                edgeFade = 1 - edgeFade;

                float4 col = _OutlineColor;
                col.a *= outline * edgeFade;

                return col;
            }
            
            ENDHLSL
        }
    }
}