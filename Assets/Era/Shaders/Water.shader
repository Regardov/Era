Shader "Era/Still Water"
{
    Properties {_Color("Deep teal",Color)=(.035,.29,.32,1)}
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        float4 _Color;
        struct Input {float3 worldPos;float3 viewDir;};
        void surf(Input i,inout SurfaceOutputStandard o){
            float2 p=i.worldPos.xz;
            float a=sin(p.x*3+p.y*1.7+_Time.y*.9),b=cos(p.y*4-p.x*1.1+_Time.y*.7);
            o.Normal=normalize(float3(a*.12,b*.12,1));
            float fres=pow(1-saturate(dot(normalize(i.viewDir),o.Normal)),3);
            float glint=pow(saturate(sin(p.x*5+p.y*3+_Time.y)*cos(p.y*4-_Time.y*.7)),12);
            o.Albedo=lerp(_Color.rgb,float3(.43,.72,.72),fres*.6)+glint*.055;
            o.Smoothness=.84;o.Metallic=.28;o.Emission=_Color.rgb*.07;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Standard"
}
