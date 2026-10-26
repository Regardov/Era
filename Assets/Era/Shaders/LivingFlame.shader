Shader "Era/Living Flame"
{
    Properties {_Color("Tint",Color)=(1,1,1,.8)}
    SubShader
    {
        Tags {"Queue"="Transparent+5" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;};
            float4 _Color;float _EraVisualTime;
            v2f vert(appdata v)
            {
                v2f o;float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;float height=saturate(v.vertex.y);
                v.vertex.x+=sin(_EraVisualTime*12+w.z*11+w.x*7)*height*.09;
                v.vertex.z+=sin(_EraVisualTime*9+w.x*9)*height*.055;
                o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.color.a*=.8+.15*sin(_EraVisualTime*17+w.x*8);return o;
            }
            fixed4 frag(v2f i):SV_Target{return fixed4(i.color.rgb,i.color.a);}
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
