// Faceted, flat-lit "crystal" look for enemies, weapons and shards. No real-time lights needed:
// a fixed light direction, an ambient floor and a fresnel rim. Supports Meta Depth API occlusion,
// so real people, walls and furniture hide virtual objects behind them.
Shader "Stopklatka/Crystal"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.86, 0.1, 0.08, 1)
        _RimColor("Rim Color", Color) = (1, 0.6, 0.55, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength("Rim Strength", Range(0, 2)) = 0.6
        _Ambient("Ambient", Range(0, 1)) = 0.55
        _LightDir("Light Direction", Vector) = (0.35, 0.85, 0.4, 0)
        _Emission("Emission", Range(0, 1)) = 0
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        // Blends against the transparent background so passthrough shows where occluded (Meta's recipe).
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _Ambient;
                float4 _LightDir;
                half _Emission;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half3 n = normalize(i.normalWS);
                half3 l = normalize(_LightDir.xyz);
                half3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half diffuse = saturate(dot(n, l)) * (1.0h - _Ambient) + _Ambient;
                half facet = 0.9h + 0.1h * n.y;
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimStrength;

                half3 color = _BaseColor.rgb * diffuse * facet + _RimColor.rgb * rim + _BaseColor.rgb * _Emission;
                half4 result = half4(color, 1.0h);
                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY_WORLDPOS(i.positionWS, result, 0);
                return result;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
