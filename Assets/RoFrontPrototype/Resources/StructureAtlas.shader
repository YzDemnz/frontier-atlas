Shader "FrontierAtlas/StructureAtlas"
{
    Properties { _BaseMap("Atlas",2D)="white"{} }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            struct A {float4 pos:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;};
            V vert(A i){UNITY_SETUP_INSTANCE_ID(i);V o;o.pos=TransformObjectToHClip(i.pos.xyz);o.normal=TransformObjectToWorldNormal(i.normal);o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target {half3 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;half light=.54+.46*saturate(dot(normalize(i.normal),normalize(float3(-.4,.85,-.3))));return half4(c*light,1);}
            ENDHLSL
        }
    }
}
