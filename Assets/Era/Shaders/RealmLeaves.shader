Shader "Era/Realm Leaves"
{
    Properties {_MainTex("Painted leaf grain",2D)="white"{}}
    SubShader {
        Tags {"RenderType"="Opaque"} Cull Back
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        struct Input {float4 color:COLOR;float2 uv_MainTex;float3 worldPos;float3 worldNormal;INTERNAL_DATA};
        void vert(inout appdata_full v,out Input o){
            UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;
            float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
            float weight=saturate(p.y*.18);
            float gust=sin(p.x*.31+p.z*.17+_Time.y*1.1)*.11+sin(p.z*1.8+_Time.y*2.4)*.023;
            v.vertex.xz+=float2(gust,gust*.46)*weight;
        }
        void surf(Input i,inout SurfaceOutputStandard o){
            float grain=tex2D(_MainTex,i.worldPos.xz*.65+i.worldPos.yy*.3).r;
            float3 face=normalize(cross(ddy(i.worldPos),ddx(i.worldPos))),normal=WorldNormalVector(i,float3(0,0,1));face*=dot(face,normal)<0?-1:1;
            o.Normal=normalize(float3(dot(face,WorldNormalVector(i,float3(1,0,0))),dot(face,WorldNormalVector(i,float3(0,1,0))),dot(face,normal)));
            o.Albedo=i.color.rgb*(.87+grain*.14);o.Emission=i.color.rgb*.045;o.Smoothness=0;o.Occlusion=.94;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
