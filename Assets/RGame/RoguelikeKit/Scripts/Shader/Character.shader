Shader "RoguelikeKit/Character"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BlendColor("BlendColor", Color) = (1,1,1,0)
        _Alpha ("Alpha", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            fixed4 _BlendColor;
            float _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // 叠加 BlendColor
                col.rgb += _BlendColor.rgb;
                
                // Alpha 统一乘上 _Alpha
                col.a *= _Alpha;

                return col;
            }
            ENDCG
        }
    }
}
