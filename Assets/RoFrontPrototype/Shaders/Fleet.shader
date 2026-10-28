Shader "FrontierAtlas/Fleet"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;float4 color:COLOR;};
            struct V {float4 pos:SV_POSITION;float4 color:COLOR;};
            V vert(A i){V o;o.pos=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color;return o;}
            half4 frag(V i):SV_Target{return half4(i.color.rgb*.96,1);}
            ENDHLSL
        }
    }
}
