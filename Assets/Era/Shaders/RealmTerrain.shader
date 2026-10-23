Shader "Era/Realm Terrain"
{
    Properties {_Color("Meadow tint",Color)=(.4,.57,.27,1) _MainTex("Meadow",2D)="white"{} _StoneTex("Rock",2D)="white"{}}
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex,_StoneTex;fixed4 _Color;
        struct Input {float3 worldPos;float3 worldNormal;};
        void surf(Input i,inout SurfaceOutputStandard o){
            float2 p=i.worldPos.xz;float3 meadow=tex2D(_MainTex,p*.19).rgb;
            float3 stone=tex2D(_StoneTex,p*.13).rgb;
            float slope=saturate((.98-i.worldNormal.y)*3.3);
            float road=1-smoothstep(1.1,2.1,abs(p.y-sin(p.x*.026)*8));
            float mottling=.96+sin(p.x*.17+sin(p.y*.21))*.06+sin(p.x*.045+p.y*.038)*.09;
            meadow=lerp(float3(.80,.80,.80),meadow,.22);stone=lerp(float3(.76,.76,.76),stone,.28);
            float3 ground=_Color.rgb*meadow*mottling;
            ground=lerp(ground,float3(.43,.37,.27)*stone,road*.82);
            o.Albedo=lerp(ground,stone*float3(.46,.48,.45),slope*.8);
            o.Smoothness=.06;o.Metallic=0;o.Occlusion=.94;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
