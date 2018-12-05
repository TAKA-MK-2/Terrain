Shader "Custum/Terrain"
{
	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag		
			#pragma target 4.5

			#include "UnityCG.cginc"
			#include "Libs/Quaternion.cginc"

			StructuredBuffer<float3> _vertexBuffer;
			int _numVertices;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color: COLOR;
			};

			v2f vert(appdata_full v, uint instanceID : SV_InstanceID)
			{
				// インスタンスIDとuvからバッファ上の要素番号を計算
				int index = instanceID + (v.texcoord.y * _numVertices + v.texcoord.x) + (instanceID / (_numVertices - 1));

				// 座標
				float3 vertex = _vertexBuffer[index];
				// 色情報
				float col = instanceID / (float)((_numVertices - 1) * (_numVertices - 1));

				// ピクセルシェーダーへの出力
				v2f o;
				o.pos = mul(UNITY_MATRIX_VP, float4(vertex, 1.0f));
				o.uv = v.texcoord;
				o.color = float4(col, col, col, 1);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				return i.color;
			}

			ENDCG
		}
	}
}
