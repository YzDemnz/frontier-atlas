Shader "FrontierAtlas/LocalWaterRipple"
{
    Properties { _LandMask("Coast coverage",2D)="black"{} _WorldSize("Map size",Vector)=(4320,2160,0,0) }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_LandMask);SAMPLER(sampler_LandMask);
            CBUFFER_START(UnityPerMaterial)
            float4 _WorldSize;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float4 colour:COLOR;};
            struct V {float4 positionCS:SV_POSITION;float4 colour:COLOR;float2 uv:TEXCOORD0;};
            V vert(A i){V o;float3 world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(world);o.uv=world.xz/_WorldSize.xy;o.colour=i.colour;return o;}
            half4 frag(V i):SV_Target {clip(.5-SAMPLE_TEXTURE2D(_LandMask,sampler_LandMask,i.uv).a);return i.colour;}
            ENDHLSL
        }
    }
}
