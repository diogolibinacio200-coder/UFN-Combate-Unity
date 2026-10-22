Shader "UFN/Kart Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.22
        _Metallic ("Metallic", Range(0,1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        fixed4 _Color;
        half _Glossiness, _Metallic;
        struct Input { float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo=_Color.rgb;
            o.Metallic=_Metallic;
            o.Smoothness=_Glossiness;
            o.Alpha=1;
        }
        ENDCG
    }
    Fallback Off
}
