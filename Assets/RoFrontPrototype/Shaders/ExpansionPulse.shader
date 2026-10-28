Shader "FrontierAtlas/ExpansionPulse"
{
    Properties { _Motion("Motion",Float)=1 }
    SubShader
    {
        Tags {"Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 timing:TEXCOORD1;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 timing:TEXCOORD1;float4 color:COLOR;};
            float _Motion;
            Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.timing=v.timing;o.color=v.color;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float age=(_Time.y-i.timing.x)/i.timing.y;
                float r=length(i.uv*2-1);
                float radius=lerp(.15,.95,saturate(age)*_Motion);
                float ring=1-smoothstep(.025,.14,abs(r-radius));
                float core=exp(-r*7)*(1-saturate(age));
                float alpha=(ring*.75+core*.5)*pow(1-saturate(age),1.6)*step(0,age)*step(age,1);
                return half4(i.color.rgb,alpha*i.color.a);
            }
            ENDHLSL
        }
    }
}
