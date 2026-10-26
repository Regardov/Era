Shader "Era/Protective Field"
{
    Properties {_Color("Shield",Color)=(.22,.55,.9,.3) _Hit("Impact",Range(0,1))=0}
    SubShader
    {
        Tags {"Queue"="Transparent+10" "RenderType"="Transparent"} Cull Back ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade noshadow
        #pragma target 3.0
        fixed4 _Color;float _Hit;
        struct Input {float3 worldNormal;float3 worldPos;};
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float fres=pow(1-saturate(dot(normalize(UnityWorldSpaceViewDir(i.worldPos)),normalize(i.worldNormal))),2.5);
            float grid=pow(saturate(sin(i.worldPos.y*15+sin(i.worldPos.x*11)*.3)),14)*.13;
            o.Albedo=_Color.rgb*.25;o.Emission=_Color.rgb*(.32+fres*.9+_Hit*1.2);o.Smoothness=.7;
            o.Alpha=_Color.a*(.018+fres*.8+grid*.35+_Hit*.4);
        }
        ENDCG
    }
    Fallback "Sprites/Default"
}
