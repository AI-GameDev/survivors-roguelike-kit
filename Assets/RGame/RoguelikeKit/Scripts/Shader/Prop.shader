Shader "RoguelikeKit/Enemy"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BlendColor("BlendColor", Color) = (0,0,0,0)
        
        [Space(10)]
        [Header(Dissolve Effect)]
        _NoiseTex ("Noise Texture", 2D) = "white" {} 
        _Cutoff ("Cutoff", Range(0, 1)) = 0.25    
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.05 
        [HDR] _EdgeColor ("Edge Color", Color) = (1,1,1,1) 
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
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
            
            sampler2D _NoiseTex;
            float _Cutoff;
            float _EdgeWidth;
            fixed4 _EdgeColor;

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
                fixed4 noisePixel = tex2D(_NoiseTex, i.uv);
                
                if (noisePixel.r < _Cutoff)
                {
                    discard;
                }
                
                if (noisePixel.r < _Cutoff + _EdgeWidth)
                {
                    col.rgb = _EdgeColor.rgb;
                }

                return col + _BlendColor;
            }
            ENDCG
        }
    }
}
