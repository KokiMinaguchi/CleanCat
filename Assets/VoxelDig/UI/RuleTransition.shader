Shader "UI/RuleTransition"
{
    Properties
    {
        // Image が自動で入れるスプライト。これが「覆うテクスチャ」になる
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _RuleTex ("Rule Texture", 2D) = "white" {}

        // 0 = 何も覆っていない / 1 = 全部覆った
        _Cutoff ("Cutoff", Range(0, 1)) = 0

        // 境界のぼかし。0に近いほど硬い切り替わり
        _Softness ("Softness", Range(0.001, 0.5)) = 0.05

        // 境界を光らせる幅。0で無効
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.0
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 1, 1, 1)

        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Toggle(_INVERT_RULE)] _InvertRule ("Invert Rule", Float) = 0

        // --- 以下は UGUI が要求する定番の項目 ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "RuleTransition"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma shader_feature_local _INVERT_RULE

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float4 screenPos: TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _RuleTex;
            float4 _RuleTex_ST;

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Cutoff;
            float _Softness;
            float _EdgeWidth;
            fixed4 _EdgeColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;

                // ルール画像は画面全体に貼りたいので、画面座標を使う。
                // スプライトのUVを使うと、9スライスやアトラスでずれる
                o.screenPos = ComputeScreenPos(o.vertex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 覆うテクスチャ。Image に入れたスプライトがそのまま出る
                half4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                float2 ruleUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
                ruleUV = ruleUV * _RuleTex_ST.xy + _RuleTex_ST.zw;

                float rule = tex2D(_RuleTex, ruleUV).r;

#if defined(_INVERT_RULE)
                rule = 1.0 - rule;
#endif

                // Cutoff が 0 と 1 のときに完全に消える／覆うよう、範囲を広げておく。
                // これをしないと、両端で境界が残る
                float c = lerp(-_Softness, 1.0 + _Softness, _Cutoff);

                // rule が c より小さい場所から覆われていく
                float covered = 1.0 - smoothstep(c, c + _Softness, rule);

                // 境界の帯。覆われる直前の場所だけ光らせる
                if (_EdgeWidth > 0.0001)
                {
                    float edge = smoothstep(c + _Softness, c + _Softness + _EdgeWidth, rule);
                    edge = (1.0 - edge) * smoothstep(c, c + _Softness, rule);
                    color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge * _EdgeColor.a);
                    covered = max(covered, edge);
                }

                color.a *= covered;

                color.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                clip(color.a - 0.001);

                return color;
            }
            ENDCG
        }
    }
}
