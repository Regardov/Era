Shader "Era/Low Poly"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) _MainTex("Ground detail",2D)="white"{} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;sampler2D _MainTex;
        struct Input { float4 color : COLOR;float3 worldPos; };
        void vert(inout appdata_full v, out Input o) { UNITY_INITIALIZE_OUTPUT(Input,o); o.color = v.color; }
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
        void surf(Input IN, inout SurfaceOutputStandard o) {
            float2 p=IN.worldPos.xz;
            float grain=noise(p*4.5)*.09+noise(p*19)*.035;
            float detail=noise(p*.72)*.12+grain;
            o.Albedo=IN.color.rgb*_Color.rgb*(.89+detail)*tex2D(_MainTex,p*.23).rgb;
            o.Smoothness=.12;o.Metallic=0;o.Occlusion=.95;o.Emission=o.Albedo*.025;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
