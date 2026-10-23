Shader "Era/Faceted Realm"
{
    Properties { _Color("Tint",Color)=(1,1,1,1) _Cull("Face culling",Float)=2 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull [_Cull]
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        struct Input { float4 color:COLOR; };
        void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;}
        void surf(Input i,inout SurfaceOutputStandard o){o.Albedo=i.color.rgb*_Color.rgb;o.Smoothness=0;o.Metallic=0;o.Occlusion=1;o.Emission=o.Albedo*.045;o.Alpha=1;}
        ENDCG
    }
    Fallback "Diffuse"
}
