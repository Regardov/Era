#include "UnityCG.cginc"
#include "Lighting.cginc"

sampler2D _EraWaterBackground;
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
float4 _Color, _ShallowColor;
float _Clarity, _FlowSpeed;
struct WaterInput {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
struct WaterVarying
{
    float4 pos:SV_POSITION;
    float3 world:TEXCOORD0;
    float4 screen:TEXCOORD1;
    float4 grab:TEXCOORD2;
    float2 uv:TEXCOORD3;
    UNITY_FOG_COORDS(4)
};
WaterVarying WaterVertex(WaterInput v)
{
    WaterVarying o;
    o.pos=UnityObjectToClipPos(v.vertex);
    o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
    o.screen=ComputeScreenPos(o.pos);
    o.grab=ComputeGrabScreenPos(o.pos);
    o.uv=v.uv;
    UNITY_TRANSFER_FOG(o,o.pos);
    return o;
}
float4 WaterFragment(WaterVarying i):SV_Target
{
    float time=_Time.y*_FlowSpeed;
    float2 p=i.world.xz;
    float2 waves=float2(sin(p.x*2.7+p.y*1.4-time*1.8)+sin(p.y*4.1-time*2.3)*.35,
                         cos(p.y*2.4-p.x*1.8-time*1.3)+sin(p.x*5.2+time)*.3);
    float3 normal=normalize(float3(waves.x*.075,1,waves.y*.075));
    float3 view=normalize(_WorldSpaceCameraPos-i.world);
    float sceneDepth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
    float surfaceDepth=-mul(UNITY_MATRIX_V,float4(i.world,1)).z;
    float depth=max(0,sceneDepth-surfaceDepth);
    float bank=1-smoothstep(.86,1,abs(i.uv.x*2-1));
    float edge=saturate(depth*7)*bank;

    // Screen-sized offsets keep refraction subtle at any distance. Reject foreground objects.
    float2 offset=waves*.0025*saturate(depth*2)*bank;
    float4 displaced=i.screen;displaced.xy+=offset*displaced.w;
    float displacedDepth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(displaced)));
    offset*=step(surfaceDepth+.02,displacedDepth);
    float4 grab=i.grab;
    #if UNITY_UV_STARTS_AT_TOP
        offset.y=-offset.y;
    #endif
    grab.xy+=offset*grab.w;
    float3 bottom=tex2Dproj(_EraWaterBackground,UNITY_PROJ_COORD(grab)).rgb;
    float absorption=1-exp(-min(depth,5)*_Clarity);
    float3 tint=lerp(_ShallowColor.rgb,_Color.rgb,saturate(depth*.55));
    float3 color=lerp(bottom,tint,absorption*.62);
    float fresnel=.035+.48*pow(1-saturate(dot(normal,view)),4);
    float3 reflection=lerp(float3(.36,.56,.61),float3(.64,.80,.82),saturate(normal.y*.7+view.y*.3));
    color=lerp(color,reflection,fresnel);
    float3 light=normalize(_WorldSpaceLightPos0.xyz);
    float spec=pow(saturate(dot(normal,normalize(light+view))),190);
    color+=_LightColor0.rgb*spec*.65;
    float foamBand=(1-smoothstep(.025,.16,depth))*.45;
    float foamBreak=smoothstep(.15,.75,sin(p.y*8-time*2+sin(p.x*5))*.5+.5);
    float foam=foamBand*foamBreak*bank;
    color=lerp(color,float3(.78,.88,.80),foam);
    float4 result=float4(color,edge*.94);
    UNITY_APPLY_FOG(i.fogCoord,result);
    return result;
}
