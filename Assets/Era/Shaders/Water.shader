Shader "Era/Still Water"
{
    Properties
    {
        _Color("Deep water",Color)=(.07,.32,.34,1)
        _ShallowColor("Sunlit shallows",Color)=(.38,.73,.66,1)
        _Clarity("Absorption per metre",Range(.05,2))=.42
        _FlowSpeed("Current speed",Range(0,3))=.25
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        GrabPass {"_EraWaterBackground"}
        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_fog
            #include "ClearWater.cginc"
            ENDCG
        }
    }
    Fallback Off
}
