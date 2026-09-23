// The sun seen from under water: a soft white-hot core in a wide, faint halo, added on top of whatever is behind (the
// ocean surface), shimmering a little. For a quad that faces the camera (Ocean Surface places it along the sun's light).
// Ignores the fog so it shows through the murk.
Shader "Out of the Depths/Sun Glow"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.97, 0.85, 1)
        _Core ("Core Brightness", Range(0, 10)) = 3
        _Halo ("Halo Brightness", Range(0, 4)) = 0.6
        _CoreSize ("Core Size", Range(0.01, 0.5)) = 0.12
        _Shimmer ("Shimmer", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SunGlow"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Core;
                float _Halo;
                float _CoreSize;
                float _Shimmer;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 d = input.uv - 0.5;
                float r = length(d) * 2.0;                  // 0 in the middle, 1 at the quad's edge
                float angle = atan2(d.y, d.x);
                float wobble = 1.0 + _Shimmer * 0.5 * (sin(angle * 7.0 + _Time.y * 1.3) + sin(angle * 13.0 - _Time.y * 0.9)) * 0.5;
                float core = saturate(1.0 - r / _CoreSize);
                core = core * core * (3.0 - 2.0 * core);
                float halo = saturate(1.0 - r);
                halo = halo * halo * halo * wobble;
                float3 colour = _Color.rgb * (core * _Core + halo * _Halo);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
