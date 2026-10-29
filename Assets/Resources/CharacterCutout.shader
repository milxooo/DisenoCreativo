Shader "RobaColores/CharacterCutout"
{
    Properties
    {
        _MainTex ("Artwork", 2D) = "white" {}
        _ArtworkUVRect ("Artwork Crop", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ArtworkUVRect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _ArtworkUVRect.zw + _ArtworkUVRect.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 background = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(0.001, 0.001), 0).rgb;
                half distanceFromBackground = distance(color.rgb, background);
                color.a *= smoothstep(0.035, 0.12, distanceFromBackground);
                return color;
            }
            ENDHLSL
        }
    }
}
