Shader "CsoundUnity/Unlit Line"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(float4 position : POSITION, fixed4 color : COLOR)
            {
                v2f o;
                o.position = UnityObjectToClipPos(position);
                o.color = color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color * _Color;
            }
            ENDCG
        }
    }
    Fallback Off
}
