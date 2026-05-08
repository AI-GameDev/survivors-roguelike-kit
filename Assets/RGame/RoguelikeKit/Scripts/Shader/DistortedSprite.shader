Shader "RoguelikeKit/DistortedSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _DistortionStrength ("Distortion Strength", Range(0,0.2)) = 0.05
        _DistortionSpeed ("Distortion Speed", Range(0,5)) = 1.0
        _WaveFrequency ("Wave Frequency", Range(1,20)) = 10.0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _DistortionStrength;
            float _DistortionSpeed;
            float _WaveFrequency;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.pos = UnityObjectToClipPos(IN.vertex);
                OUT.color = IN.color * _Color;
                
                float2 baseUV = TRANSFORM_TEX(IN.texcoord, _MainTex);
                
                float t = _Time.y * _DistortionSpeed;

                // Compute a simple 2D sinusoidal distortion
                float offsetX = sin((baseUV.y * _WaveFrequency) + t);
                float offsetY = cos((baseUV.x * _WaveFrequency) + t);

                OUT.uv = baseUV + float2(offsetX, offsetY) * _DistortionStrength;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 texcol = tex2D(_MainTex, IN.uv);
                return texcol * IN.color;
            }
            ENDCG
        }
    }
}
