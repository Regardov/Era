Shader "Era/Stream Water"
{
    Properties {_Color("Water tint",Color)=(.15,.46,.46,1) _MainTex("Flow coordinates",2D)="white"{}}
    SubShader
    {
        Tags {"RenderType"="Opaque"} Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        struct Input {float2 uv_MainTex;float3 worldPos;};
        sampler2D _MainTex;
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float2 p=i.worldPos.xz;
            float flow=p.y*.9-_Time.y*1.7;
            float ripple=sin(flow+sin(p.x*2.3))*sin(p.x*3.7+_Time.y*.8);
            float banks=smoothstep(.34,.5,abs(i.uv_MainTex.x-.5));
            float foam=banks*saturate(ripple*.5+.35)*.3;
            o.Albedo=lerp(_Color.rgb*(.86+ripple*.12),float3(.73,.89,.82),foam);
            o.Normal=normalize(float3(cos(flow+p.x)*.09,sin(p.x*2-flow)*.07,1));
            o.Emission=_Color.rgb*.10;o.Metallic=.1;o.Smoothness=.72;o.Occlusion=1;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
