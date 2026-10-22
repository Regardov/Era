Shader "Era/Actor Satin"
{
    Properties {_MainTex("Painted atlas",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _Accent("Rim hue",Color)=(.65,.75,1,1) _Lumen("Ivory and gold palette",Float)=0 _Boss("Realm clothing palette",Float)=0}
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;fixed4 _Color,_Accent;float _Lumen,_Boss;
        struct Input {float2 uv_MainTex;float3 viewDir;};
        void surf(Input i,inout SurfaceOutputStandard o){
            float3 c=tex2D(_MainTex,i.uv_MainTex).rgb;
            if(_Boss>.5){float mask=max(step(c.r*1.2,c.b),max(step(c.r*1.4,c.g),step(c.g*1.7,c.r)));c=lerp(c,_Accent.rgb*(.35+dot(c,float3(.25,.5,.25))*1.4),mask);}
            if(_Lumen>.5){
                float blue=step(c.r*1.1,c.b)*step(c.g*.98,c.b);
                float pink=step(c.g*1.5,c.r)*step(c.r*.18,c.b);
                float light=dot(c,float3(.25,.5,.25));
                c=lerp(c,lerp(float3(.34,.35,.49),float3(.94,.91,.78),saturate(light*1.8)),blue);
                c=lerp(c,float3(.70,.49,.18)*(light+.75),pink);
                if(_Lumen>1.5)c=lerp(float3(.34,.31,.20),float3(.93,.89,.73),saturate(light*2.3));
            }
            o.Albedo=c*_Color.rgb;o.Metallic=0;o.Smoothness=.23;
            float rim=pow(1-saturate(dot(normalize(i.viewDir),float3(0,0,1))),3);
            o.Emission=_Accent.rgb*rim*.13;o.Occlusion=.96;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
