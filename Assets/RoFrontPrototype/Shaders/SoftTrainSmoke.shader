Shader "FrontierAtlas/SoftTrainSmoke"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 vertex:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            struct V{float4 position:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            V Vert(A input){V output;output.position=TransformObjectToHClip(input.vertex.xyz);output.color=input.color;output.uv=input.uv;return output;}
            half4 Frag(V input):SV_Target{half falloff=1-smoothstep(.05h,1.h,length(input.uv*2-1));return half4(input.color.rgb,input.color.a*falloff*falloff);}
            ENDHLSL
        }
    }
}
