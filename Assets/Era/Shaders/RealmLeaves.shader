Shader "Era/Realm Leaves"
{
    Properties {_MainTex("Painted leaf grain",2D)="white"{}}
    SubShader {
        Tags {"RenderType"="Opaque"} Cull Back
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        struct Input {float4 color:COLOR;float2 uv_MainTex;float3 worldPos;};
        void vert(inout appdata_full v,out Input o){
            UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;
            float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
            float weight=saturate(p.y*.18);
            float gust=sin(p.x*.31+p.z*.17+_Time.y*1.1)*.11+sin(p.z*1.8+_Time.y*2.4)*.023;
            v.vertex.xz+=float2(gust,gust*.46)*weight;
        }
        void surf(Input i,inout SurfaceOutputStandard o){
            float grain=tex2D(_MainTex,i.worldPos.xz*.65+i.worldPos.yy*.3).r;
            o.Albedo=i.color.rgb*(.72+grain*.45);o.Emission=i.color.rgb*.045;o.Smoothness=.08;o.Occlusion=.85;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
