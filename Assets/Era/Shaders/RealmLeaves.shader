Shader "Era/Realm Leaves"
{
    Properties {_MainTex("Painted leaf grain",2D)="white"{} _Opacity("Leaf opacity",Range(0,1))=.58}
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;half _Opacity;
        struct Input {float4 color:COLOR;float2 uv_MainTex;float3 worldPos;float facing:VFACE;};
        void vert(inout appdata_full v,out Input o){
            UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;
            float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
            float weight=saturate(p.y*.12);
            float gust=sin(p.x*.31+p.z*.17+_Time.y*1.1)*.10;
            float flutter=sin(p.x*2.8+p.z*2.1+_Time.y*3.4)*.025*saturate(v.texcoord.y);
            v.vertex.xz+=float2(gust+flutter,gust*.46-flutter*.3)*weight;
        }
        void surf(Input i,inout SurfaceOutputStandard o){
            float shell=step(i.uv_MainTex.x,-.5);
            float grain=tex2D(_MainTex,lerp(i.uv_MainTex*1.7,i.worldPos.xz*.35,shell)).r;
            float vein=(1-smoothstep(.008,.035,abs(i.uv_MainTex.x-.5)))*(1-shell);
            float secondary=pow(saturate(cos((i.uv_MainTex.y-abs(i.uv_MainTex.x-.5f)*.75)*55)),16)*.045*(1-shell);
            o.Normal=float3(0,0,i.facing>0?1:-1);
            o.Albedo=i.color.rgb*(.88+grain*.12+vein*.09+secondary);
            o.Emission=i.color.rgb*(i.facing>0?.055:.14);o.Smoothness=.18;o.Occlusion=.92;o.Alpha=_Opacity*(.88+grain*.12);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
