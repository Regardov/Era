Shader "Era/Foliage"
{
    Properties { _Color("Tint",Color)=(1,1,1,1) _MainTex("Leaf grain",2D)="white"{} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;fixed4 _Color;
        struct Input { float2 uv_MainTex;float3 worldPos; };
        void vert(inout appdata_full v)
        {
            float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
            float crown=saturate(v.vertex.y+.55);
            float gust=sin(_Time.y*.42+world.x*.035+world.z*.028)*.5+.5;
            float broad=sin(_Time.y*1.05+world.x*.24+world.z*.17);
            float flutter=sin(_Time.y*2.7+world.x*1.15-world.z*.72);
            float2 wind=float2(.075,.038)*(broad*(.55+gust*.65)+flutter*.18);
            v.vertex.xz+=wind*lerp(.35,1,crown);
        }
        void surf(Input IN,inout SurfaceOutputStandard o){
            float grain=.92+sin(IN.worldPos.x*3.1+IN.worldPos.z*2.7)*.035;
            o.Albedo=_Color.rgb*tex2D(_MainTex,IN.uv_MainTex).rgb*grain;
            o.Emission=o.Albedo*.025;o.Metallic=0;o.Smoothness=.08;o.Occlusion=.88;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
