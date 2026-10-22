Shader "Era/Meadow Grass"
{
    Properties {_Color("Tint",Color)=(1,1,1,1) _MainTex("Painted blade clump",2D)="white"{}}
    SubShader {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"} Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;sampler2D _MainTex;float4 _GrassBend0,_GrassBend1,_GrassBend2;
        struct Input {float4 color:COLOR;float2 uv_MainTex;float3 worldPos;};
        float2 bend(float3 p,float4 player){
            float2 delta=p.xz-player.xz;float distance=length(delta);
            float height=1-smoothstep(.6,2.0,abs(p.y-player.y));
            return delta/max(.08,distance)*pow(saturate(1-distance/1.8),1.4)*height*player.w*.85;
        }
        void vert(inout appdata_full v,out Input o){
            UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;
            float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;
            float sway=(sin(w.x*.75+w.z*.4+_Time.y*1.5)+sin(w.z*1.4+_Time.y*2.1)*.4)*.11;
            float2 interaction=bend(w,_GrassBend0)+bend(w,_GrassBend1)+bend(w,_GrassBend2);
            float tip=v.texcoord.y*v.texcoord.y;
            v.vertex.xz+=(float2(sway,sway*.48)+interaction)*tip;
            v.vertex.y-=saturate(length(interaction))*.26*tip;
        }
        void surf(Input i,inout SurfaceOutputStandard o){
            clip(88-distance(i.worldPos,_WorldSpaceCameraPos));
            float4 blade=tex2D(_MainTex,i.uv_MainTex);clip(blade.a-.38);
            float3 root=i.color.rgb*.53,tip=i.color.rgb*1.19;
            o.Albedo=lerp(root,tip,saturate(i.uv_MainTex.y)) * _Color.rgb*blade.rgb;
            o.Emission=o.Albedo*.08;o.Metallic=0;o.Smoothness=.06;o.Occlusion=lerp(.55,1,i.uv_MainTex.y);o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
