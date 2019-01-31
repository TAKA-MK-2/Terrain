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

			// テクスチャ
			sampler2D _mainTexture;
	
			// 頂点数
			int _numVertices;
			float _height;

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
				int index_x = instanceID % (_numVertices - 1) + v.texcoord.x;
				int index_z = instanceID / (_numVertices - 1) + v.texcoord.y;
				int index = index_x + index_z * _numVertices;

				// 座標
				float3 vertex = _verticesBuffer[index];

				float val = vertex.y / _height;
				float3 color = float3(val, val, val);
				
				// 透明度
				float alpha = 1;

				// ピクセルシェーダーへの出力
				v2f o;
				o.pos = mul(UNITY_MATRIX_VP, float4(vertex, 1.0f));
				o.uv = v.texcoord;
				o.color = float4(color, alpha);
				return o;
			}

			fixed4  frag(v2f i) : SV_Target
			{
				return i.color;
				fixed4 col = tex2D(_mainTexture, i.uv) * i.color;
				return col;
			}

			ENDCG
		}
	}
}
