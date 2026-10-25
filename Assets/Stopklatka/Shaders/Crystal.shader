// Superhot-style faceted material for enemies, weapons, bottles and shards.
// Flat facets with a two-tone light/shadow split, a glossy highlight, a fresnel rim and a little
// darkening near the floor. No real-time lights or shadows needed, so it stays cheap at 120 Hz.
// Supports Meta Depth API occlusion (real people and furniture hide what's behind them).
// The _REVEAL variant (enemy spawn-in) cuts the mesh above a height with a glowing edge.
// Optional diagonal hatching (enemies): thin darker stripes at 45 degrees in object space, faded out
// with distance so they never shimmer. A material value, not a keyword, so there are no extra variants.
// Optional light cyan haze (level props): far surfaces fade toward _FogColor between _FogStart and
// _FogEnd metres from the eye. Also a material value (_FogStrength 0 = off), no keyword.
Shader "Stopklatka/Crystal"
{
    Properties
    {
        [MainColor] _BaseColor("Lit Color", Color) = (0.9, 0.12, 0.09, 1)
        _ShadowColor("Shadow Color", Color) = (0.45, 0.03, 0.04, 1)
        _RimColor("Rim Color", Color) = (1, 0.55, 0.5, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 3
        _RimStrength("Rim Strength", Range(0, 2)) = 0.7
        _HighlightColor("Highlight Color", Color) = (1, 0.88, 0.84, 1)
        _Gloss("Gloss", Range(4, 256)) = 48
        _SpecStrength("Highlight Strength", Range(0, 2)) = 0.6
        _LightDir("Light Direction", Vector) = (0.35, 0.85, 0.4, 0)
        _Emission("Emission", Range(0, 1)) = 0
        _FloorShade("Darkening Near Floor", Range(0, 1)) = 0.35
        _RevealY("Reveal Height (world)", Float) = 1000
        _RevealColor("Reveal Edge Color", Color) = (1, 0.85, 0.75, 1)
        _HatchColor("Hatch Color", Color) = (0.72, 0.055, 0.04, 1)
        _HatchStrength("Hatch Opacity", Range(0, 1)) = 0
        _HatchDensity("Hatch Stripes per Metre", Float) = 60
        _FogColor("Haze Color", Color) = (0.867, 0.937, 0.957, 1)
        _FogStart("Haze Start (m)", Float) = 3
        _FogEnd("Haze End (m)", Float) = 15
        _FogStrength("Haze Strength", Range(0, 1)) = 0
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
            #pragma shader_feature_local _REVEAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half4 _HighlightColor;
                half _Gloss;
                half _SpecStrength;
                float4 _LightDir;
                half _Emission;
                half _FloorShade;
                float _RevealY;
                half4 _RevealColor;
                half4 _HatchColor;
                half _HatchStrength;
                float _HatchDensity;
                half4 _FogColor;
                float _FogStart;
                float _FogEnd;
                half _FogStrength;
            CBUFFER_END

            // Set by RoomService to the floor height of the room you're in.
            float _StopklatkaFloorY;

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
                float2 hatchOS : TEXCOORD2;   // object-space x and y, for the hatching
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
                o.hatchOS = v.positionOS.xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            #if defined(_REVEAL)
                float below = _RevealY - i.positionWS.y;
                clip(below);
            #endif

                half3 n = normalize(i.normalWS);
                half3 l = normalize((half3)_LightDir.xyz);
                half3 viewDir = normalize((half3)GetWorldSpaceViewDir(i.positionWS));

                // two-tone: a soft step between the shadow and the lit color makes every facet read
                half wrap = saturate(dot(n, l) * 0.5h + 0.5h);
                half lit = smoothstep(0.3h, 0.7h, wrap);
                half3 color = lerp(_ShadowColor.rgb, _BaseColor.rgb, lit);

                // a little darker near the floor: grounds objects in the real room
                half height = (half)saturate((i.positionWS.y - _StopklatkaFloorY) / 0.6);
                color *= lerp(1.0h - _FloorShade, 1.0h, height);

                // hatching: stripe coordinate along the 45 degree diagonal, one unit per stripe.
                // fwidth (stripes per pixel) smooths the edges, and the hatching fades out as the stripes
                // shrink on screen (full above ~4 px per stripe, gone below ~1.5 px), so it only shows up close.
                float stripe = (i.hatchOS.x + i.hatchOS.y) * 0.70710678 * _HatchDensity;
                float aa = max(fwidth(stripe), 1e-4);
                float d = abs(frac(stripe) - 0.5);                     // 0 at the stripe's center
                half stripeMask = (half)(1.0 - smoothstep(0.14 - aa, 0.14 + aa, d));
                half nearFade = (half)saturate(1.6 - aa * 2.5);
                color = lerp(color, _HatchColor.rgb, stripeMask * nearFade * _HatchStrength);

                half3 h = normalize(l + viewDir);
                half spec = pow(saturate(dot(n, h)), _Gloss) * _SpecStrength;
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower) * _RimStrength;
                color += _HighlightColor.rgb * spec + _RimColor.rgb * rim + _BaseColor.rgb * _Emission;

                // light cyan haze with distance from the eye (level props only; 0 strength elsewhere)
                float eyeDistance = length(GetWorldSpaceViewDir(i.positionWS));
                half haze = (half)saturate((eyeDistance - _FogStart) / max(_FogEnd - _FogStart, 0.01)) * _FogStrength;
                color = lerp(color, _FogColor.rgb, haze);

            #if defined(_REVEAL)
                color += _RevealColor.rgb * (half)saturate(1.0 - below / 0.05) * 2.0h;
            #endif

                half4 result = half4(color, 1.0h);
                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY_WORLDPOS(i.positionWS, result, 0);
                return result;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
