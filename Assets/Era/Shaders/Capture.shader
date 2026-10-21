Shader "Hidden/Era/Capture"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _IsFont ("Font", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Overlay" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float _IsFont;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target { fixed4 t = tex2D(_MainTex,i.uv); t.rgb = lerp(t.rgb, fixed3(1,1,1), _IsFont); return t * i.color; }
            ENDCG
        }
    }
}
