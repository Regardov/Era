Shader "Hidden/Era/WorldTreeMask"
{
    SubShader { Tags { "RenderType"="Opaque" } Pass { ZWrite On Cull Back
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        struct V { float4 vertex:POSITION; };struct F { float4 position:SV_POSITION; };
        F vert(V v){F o;o.position=UnityObjectToClipPos(v.vertex);return o;}
        fixed4 frag(F i):SV_Target{return fixed4(1,1,1,1);}
        ENDCG
    }}
}
