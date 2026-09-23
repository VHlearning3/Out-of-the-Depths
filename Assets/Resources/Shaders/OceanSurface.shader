// The underside of the ocean surface, seen from below: a bright shifting web of light (layered moving ripples, all
// maths, no texture), brightest straight overhead and fading off towards the horizon and with distance from the camera.
// It ignores the scene fog on purpose, so it reads through the murk, but is mixed towards the fog colour by Murk.
// Ocean Surface (the component) keeps its quad over the camera.
Shader "Out of the Depths/Ocean Surface"
{
    Properties
    {
        _Color ("Colour", Color) = (0.55, 0.85, 1, 1)
        _Brightness ("Brightness", Range(0, 4)) = 1.3
        _RippleScale ("Ripple Size (per metre)", Range(0.02, 2)) = 0.35
        _RippleSpeed ("Ripple Speed", Range(0, 3)) = 0.6
        _FadeDistance ("Fade Out By (metres from the camera)", Float) = 120
        _Murk ("Murk (0 = clear, 1 = all fog colour)", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "OceanSurface"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Brightness;
                float _RippleScale;
                float _RippleSpeed;
                float _FadeDistance;
                float _Murk;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            // Bright thin lines where waves of several directions cross: a caustic-like web.
            float Web(float2 p, float t)
            {
                float w = sin(p.x * 1.3 + t) + sin(p.y * 1.7 - t * 1.2)
                        + sin((p.x + p.y) * 0.9 + t * 0.7) + sin((p.x - p.y) * 2.3 - t * 1.5);
                float line1 = pow(saturate(1.0 - abs(w) * 0.55), 6.0);
                float2 q = p * 1.9 + float2(3.1, 7.7);
                float v = sin(q.x * 1.1 - t * 0.9) + sin(q.y * 1.4 + t * 1.1) + sin((q.x - q.y) * 1.7 + t * 0.6);
                float line2 = pow(saturate(1.0 - abs(v) * 0.6), 5.0);
                return saturate(line1 + line2 * 0.6);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 toPoint = input.positionWS - _WorldSpaceCameraPos;
                float distance = length(toPoint);
                float3 view = toPoint / max(distance, 0.001);

                float t = _Time.y * _RippleSpeed;
                float2 p = input.positionWS.xz * _RippleScale;
                float web = Web(p, t);

                // Brightest straight up (the bright window overhead), dim towards the horizon.
                float overhead = saturate(abs(view.y));
                overhead = overhead * overhead;

                float3 colour = _Color.rgb * _Brightness * (0.35 + web * 1.2) * (0.35 + overhead * 0.9);
                colour = lerp(colour, unity_FogColor.rgb, _Murk);

                float horizontal = length(toPoint.xz);
                float alpha = saturate(1.0 - horizontal / max(_FadeDistance, 1.0)) * saturate(0.25 + overhead);
                return half4(colour, alpha * _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
