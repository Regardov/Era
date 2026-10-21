Shader "Era/Meadow Grass"
{
    Properties {_Color("Tint",Color)=(1,1,1,1)}
    SubShader {
        Tags {"RenderType"="Opaque"} Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        struct Input {float4 color:COLOR;float2 uv_MainTex;float3 worldPos;};
        void vert(inout appdata_full v,out Input o){
            UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;
            float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;
            float sway=(sin(w.x*.75+w.z*.4+_Time.y*1.5)+sin(w.z*1.4+_Time.y*2.1)*.4)*.11;
            v.vertex.xz+=float2(sway,sway*.48)*v.texcoord.y*v.texcoord.y;
        }
        void surf(Input i,inout SurfaceOutputStandard o){
            clip(88-distance(i.worldPos,_WorldSpaceCameraPos));
            float3 root=i.color.rgb*.59,tip=i.color.rgb*1.13;
            o.Albedo=lerp(root,tip,saturate(i.uv_MainTex.y)) * _Color.rgb;
            o.Emission=o.Albedo*.08;o.Metallic=0;o.Smoothness=.06;o.Occlusion=lerp(.55,1,i.uv_MainTex.y);o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
