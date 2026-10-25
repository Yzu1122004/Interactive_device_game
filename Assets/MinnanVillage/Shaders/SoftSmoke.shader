Shader "MinnanVillage/Soft Smoke" {
 Properties{_MainTex("Soft puff",2D)="white"{} _Color("Tint",Color)=(.72,.74,.75,.2)}
 SubShader {Tags{"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off Cull Off
 Pass {CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _Color;
 struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
 fixed4 frag(v2f i):SV_Target{return tex2D(_MainTex,i.uv)*_Color*i.color;}
 ENDCG}
 }
}
