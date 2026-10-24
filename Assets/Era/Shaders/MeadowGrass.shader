Shader "Era/Meadow Grass"
{
    Properties {_Color("Tint",Color)=(1,1,1,1)}
    SubShader {
        Tags {"RenderType"="Opaque"} Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;float4 _GrassBend0,_GrassBend1,_GrassBend2,_GrassImpact;float _EraVisualTime;
        struct Input {float4 color:COLOR;float2 bladeUV;float3 worldPos;};
        float2 bend(float3 p,float4 player){
            float2 delta=p.xz-player.xz;float distance=length(delta);
            float height=1-smoothstep(.6,2.0,abs(p.y-player.y));
            return delta/max(.08,distance)*pow(saturate(1-distance/1.8),1.4)*height*player.w*.85;
        }
        void vert(inout appdata_full v,out Input o){
            UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;o.bladeUV=v.texcoord.xy;
            float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;
            float sway=(sin(w.x*.75+w.z*.4+_EraVisualTime*1.5)+sin(w.z*1.4+_EraVisualTime*2.1)*.4)*.11;
            float2 interaction=bend(w,_GrassBend0)+bend(w,_GrassBend1)+bend(w,_GrassBend2);
            float2 impactDelta=w.xz-_GrassImpact.xy;float impactDistance=length(impactDelta);interaction+=impactDelta/max(.1,impactDistance)*saturate(1-impactDistance/max(.1,_GrassImpact.z))*_GrassImpact.w*.55;
            float tip=v.texcoord.y*v.texcoord.y;
            v.vertex.xz+=(float2(sway,sway*.48)+interaction)*tip;
            v.vertex.y-=saturate(length(interaction))*.26*tip;
        }
        void surf(Input i,inout SurfaceOutputStandard o){
            clip(88-distance(i.worldPos,_WorldSpaceCameraPos));
            float3 root=i.color.rgb*.72,tip=i.color.rgb*1.15;
            o.Albedo=lerp(root,tip,saturate(i.bladeUV.y)) * _Color.rgb;
            o.Emission=o.Albedo*.16;o.Metallic=0;o.Smoothness=.08;o.Occlusion=lerp(.85,1,i.bladeUV.y);o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
