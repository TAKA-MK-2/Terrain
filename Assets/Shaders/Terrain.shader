Shader "Custom/Terrain"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}

	CGINCLUDE
		#include "UnityCG.cginc"

		struct VertexData
		{
			float3 vertex;
			float3 normal;
			float2 uv;
			float4 tangent;
		};


		struct v2f
		{
			float4 pos : SV_POSITION;
			float2 uv : TEXCOORD0;
			float4 color : COLOR;
			//float3 normal : TEXCOORD1;
			//float4 tangent : TEXCOORD2;
			//float3 worldNormal  : TEXCOORD3;
			//float3 worldPos : TEXCOORD4;
		};

		StructuredBuffer<uint> _indices;
		StructuredBuffer<VertexData> _vertices;

		sampler2D _mainTex;

		v2f vert
		{

		}

		fixed4  frag(v2f i) : SV_Target
		{
			fixed4 col = tex2D(_MainTex, i.uv) * i.color;
			return col;
		}

	ENDCG

	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		//Tags{ "RenderType" = "Transparent" "IgnoreProjector" = "True" }
		LOD 100
		Pass
		{
			Name "DEFERRED"
			Blend OneMinusDstColor One // soft additive
			Lighting Off
			ZWrite Off
			Cull Off
			
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 5.0
				#pragma multi_compile_instancing
			ENDCG
		}
	}

	Fallback Off
}
