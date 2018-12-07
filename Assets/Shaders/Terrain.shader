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

			// 頂点バッファ
			StructuredBuffer<float3> _verticesBuffer;

			// 頂点数
			int _numVertices;

			// ピクセルシェーダーへの出力
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
				float3 vertex = _verticesBuffer[index];

				// 色相
				float3 hue = float3(0, 1, 0);
				// 明度
				float brightness = (instanceID / (_numVertices - 1)) / float(_numVertices - 1);
				// 彩度
				float chroma = (instanceID % (_numVertices - 1)) / float(_numVertices - 1);
				// 色情報
				float3 color = (float3(1, 1, 1) * brightness) + ((hue - float3(1, 1, 1)) * chroma);
				// 透明度
				float alpha = 1;

				// ピクセルシェーダーへの出力
				v2f o;
				o.pos = mul(UNITY_MATRIX_VP, float4(vertex, 1.0f));
				o.uv = v.texcoord;
				o.color = float4(color, alpha);
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
