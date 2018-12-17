Shader "Custom/GPUInstancing"
{
	CGINCLUDE
		#include "UnityCG.cginc"
		#include "Assets/Shaders/Libs/ColorUtil.cginc"
		#include "Assets/Shaders/Libs/Quaternion.cginc"
		#include "GPUParticleCommon.cginc"

		// 頂点情報
		struct VertexData
		{
			// 座標
			float3 vertex;
			// 法線
			float3 normal;
			// uv
			float2 uv;
		};

		// パーティクル情報
		struct GPUParticleData
		{
			// アクティブか判断する
			bool isActive;
			// 座標
			float3 position;
			// 速度
			float3 velocity;
			// スケール
			float scale;
			// 開始スケール
			float startScale;
			// 最終スケール
			float endScale;
			// 色
			float4 color;
			// 生存時間
			float lifeTime;
			// 経過時間
			float elapsedTime;
		};

		// ピクセルシェーダーへの出力
		struct v2f
		{
			float4 pos : SV_POSITION;
			float2 uv : TEXCOORD0;
			float4 color : COLOR;
		};

		// メッシュの頂点の要素番号
		StructuredBuffer<uint> _meshIndicesBuffer;
		// メッシュの頂点情報
		StructuredBuffer<VertexData> _meshVertexDatasBuffer;
		// パーティクル情報
		StructuredBuffer<GPUParticleData> _particlesBuffer;

		// テクスチャ
		sampler2D _mainTexture;

		v2f vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
		{
			// 頂点情報バッファの要素番号
			uint idx = _meshIndicesBuffer[vid];

			// 頂点情報を取り出す
			VertexData vertex = _meshVertexDatasBuffer[idx];
			float4 pos = float4(vertex.vertex, 1.0);
			float2 uv = vertex.uv;
			float3 normal = vertex.normal;

			// パーティクル情報バッファの要素番号
			uint iidx = GetParticleIndex(iid);

			// パーティクル情報を取り出す
			GPUParticleData particle = _particlesBuffer[iidx];

			// 現在の座標
			float3 offset = particle.position;

			// 座標にスケールを適用
			pos.xyz *= particle.scale;

			// ピクセルシェーダーへの出力
			v2f o;
			// 非ビルボード
			//o.pos = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_MV, pos));
			// ビルボード
			o.pos = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_MV, float4(offset.x, offset.y, offset.z + pos.z, 1)) + float4(pos.x, pos.y, 0, 0));	
			o.uv = uv;
			o.color = particle.color;
		
			return o;
		}

		fixed4  frag(v2f i) : SV_Target
		{
			fixed4 col = tex2D(_mainTexture, i.uv) * i.color;
			return col;
		}
	ENDCG

	SubShader
	{
		Tags{ "RenderType" = "Transparent" "IgnoreProjector" = "True"}
		LOD 100

		Pass
		{
			Name "DEFERRED"
			Blend OneMinusDstColor One
			Lighting Off
			ZWrite Off
			Cull Off

		CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma shader_feature GPUPARTICLE_CULLING_ON
			#pragma multi_compile_instancing

		ENDCG
		}
	}

	Fallback Off
}
