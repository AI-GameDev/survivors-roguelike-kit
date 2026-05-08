Shader "RoguelikeKit/Trail" 
{
    Properties 
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _TrailOffset ("Trail Offset", Float) = 0.05
        _TrailDirection ("Trail Direction", Vector) = (0, -1, 0, 0)
        _TrailWeight ("Trail Weight", Float) = 0.5
        _EdgeSoftness ("Edge Softness", Range(0,1)) = 0.2
    }
    SubShader 
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Pass 
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata_t 
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
            float4 _MainTex_ST;
            float _TrailOffset;
            float4 _TrailDirection;
            float _TrailWeight;
            float _EdgeSoftness;
            v2f vert (appdata_t v) 
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target 
            {
                float2 trailDir = normalize(_TrailDirection.xy);
                fixed4 col0 = tex2D(_MainTex, i.uv);
                fixed4 col1 = tex2D(_MainTex, i.uv + trailDir * _TrailOffset);
                fixed4 col2 = tex2D(_MainTex, i.uv + trailDir * _TrailOffset * 2.0);
                fixed4 finalCol = col0 + col1 * _TrailWeight + col2 * (_TrailWeight * _TrailWeight);
                float2 uvCentered = (i.uv - 0.5) * 2.0;
                float dist = length(uvCentered);
                float edge = smoothstep(1.0 - _EdgeSoftness, 1.0, dist);
                finalCol.a *= (1.0 - edge);
                return finalCol;
            }
            ENDCG
        }
    }
}
