Shader "Custom/FishSwim"
{
    Properties
    {
        [MainTexture] _MainTex("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0,2)) = 1
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.5

        [Header(Swim Animation)]
        _SwimAxis("Body Axis (Head-Tail Direction)", Vector) = (0,1,0,0)
        _SwayAxis("Sway Axis (Side-to-Side Direction)", Vector) = (1,0,0,0)
        _HeadAnchor("Head Position On Axis", Float) = 0
        _HeadDamp("Body Length From Head (mask reaches 1 here)", Range(0.01, 3)) = 1.0
        _TailPower("Tail Curve Sharpness (higher = stiffer head, whippier tail)", Range(0.5, 6)) = 2.5
        _Frequency("Wave Frequency", Range(0, 20)) = 6
        _Speed("Wave Speed", Range(0, 10)) = 2
        _Amplitude("Wave Amplitude", Range(0, 0.3)) = 0.045
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _NormalScale;
                half _Metallic;
                half _Smoothness;
                float4 _SwimAxis;
                float4 _SwayAxis;
                float _HeadAnchor;
                float _HeadDamp;
                float _TailPower;
                float _Frequency;
                float _Speed;
                float _Amplitude;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                float fogFactor : TEXCOORD6;
            };

            // Displaces the body sideways with a head-to-tail traveling sine wave.
            // headMask uses abs() so it works regardless of which way the body
            // extends from _HeadAnchor along _SwimAxis. _TailPower reshapes the
            // 0->1 ramp so the head stays stiff longer and the tail tip whips
            // harder, instead of the whole rear half moving at one flat amplitude.
            void ApplySwim(inout float3 positionOS, inout float3 normalOS)
            {
                float3 swimAxis = normalize(_SwimAxis.xyz);
                float3 swayAxis = normalize(_SwayAxis.xyz);

                float bodyCoord = dot(positionOS, swimAxis);
                float t = saturate(abs(bodyCoord - _HeadAnchor) / max(_HeadDamp, 0.0001));
                float headMask = pow(t, _TailPower);

                float phase = bodyCoord * _Frequency + _Time.y * _Speed;
                float wave = sin(phase);

                positionOS += swayAxis * (wave * _Amplitude * headMask);

                // Shear the normal by the wave's spatial derivative so lighting
                // follows the bend instead of looking flat/wrong.
                float derivative = cos(phase) * _Frequency * _Amplitude * headMask;
                normalOS = normalize(normalOS + swayAxis * derivative);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionOS = IN.positionOS.xyz;
                float3 normalOS = IN.normalOS;
                ApplySwim(positionOS, normalOS);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(normalOS, IN.tangentOS);

                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = normalInputs.normalWS;
                OUT.tangentWS = float4(normalInputs.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.shadowCoord = GetShadowCoord(positionInputs);
                OUT.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _BaseColor;

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _NormalScale);
                float3 bitangentWS = cross(IN.normalWS, IN.tangentWS.xyz) * IN.tangentWS.w;
                float3x3 tangentToWorld = float3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = normalize(IN.viewDirWS);
                inputData.shadowCoord = IN.shadowCoord;
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedoAlpha.rgb;
                surfaceData.alpha = albedoAlpha.a;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.emission = 0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half _NormalScale;
                half _Metallic;
                half _Smoothness;
                float4 _SwimAxis;
                float4 _SwayAxis;
                float _HeadAnchor;
                float _HeadDamp;
                float _TailPower;
                float _Frequency;
                float _Speed;
                float _Amplitude;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            void ApplySwim(inout float3 positionOS)
            {
                float3 swimAxis = normalize(_SwimAxis.xyz);
                float3 swayAxis = normalize(_SwayAxis.xyz);
                float bodyCoord = dot(positionOS, swimAxis);
                float t = saturate(abs(bodyCoord - _HeadAnchor) / max(_HeadDamp, 0.0001));
                float headMask = pow(t, _TailPower);
                float phase = bodyCoord * _Frequency + _Time.y * _Speed;
                positionOS += swayAxis * (sin(phase) * _Amplitude * headMask);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionOS = IN.positionOS.xyz;
                ApplySwim(positionOS);

                float3 positionWS = TransformObjectToWorld(positionOS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
