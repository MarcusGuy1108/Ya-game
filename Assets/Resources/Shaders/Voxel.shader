// Opaque + alpha-cutout voxel shader. Lighting (face shade + ambient occlusion) is baked into vertex colours,
// so this is a plain unlit shader that works in the Built-in pipeline and in URP.
Shader "UKCity/Voxel"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Pass
        {
            Cull Back
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UKCityCommon.cginc"

            sampler2D _MainTex;
            float _Cutoff;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float fog : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.fog = UKFogFactor(mul(unity_ObjectToWorld, v.vertex).xyz);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                clip(c.a - _Cutoff);
                c.rgb *= i.color.rgb * _UKDaylight;
                c.rgb = lerp(c.rgb, _UKFogColor.rgb, i.fog);
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
}
