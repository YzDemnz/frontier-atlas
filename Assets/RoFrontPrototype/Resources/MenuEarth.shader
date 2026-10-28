Shader "FrontierAtlas/MenuEarth"
{
 Properties { _MainTex("Tierra",2D)="white"{} _Aspect("Aspecto",Float)=1.7778 _Rotation("RotaciÃ³n",Float)=0 _CloudTime("Nubes",Float)=0 }
 SubShader { Tags { "RenderType"="Opaque" } Cull Off ZWrite Off ZTest Always
 Pass { HLSLPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float _Aspect,_Rotation,_CloudTime;
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 float cloud(float2 p){return .46*noise(p)+.25*noise(p*2.03)+.17*noise(p*4.11)+.08*noise(p*8.13)+.04*noise(p*16.3);}
 fixed4 frag(v2f_img i):SV_Target
 {
   float2 uv=i.uv;
   float3 col=lerp(float3(.003,.007,.016),float3(.012,.025,.045),uv.y);
   float2 starUV=uv*float2(_Aspect,1)*650;
   float star=step(.9985,hash(floor(starUV)))*pow(saturate(1-length(frac(starUV)-.5)*2),5);
   col+=star*.35;
   // Orthographic ray/sphere intersection: a real globe, not a panning image.
   float radius=min(.40,_Aspect*.235);
   float2 p=(uv-float2(.73,.52))*float2(_Aspect,1)/radius;
   float r2=dot(p,p),r=sqrt(r2);
   float halo=exp(-max(0,r-1)*26)*smoothstep(.97,1.015,r);
   col+=float3(.04,.18,.32)*halo*.45;
   if(r2<1)
   {
     float3 n=float3(p,sqrt(1-r2));
     float tilt=.4091;float3 local=float3(n.x*cos(tilt)-n.y*sin(tilt),n.x*sin(tilt)+n.y*cos(tilt),n.z);
     float longitude=atan2(local.x,local.z)+_Rotation*6.2831853;
     float2 unwrappedUV=float2(longitude/6.2831853+.5,asin(clamp(local.y,-1,1))/3.14159265+.5);
     float2 mapUV=float2(frac(unwrappedUV.x),clamp(unwrappedUV.y,.0001,.9999));
     // Derivatives before wrapping prevent a blurry vertical strip at longitude 180°.
     float3 surface=tex2Dgrad(_MainTex,mapUV,ddx(unwrappedUV),ddy(unwrappedUV)).rgb;
     float ocean=smoothstep(.005,.09,surface.b-surface.r);
     surface=lerp(surface*float3(.72,.79,.73),float3(.018,.105,.19)+surface*.10,ocean);
     float diffuse=dot(n,normalize(float3(-.65,.48,.63)));
     float day=smoothstep(-.12,.22,diffuse);
     col=surface*(.025+max(0,diffuse)*1.05)*day+float3(.003,.009,.019)*(1-day);
     float clouds=smoothstep(.59,.77,cloud(float2(sin(longitude)*9+cos(longitude)*5+_CloudTime*.002,local.y*13+cos(longitude)*4)));
     clouds*=smoothstep(.04,.18,abs(local.y))*(1-smoothstep(.9,1,abs(local.y)));
     col=lerp(col,float3(.70,.77,.80)*(.04+max(0,diffuse)*.87),clouds*.68);
     float rim=pow(1-n.z,3.4);
     col+=float3(.055,.27,.48)*rim*(.08+.75*day);
     col=lerp(col,float3(.04,.15,.25),smoothstep(.985,1,r)*.35);
   }
   return float4(col,1);
 }
 ENDHLSL }
 }
 Fallback Off
}
