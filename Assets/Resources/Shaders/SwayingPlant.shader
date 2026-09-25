// Seaweed that sways and parts round the player on the GPU (PlantSway.hlsl), lit like URP's Simple Lit: every pass is
// Simple Lit's own, with the vertices moved first, so the lighting, fog, shadows and depth all behave as usual.
// Seaweed uses it for its generated clumps, one combined mesh per clump; the paint (_BaseMap) is its gradient.
Shader "Out of the Depths/Swaying Plant"
{
    Properties
    {
        [MainTexture] _BaseMap ("Paint", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _SpecColor ("Specular", Color) = (0.2, 0.2, 0.2, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        [HDR] _EmissionColor ("Glow", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _EmissionMap ("Glow Map", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _Cull ("__cull", Float) = 2

        _SwayDirection ("Current Direction (xz)", Vector) = (1, 0, 0.3, 0)
        _SwayAmount ("Sway (metres at the top)", Float) = 0.25
        _SwaySpeed ("Sway Speed (cycles a second)", Float) = 0.1
        _TipLag ("Tip Lag", Range(0, 0.5)) = 0.22
        _PlantHeight ("Height (metres)", Float) = 1
        _Gust ("Gust", Float) = 0
        _PushReach ("Push Reach (metres beyond the player)", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "SimpleLit" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PlantVertex
            #pragma fragment LitPassFragmentSimple

            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ _SPECGLOSSMAP _SPECULAR_COLOR

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitForwardPass.hlsl"
            #include "PlantSway.hlsl"

            Varyings PlantVertex(Attributes input)
            {
                input.positionOS.xyz = PlantSway(input.positionOS.xyz);
                return LitPassVertexSimple(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PlantShadowVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            #include "PlantSway.hlsl"

            Varyings PlantShadowVertex(Attributes input)
            {
                input.positionOS.xyz = PlantSway(input.positionOS.xyz);
                return ShadowPassVertex(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PlantDepthVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            #include "PlantSway.hlsl"

            Varyings PlantDepthVertex(Attributes input)
            {
                input.position.xyz = PlantSway(input.position.xyz);
                return DepthOnlyVertex(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PlantDepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitDepthNormalsPass.hlsl"
            #include "PlantSway.hlsl"

            Varyings PlantDepthNormalsVertex(Attributes input)
            {
                input.positionOS.xyz = PlantSway(input.positionOS.xyz);
                return DepthNormalsVertex(input);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
