Shader "Era/Rift Portal"
{
    Properties {_Color("Realm tint",Color)=(.5,.4,1,1)}
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            float4 _Color;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            float4 frag(v2f i):SV_Target {
                float2 p=(i.uv-.5)*2;float r=length(p);clip(1-r);
                float a=atan2(p.y,p.x);float swirl=sin(r*24-a*3-_Time.y*2.8)*.5+.5;
                float halo=pow(saturate(r),5);float3 c=lerp(_Color.rgb*.13,_Color.rgb,swirl*.6+halo*.6);
                c+=pow(saturate(1-r),12)*.7;return float4(c,(1-smoothstep(.92,1,r))*.86);
            }
            ENDCG
        }
    }
    Fallback Off
}
