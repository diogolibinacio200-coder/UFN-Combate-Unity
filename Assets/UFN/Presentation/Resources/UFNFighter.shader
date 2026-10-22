Shader "UFN/Fighter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Flash ("Hit light", Range(0,1)) = 0
        _RimColor ("Rim", Color) = (0.4,0.8,1,1)
        _Rim ("Rim strength", Range(0,1)) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float4 _MainTex_TexelSize;
            float _Flash, _Rim;
            fixed4 _RimColor;
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 source = SampleSpriteTexture(IN.texcoord);
                fixed4 col = source * IN.color;
                float2 d = _MainTex_TexelSize.xy;
                float edge = max(max(SampleSpriteTexture(IN.texcoord+float2(d.x,0)).a,SampleSpriteTexture(IN.texcoord-float2(d.x,0)).a),max(SampleSpriteTexture(IN.texcoord+float2(0,d.y)).a,SampleSpriteTexture(IN.texcoord-float2(0,d.y)).a));
                col.rgb = lerp(col.rgb, fixed3(1,0.97,0.88),_Flash*.72);
                // Lighting changes RGB only. Opaque source pixels stay opaque at all times.
                col.rgb += _RimColor.rgb * _Rim * saturate(1-edge) * .16;
                col.rgb *= col.a;
                return col;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
