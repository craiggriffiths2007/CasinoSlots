Shader "CasinoSlots/SymbolBurn"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        _BurnProgress ("Burn Progress", Range(0,1)) = 0
        _Reveal ("Reveal instead of burn", Float) = 0
        _BurnBottom ("World Bottom", Float) = 0
        _BurnHeight ("World Height", Float) = 1
        _BurnLeft ("World Left", Float) = 0
        _BurnWidth ("World Width", Float) = 1

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                float2 burnWorld : TEXCOORD7;
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
          
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _BurnProgress;
                float _Reveal;
                float _BurnBottom;
                float _BurnHeight;
                float _BurnLeft;
                float _BurnWidth;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.burnWorld = TransformObjectToWorld(input.positionOS).xy;
                o.color = input.color *_Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);
                float height = (input.burnWorld.y - _BurnBottom) / max(_BurnHeight, 0.001);
                float x = (input.burnWorld.x - _BurnLeft) / max(_BurnWidth, 0.001);
                float wave = sin(x * 15.0 + _BurnProgress * 12.0) * 0.055
                           + sin(x * 37.0 - _BurnProgress * 19.0) * 0.025;
                float edge = lerp(-0.15, 1.18, _BurnProgress) + wave + (x - 0.5) * 0.16;
                float remaining = lerp(height - edge, edge - height, _Reveal);
                clip(remaining);
                float active = smoothstep(0.0, 0.06, _BurnProgress);
                float heat = (1.0 - smoothstep(0.0, 0.24, remaining)) * active;
                float core = 1.0 - smoothstep(0.0, 0.075, remaining);
                half3 flame = lerp(half3(1.0, 0.28, 0.015), half3(1.0, 0.98, 0.68), core);
                color.rgb = lerp(color.rgb, flame, heat);
                return color;
            }
            ENDHLSL
        }
    }
}
