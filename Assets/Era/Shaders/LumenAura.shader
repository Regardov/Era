Shader "Era/Lumen Aura"
{
    Properties { [HDR] _Tint("Light",Color)=(2.5,1.25,.12,1) }
    SubShader
    {
        Tags { "Queue"="Transparent+8" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off ZWrite Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
            struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            float4 frag(v2f i):SV_Target
            {
                float across=saturate(1-abs(i.uv.y*2-1));
                float ends=smoothstep(0,.13,i.uv.x)*smoothstep(0,.13,1-i.uv.x);
                float hot=pow(across,12),soft=pow(across,2.2);
                float3 light=lerp(_Tint.rgb,float3(4,3.4,1.8),hot*.8)*i.color.rgb;
                return float4(light,(soft*.38+hot*.65)*ends*_Tint.a*i.color.a);
            }
            ENDCG
        }
    }
}
