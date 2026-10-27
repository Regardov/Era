Shader "Era/Loot Glow"
{
    Properties { _MainTex("Soft mask",2D)="white"{} _Tint("Rarity",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off ZWrite Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _Tint;
            struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Tint;return o;}
            fixed4 frag(v2f i):SV_Target{return tex2D(_MainTex,i.uv)*i.color;}
            ENDCG
        }
    }
}
