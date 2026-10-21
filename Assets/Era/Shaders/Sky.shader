Shader "Era/Painted Sky"
{
    Properties {
        _Zenith("Zenith",Color)=(.18,.42,.62,1)
        _Horizon("Horizon",Color)=(.72,.81,.77,1)
        _SunDirection("Sun direction",Vector)=(.44,.65,-.5,0)
    }
    SubShader {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 pos:SV_POSITION;float3 ray:TEXCOORD0;};
            float4 _Zenith,_Horizon,_SunDirection;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.ray=v.vertex.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            float4 frag(v2f i):SV_Target {
                float3 d=normalize(i.ray);float h=saturate(d.y);
                float3 col=lerp(_Horizon.rgb,_Zenith.rgb,pow(h,.48));
                float sun=dot(d,normalize(_SunDirection.xyz));
                col+=float3(1,.77,.40)*pow(saturate(sun),48)*.32;
                col=lerp(col,float3(1.8,1.45,.88),smoothstep(.9993,.99965,sun));
                float2 p=d.xz/max(.12,d.y)*2.1+float2(_Time.y*.0015,0);
                float cloud=noise(p)*.55+noise(p*2.05)*.28+noise(p*4.1)*.12+noise(p*8.2)*.05;
                float c=smoothstep(.52,.73,cloud)*smoothstep(.03,.16,d.y);
                col=lerp(col,lerp(float3(.78,.84,.83),float3(1,.94,.80),cloud),c*.78);
                return float4(col,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
