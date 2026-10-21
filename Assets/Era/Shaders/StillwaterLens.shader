Shader "Hidden/Era/Stillwater Lens"
{
    Properties {_MainTex("Source",2D)="white"{} _BloomTex("Bloom",2D)="black"{}}
    SubShader {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex,_BloomTex,_CameraDepthNormalsTexture;
        float4 _MainTex_TexelSize;
        float2 _BlurDirection;
        float _BloomStrength,_OcclusionStrength;
        float4 fragExtract(v2f_img i):SV_Target {
            float3 c=tex2D(_MainTex,i.uv).rgb;
            float l=max(c.r,max(c.g,c.b));return float4(c*saturate((l-.92)/max(l,.001)),1);
        }
        float4 fragBlur(v2f_img i):SV_Target {
            float2 d=_BlurDirection*_MainTex_TexelSize.xy;
            float3 c=tex2D(_MainTex,i.uv).rgb*.227027;
            c+=(tex2D(_MainTex,i.uv+d*1.384615).rgb+tex2D(_MainTex,i.uv-d*1.384615).rgb)*.316216;
            c+=(tex2D(_MainTex,i.uv+d*3.230769).rgb+tex2D(_MainTex,i.uv-d*3.230769).rgb)*.070270;
            return float4(c,1);
        }
        float occlusion(float2 uv) {
            float z;float3 n;DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture,uv),z,n);
            if(z>.995)return 1;
            float depth=z*_ProjectionParams.z;
            float radius=clamp(22/max(depth,1),1,15);
            float ao=0;
            for(int k=0;k<8;k++) {
                float a=k*2.399963;float2 off=float2(cos(a),sin(a))*radius*(.55+.45*(k%3)/2.0)*abs(_MainTex_TexelSize.xy);
                float q;float3 nn;DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture,uv+off),q,nn);
                float diff=depth-q*_ProjectionParams.z;
                ao+=smoothstep(.025,.28,diff)*(1-smoothstep(.35,1.3,diff))*(.45+.55*(1-saturate(dot(n,nn))));
            }
            return 1-ao*.125*_OcclusionStrength;
        }
        float4 fragFinal(v2f_img i):SV_Target {
            float2 duv=i.uv;
            #if UNITY_UV_STARTS_AT_TOP
            if(_MainTex_TexelSize.y<0)duv.y=1-duv.y;
            #endif
            float3 c=tex2D(_MainTex,i.uv).rgb*occlusion(duv);
            c+=tex2D(_BloomTex,i.uv).rgb*_BloomStrength;
            // A restrained filmic shoulder keeps painted colors and bright magic readable.
            c*=1.03;
            c=(c*(2.51*c+.03))/(c*(2.43*c+.59)+.14);
            float l=dot(c,float3(.2126,.7152,.0722));c=lerp(l.xxx,c,1.055);
            c=lerp(c,c*float3(1.025,1.01,.975),.55);
            float2 v=i.uv*(1-i.uv);float vignette=pow(saturate(v.x*v.y*16),.14);
            c*=lerp(.89,1,vignette);
            return float4(saturate(c),1);
        }
        ENDCG
        Pass {CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragExtract
            ENDCG}
        Pass {CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragBlur
            ENDCG}
        Pass {CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment fragFinal
            ENDCG}
    }
    Fallback Off
}
