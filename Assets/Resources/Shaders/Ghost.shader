// Translucent tinted preview of a building template before it is placed.
Shader "UKCity/Ghost"
{
    Properties
    {
        _Tiles ("Block Tiles", 2DArray) = "" {}
        _Color ("Tint", Color) = (0.6, 1, 0.6, 0.55)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray
            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2DARRAY(_Tiles);
            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; float3 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = UNITY_SAMPLE_TEX2DARRAY(_Tiles, i.uv);
                clip(c.a - 0.1);
                c.rgb = c.rgb * i.color.rgb * _Color.rgb;
                c.a = _Color.a;
                return c;
            }
            ENDCG
        }
    }
}
