Shader "Era/Character Surface"
{
    Properties { _Color("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" } Cull Back
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        struct Input {float4 color:COLOR;float2 paintSurface;};
        void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;o.paintSurface=v.texcoord1.xy;}
        void surf(Input i,inout SurfaceOutputStandard o){o.Albedo=i.color.rgb*_Color.rgb;o.Smoothness=saturate(i.paintSurface.x);o.Metallic=saturate(i.paintSurface.y);o.Occlusion=1;o.Emission=o.Albedo*.025;o.Alpha=1;}
        ENDCG
    }
    Fallback "Standard"
}
