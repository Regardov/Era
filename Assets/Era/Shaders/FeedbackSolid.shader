Shader "Era/Feedback Solid"
{
    Properties {_Color("Tint",Color)=(1,1,1,1)}
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert noshadow
        #pragma target 3.0
        struct Input {float4 color:COLOR;};
        fixed4 _Color;
        void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;}
        void surf(Input i,inout SurfaceOutputStandard o){float4 c=i.color*_Color;o.Albedo=c.rgb*.65;o.Emission=c.rgb*.40;o.Smoothness=.3;o.Alpha=c.a;}
        ENDCG
    }
    Fallback "Sprites/Default"
}
