Shader "MinnanVillage/Stylized Surface" {
 Properties {
 _ColorA("Color A",Color)=(.6,.2,.1,1)
 _ColorB("Color B",Color)=(.5,.15,.08,1)
 _Mortar("Mortar",Color)=(.4,.25,.18,1)
 _Brick("Brick width / height / joint",Vector)=(.26,.09,.0025,0)
 _Mode("Surface mode",Float)=0
 _Metallic("Metallic",Range(0,1))=0
 _Smoothness("Smoothness",Range(0,1))=.25
 _Glow("Emission",Color)=(0,0,0,0)
 }
 SubShader {
 Tags {"RenderType"="Opaque"}
 LOD 200
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 #include "UnityCG.cginc"
 struct Input {float3 worldPos; float3 worldNormal; INTERNAL_DATA};
 float4 _ColorA,_ColorB,_Mortar,_Brick,_Glow;
 float _Mode,_Metallic,_Smoothness;
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 void surf(Input IN,inout SurfaceOutputStandard o){
 float3 p=IN.worldPos;float3 n=abs(WorldNormalVector(IN,float3(0,0,1)));
 float2 q=(n.y>.7)?p.xz:((n.x>n.z)?p.zy:p.xy);
 float v=noise(q*3.4),fine=noise(q*38);
 float3 col=lerp(_ColorA.rgb,_ColorB.rgb,v);
 if(_Mode>.5 && _Mode<2.5){
 float row=floor(q.y/_Brick.y); float2 u=float2(q.x+fmod(abs(row),2)*_Brick.x*.5,q.y);
 float2 cell=floor(u/_Brick.xy),f=frac(u/_Brick.xy);
 float2 edge=min(f,1-f)*_Brick.xy;
 float aa=max(fwidth(q.x),fwidth(q.y))*.65;
 float joint=smoothstep(_Brick.z-aa,_Brick.z+aa,min(edge.x,edge.y));
 col=lerp(_Mortar.rgb,lerp(_ColorA.rgb,_ColorB.rgb,hash(cell))*(.94+.1*v),joint);
 }else if(_Mode>2.5 && _Mode<3.5){float grain=noise(float2(q.x*42,q.y*2));col=lerp(_ColorA.rgb,_ColorB.rgb,grain)*(.9+.14*v);
 }else if(_Mode>3.5 && _Mode<4.5){
 float2 w=q*float2(1.1,2.6)+float2(_Time.y*.06,_Time.y*.035);
 float wave=noise(w*3);col=lerp(_ColorA.rgb,_ColorB.rgb,wave);
 col+=pow(saturate(sin(q.x*4+q.y*13+_Time.y*.9)*.5+.5),24)*.07;
 o.Normal=normalize(float3(sin(q.x*4+_Time.y)*.08,cos(q.y*7+_Time.y)*.06,1));
 }
 o.Albedo=col*(.97+.05*fine);o.Metallic=_Metallic;o.Smoothness=_Smoothness;o.Emission=_Glow.rgb+col*.075;o.Alpha=1;
 }
 ENDCG
 }
 FallBack "Diffuse"
}
