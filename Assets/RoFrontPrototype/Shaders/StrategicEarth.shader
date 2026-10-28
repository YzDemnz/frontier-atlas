Shader "FrontierAtlas/StrategicEarth"
{
    Properties
    {
        _BaseMap("Terrain",2D)="white"{}
        _TerrainData("Relief and coast distance",2D)="white"{}
        _OwnerMap("Live territories",2D)="black"{}
        _EarthAtlas("Natural Earth shaded relief",2D)="white"{}
        _UseAtlas("Geographic relief enabled",Float)=0
        _QuickMatch("Partida rápida",Float)=0
        _RadiationMap("Radiación",2D)="black"{}
        _Detail("Detail",Float)=1
        _Motion("Motion",Float)=1
        _GridScale("Grid resolution scale",Float)=1
        _SurfaceKind("Ocean surface",Float)=0
        _MapExposure("Exposición del mapa",Range(.5,1.2))=.86
        _MapContrast("Contraste del mapa",Range(.8,1.4))=1.12
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_TerrainData);SAMPLER(sampler_TerrainData);
            TEXTURE2D(_OwnerMap);SAMPLER(sampler_OwnerMap);
            TEXTURE2D(_EarthAtlas);SAMPLER(sampler_EarthAtlas);
            TEXTURE2D(_RadiationMap);SAMPLER(sampler_RadiationMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST,_OwnerMap_TexelSize,_TerrainData_TexelSize;
            float _Detail,_Motion,_GridScale,_SurfaceKind,_UseAtlas,_MapExposure,_MapContrast,_QuickMatch;
            CBUFFER_END
            Varyings vert(Attributes i){Varyings o;float3 p=i.positionOS.xyz;float t=_Time.y*_Motion;if(p.y<.01)p.y=(sin(p.x*.025+t*.6)*sin(p.z*.017-t*.42)*.2+sin(p.x*.055+p.z*.04+t*.9)*.05)*_GridScale;o.positionCS=TransformObjectToHClip(p);o.world=TransformObjectToWorld(p);o.uv=i.uv;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            float fbm(float2 p){return noise(p)*.58+noise(p*2.04)*.28+noise(p*4.11)*.14;}
            float sameRegion(float4 ids,float id,float4 weights){return dot(1-step(.0015,abs(ids-id)),weights)*step(.002,id);}
            float3 OceanColour(float3 world,float distance)
            {
                if(_QuickMatch>.5)return float3(.24,.49,.68)+noise(world.xz*.03)*.006;
                float scale=max(1,_GridScale),t=_Time.y*_Motion;
                float coast=pow(saturate(1-distance),3);
                float3 ocean=lerp(float3(.007,.026,.045),float3(.026,.091,.096),coast*.78);
                float2 p=world.xz/scale;
                float footprint=max(length(ddx(p)),length(ddy(p)));
                float coastDetail=1-smoothstep(.4,1.8,footprint);
                ocean=lerp(float3(.007,.026,.045),ocean,lerp(.25,1,coastDetail));
                float swellPhase=dot(p,float2(.74,.67))*.085-t*.58;
                float crossPhase=dot(p,float2(-.36,.93))*.14+t*.38;
                float swell=sin(swellPhase),crossWave=sin(crossPhase);
                float2 slope=cos(swellPhase)*float2(.74,.67)*.10+cos(crossPhase)*float2(-.36,.93)*.055;
                float3 normal=normalize(float3(-slope.x,1,-slope.y));
                float3 view=normalize(_WorldSpaceCameraPos-world);
                float reflected=pow(saturate(dot(normal,normalize(view+normalize(float3(-.4,1,.3))))),120);
                float fresnel=.02+.35*pow(1-saturate(dot(normal,view)),5);
                ocean+=reflected*fresnel*float3(.075,.090,.10)*_Detail;
                float variation=noise(p*.018+float2(t*.006,-t*.004))-.5;
                ocean+=variation*float3(.003,.006,.007)*_Detail;
                ocean+=(swell*.45+crossWave*.55)*float3(.0015,.003,.004)*_Detail;
                float crest=pow(saturate(swell*.65+crossWave*.35),8);
                ocean+=crest*float3(.002,.004,.005)*_Detail;
                float shore=1-smoothstep(.004,.075,distance);
                float breaker=sin(distance*58-t*1.05+sin(p.x*.055+p.y*.04)*.32);
                float foam=smoothstep(.60,.96,breaker)*shore;
                ocean=lerp(ocean,float3(.22,.29,.28),foam*.10*_Detail*coastDetail);
                return ocean;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 uv=i.uv;
                float4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
                if(_SurfaceKind>.5)clip(.52-base.a);else clip(base.a-.48);
                float4 data=SAMPLE_TEXTURE2D(_TerrainData,sampler_TerrainData,uv);
                if(_SurfaceKind>.5)return half4(OceanColour(i.world,data.g),1);
                float lighting=1;float3 land;
                if(_UseAtlas>.5)
                {
                    land=SAMPLE_TEXTURE2D_BIAS(_EarthAtlas,sampler_EarthAtlas,uv,-.35).rgb*.985;
                }
                else
                {
                    float2 texel=_TerrainData_TexelSize.xy;
                    float hL=SAMPLE_TEXTURE2D(_TerrainData,sampler_TerrainData,uv-float2(texel.x,0)).r;
                    float hR=SAMPLE_TEXTURE2D(_TerrainData,sampler_TerrainData,uv+float2(texel.x,0)).r;
                    float hD=SAMPLE_TEXTURE2D(_TerrainData,sampler_TerrainData,uv-float2(0,texel.y)).r;
                    float hU=SAMPLE_TEXTURE2D(_TerrainData,sampler_TerrainData,uv+float2(0,texel.y)).r;
                    float3 normal=normalize(float3((hL-hR)*12*_GridScale,(hD-hU)*12*_GridScale,1));
                    lighting=.67+.43*saturate(dot(normal,normalize(float3(-.6,.7,1))));
                    land=base.rgb*lighting*(.94+noise(i.world.xz*.25)*.12*_Detail);
                }
                if(_QuickMatch>.5){float2 pixel=floor(uv*_TerrainData_TexelSize.zw)*_TerrainData_TexelSize.xy;float h=SAMPLE_TEXTURE2D(_TerrainData,sampler_TerrainData, pixel).r;land=lerp(float3(.79,.80,.65),float3(.66,.77,.44),saturate(data.b));land=lerp(land,float3(.90,.88,.75),smoothstep(.44,.74,h));land*=.94+noise(floor(i.world.xz/max(1,_GridScale))*.35)*.12;lighting=1;}
                // Interpolate region coverage, never country IDs or their colours.
                // Isocontours round cell corners while retaining exact ownership at cell centres.
                float2 position=uv*_OwnerMap_TexelSize.zw-.5;
                float2 lower=floor(position),fraction=frac(position);
                float2 origin=(lower+.5)*_OwnerMap_TexelSize.xy;
                float2 size=_OwnerMap_TexelSize.xy;
                float4 o0=SAMPLE_TEXTURE2D(_OwnerMap,sampler_OwnerMap,origin);
                float4 o1=SAMPLE_TEXTURE2D(_OwnerMap,sampler_OwnerMap,origin+float2(size.x,0));
                float4 o2=SAMPLE_TEXTURE2D(_OwnerMap,sampler_OwnerMap,origin+float2(0,size.y));
                float4 o3=SAMPLE_TEXTURE2D(_OwnerMap,sampler_OwnerMap,origin+size);
                float4 weights=float4((1-fraction.x)*(1-fraction.y),fraction.x*(1-fraction.y),(1-fraction.x)*fraction.y,fraction.x*fraction.y);
                float4 ids=float4(o0.a,o1.a,o2.a,o3.a);
                float field=sameRegion(ids,o0.a,weights);float4 own=o0;
                float s1=sameRegion(ids,o1.a,weights),s2=sameRegion(ids,o2.a,weights),s3=sameRegion(ids,o3.a,weights);
                if(s1>field){field=s1;own=o1;}if(s2>field){field=s2;own=o2;}if(s3>field){field=s3;own=o3;}
                float spread=max(own.r,max(own.g,own.b))-min(own.r,min(own.g,own.b));
                float controlled=step(.002,own.a);
                float aa=max(fwidth(field),.004);
                float coverage=smoothstep(.5-aa*.65,.5+aa*.65,field)*controlled;
                land=lerp(land,own.rgb*(.84+lighting*.16),coverage*(_QuickMatch>.5?.62:lerp(.28,.58,saturate(spread*2))));
                float border=(1-smoothstep(aa*.10,aa*.72,abs(field-.5)))*controlled;
                land=lerp(land,lerp(float3(.20,.27,.29),own.rgb*1.02,saturate(spread*3)),border*.68);
                float2 scar=SAMPLE_TEXTURE2D(_RadiationMap,sampler_RadiationMap,uv).rg;
                float3 fallout=lerp(float3(.95,.005,.002),float3(1,.72,.002),smoothstep(.15,.31,scar.g));
                fallout=lerp(fallout,float3(1,.18,.002),smoothstep(.33,.53,scar.g));
                fallout=lerp(fallout,float3(.015,.24,.002),smoothstep(.55,.76,scar.g));
                fallout=lerp(fallout,float3(.003,.075,.001),smoothstep(.78,1,scar.g));
                // Opaque fallout tint retains relief, without an emissive glare or per-cell objects.
                land=lerp(land,fallout*(.63+lighting*.37)+land*.025,scar.r);
                land=saturate((land-.22)*_MapContrast+.22)*_MapExposure;
                float landMask=smoothstep(.35,.65,base.a);
                float shoreAA=max(fwidth(base.a)*.8,.012);
                float shoreCoverage=smoothstep(.5-shoreAA,.5+shoreAA,base.a);
                if(shoreCoverage<.999)land=lerp(OceanColour(i.world,data.g),land,shoreCoverage);
                return half4(land,1);
            }
            ENDHLSL
        }
    }
}
