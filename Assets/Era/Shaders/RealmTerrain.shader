Shader "Era/Realm Terrain"
{
    Properties {_Color("Meadow tint",Color)=(.4,.57,.27,1) _MainTex("Meadow",2D)="white"{} _StoneTex("Rock",2D)="white"{} _RealmStage("Realm",Float)=1}
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex,_StoneTex;fixed4 _Color;float _RealmStage;
        struct Input {float3 worldPos;float3 worldNormal;};
        float3 Pebbles(float2 p)
        {
            p=p*4.3+float2(sin(p.y*2),cos(p.x*2.4))*.24;
            float2 cell=floor(p),f=frac(p);float nearest=10,second=10,variation=0;
            [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++)
            {
                float2 g=float2(x,y),key=cell+g;
                float2 random=frac(sin(float2(dot(key,float2(127.1,311.7)),dot(key,float2(269.5,183.3))))*43758.5453);
                float distance=length(g+.22+random*.56-f);
                if(distance<nearest){second=nearest;nearest=distance;variation=random.x;}else second=min(second,distance);
            }
            float grout=smoothstep(.015,.095,second-nearest);
            float shade=.70+.30*(1-saturate(nearest));
            float3 stone=lerp(float3(.17,.23,.22),float3(.39,.39,.30),variation)*shade;
            return lerp(float3(.10,.15,.13),stone,grout);
        }
        void surf(Input i,inout SurfaceOutputStandard o){
            float2 p=i.worldPos.xz;float3 meadow=tex2D(_MainTex,p*.19).rgb;
            float3 stone=tex2D(_StoneTex,p*.13).rgb;
            float slope=saturate((.98-i.worldNormal.y)*3.3);
            float road=1-smoothstep(1.1,2.1,abs(p.y-sin(p.x*.026)*8));
            float mottling=.96+sin(p.x*.17+sin(p.y*.21))*.06+sin(p.x*.045+p.y*.038)*.09;
            meadow=lerp(float3(.80,.80,.80),meadow,.22);stone=lerp(float3(.76,.76,.76),stone,.28);
            float3 ground=_Color.rgb*meadow*mottling;
            ground=lerp(ground,float3(.43,.37,.27)*stone,road*.82);
            o.Albedo=lerp(ground,stone*float3(.46,.48,.45),slope*.8);
            float river0=48+sin(p.y*.019+_RealmStage*.3)*16+sin(p.y*.043)*4;
            float river1=-96+sin(p.y*.016-_RealmStage*.5)*21;
            float width=sin(p.y*.027+_RealmStage)*.35;
            float bankDistance=min(abs(p.x-river0)-(3.2+width),abs(p.x-river1)-(2.1+width));
            float riverbed=1-smoothstep(-.2,1.1,bankDistance);
            [branch]if(riverbed>.001)o.Albedo=lerp(o.Albedo,Pebbles(p),riverbed*.96);
            float caustic=pow(saturate(1-abs(sin(p.x*4+sin(p.y*3+_Time.y*.65))+sin(p.y*4.7-_Time.y*.8))*.75),9);
            o.Emission=float3(.025,.045,.035)*caustic*riverbed*saturate(-bankDistance*2);
            o.Smoothness=.06;o.Metallic=0;o.Occlusion=.94;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
