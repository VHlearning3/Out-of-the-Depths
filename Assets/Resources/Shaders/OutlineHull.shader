// Inverted-hull outline for URP: the mesh drawn again with its back faces only, grown outward by Thickness metres,
// in a flat colour. Whatever is in front of it (the object itself) hides the inside, leaving a rim.
// OutlineHull.cs builds the copies and sets _Center (the mesh's own middle) per object; PlayerInteractor owns the
// material. Grow From Centre 1 gives clean rims on boxes and simple props; 0 follows the surface normals instead.
Shader "Out of the Depths/Outline Hull"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Thickness ("Thickness (metres)", Range(0, 0.1)) = 0.012
        _CentreBias ("Grow From Centre (1) / Along Normals (0)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineHull"
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Thickness;
                float _CentreBias;
            CBUFFER_END

            float4 _Center;   // object-space centre of the mesh, per renderer (MaterialPropertyBlock)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 centreWS = TransformObjectToWorld(_Center.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                float3 fromCentre = positionWS - centreWS;
                float len = length(fromCentre);
                fromCentre = len > 1e-5 ? fromCentre / len : normalWS;
                float3 dir = normalize(lerp(normalWS, fromCentre, _CentreBias));
                output.positionCS = TransformWorldToHClip(positionWS + dir * _Thickness);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_Color.rgb, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
