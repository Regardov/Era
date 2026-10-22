Shader "Era/Realm Surface"
{
    Properties {
        _Color("Tint",Color)=(1,1,1,1)
        _MainTex("Painted surface",2D)="white"{}
        _BumpMap("Fine relief",2D)="bump"{}
        _MossColor("Moss",Color)=(.27,.42,.19,1)
        _Kind("Bark or stone",Float)=0
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex,_BumpMap;fixed4 _Color,_MossColor;float _Kind;
        struct Input {float2 uv_MainTex;float4 color:COLOR;float3 worldPos;float3 worldNormal;INTERNAL_DATA};
        void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;}
        void surf(Input i,inout SurfaceOutputStandard o){
            float2 uv=i.uv_MainTex*(_Kind>.5?float2(2,2):float2(2.5,2.5));
            float3 base=tex2D(_MainTex,uv).rgb;
            float3 detail=UnpackNormal(tex2D(_BumpMap,uv));detail.xy*=.65;
            float top=saturate(WorldNormalVector(i,float3(0,0,1)).y-.23);
            float patch=sin(i.worldPos.x*1.8+sin(i.worldPos.z*2.3))*sin(i.worldPos.z*1.2)*.5+.5;
            float moss=(_Kind<.5)?smoothstep(.3,.8,top)*smoothstep(.25,.8,patch)*.72:0;
            o.Albedo=lerp(base*i.color.rgb*_Color.rgb,_MossColor.rgb*(.65+base*.45),moss);
            o.Normal=normalize(detail);o.Smoothness=.11;o.Metallic=0;o.Occlusion=lerp(.78,1,base.r);o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
