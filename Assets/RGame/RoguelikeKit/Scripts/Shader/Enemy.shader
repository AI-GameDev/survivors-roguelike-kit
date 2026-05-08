Shader "RoguelikeKit/Enemy"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BlendColor("BlendColor", Color) = (0,0,0,0)
        
        [Space(10)]
        [Header(Shadow)]
        _ShadowColor("Shadow Color", Color) = (0,0,0,0.5)
        _ShadowOffset("Shadow Offset", Vector) = (0.1, -0.1, 0, 0)
        _ShadowScale("Shadow Scale", Vector) = (1, 0.3, 1, 1)
        
        [Space(10)]
        _DistortionStrength ("Distortion Strength", Float) = 0.1
        _DistortionFrequency ("Distortion Frequency", Float) = 5
        _TimeSpeed ("Time Speed", Float) = 1
        
        [Space(10)]
        [Header(Dissolve Effect)]
        _NoiseTex ("Noise Texture", 2D) = "white" {} 
        _Cutoff ("Cutoff", Range(0, 1)) = 0.25    
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.05 
        [HDR] _EdgeColor ("Edge Color", Color) = (1,1,1,1)
        
         [Space(10)]
        [Header(Flame Blend)]
        _FlameRedBlend("Flame Red Blend", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        LOD 100

        // Shadow Pass
        Pass
        {
            Name "Shadow"
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
                float objectY : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ShadowColor;
            float2 _ShadowOffset;
            float3 _ShadowScale;
            
            float _DistortionStrength;
            float _DistortionFrequency;
            float _TimeSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                
                float4 vertexPos = v.vertex;
                vertexPos.xy *= _ShadowScale.xy;
                vertexPos.xy += _ShadowOffset;
                
                float time = _Time.y * _TimeSpeed;
                float distortion = sin((vertexPos.x + time) * _DistortionFrequency) * _DistortionStrength;
                vertexPos.y += distortion * 0.15f;
                
                o.vertex = UnityObjectToClipPos(vertexPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.objectY = vertexPos.y;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                return _ShadowColor;
            }
            ENDCG
        }
        
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
                float objectY : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _BlendColor;
            sampler2D _CrossTex;
            float4 _CrossTex_ST;
            
            float _DistortionStrength;
            float _DistortionFrequency;
            float _TimeSpeed;

            // Dissolve Effect Properties
            sampler2D _NoiseTex;
            float _Cutoff;
            float _EdgeWidth;
            fixed4 _EdgeColor;

            fixed4 _FlameRedBlend;
            
            v2f vert(appdata v)
            {
                v2f o;
                
                float time = _Time.y * _TimeSpeed;
                float distortion = sin((v.vertex.x + time) * _DistortionFrequency) * _DistortionStrength;
                v.vertex.y += distortion;
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.objectY = v.vertex.y;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 noisePixel = tex2D(_NoiseTex, i.uv);

                // Dissolve Effect
                if (noisePixel.r < _Cutoff)
                {
                    discard; // Discard pixels below the cutoff
                }

                // Edge Highlight
                if (noisePixel.r < _Cutoff + _EdgeWidth)
                {
                    col.rgb = _EdgeColor.rgb; // Apply edge color
                }

                col.rgb *= _FlameRedBlend.rgb;
                
                return col + _BlendColor;
            }
            ENDCG
        }
    }
}
