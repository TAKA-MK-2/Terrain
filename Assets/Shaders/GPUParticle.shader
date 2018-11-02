Shader "Custom/GPUInstancing"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}

	// 共通設定
	CGINCLUDE
		// ファイル読み込み
		#include "UnityCG.cginc"
		#include "Assets/Shaders/Libs/ColorUtil.cginc"
		#include "Assets/Shaders/Libs/Quaternion.cginc"
		#include "GPUParticleCommon.cginc"

		// 頂点データ
		struct VertexData
		{
			float3 vertex;
			float3 normal;
			float2 uv;
			float4 tangent;
		};

		// GPUパーティクルデータ
		struct GPUParticleData
		{
			 // アクティブか判断する
			 bool isActive;
			 // 座標
			 float3 position;
			 // 速度
			 float3 velocity;
			 // 回転
			 float3 rotation;
			 // 角速度
			 float3 angVelocity;
			 // スケール
			 float scale;
			 // 開始スケール
			 float startScale;
			 // 最終スケール
			 float endScale;
			 // 生存時間
			 float lifeTime;
			 // 経過時間
			 float elapsedTime;
		};

		// フラグメントシェーダーへの出力データ
		struct v2f
		{
			// 座標
			float4 pos : SV_POSITION;
			// UV座標
			float2 uv : TEXCOORD0;
			// 色情報
			float4 color : COLOR;
			//float3 normal : TEXCOORD1;
			//float4 tangent : TEXCOORD2;
			//float3 worldNormal  : TEXCOORD3;
			//float3 worldPos : TEXCOORD4;
		};

		// 頂点データの番号のバッファ
		StructuredBuffer<uint> _indices;
		// 頂点データのバッファ
		StructuredBuffer<VertexData> _vertices;
		// GPUパーティクルのバッファ
		StructuredBuffer<GPUParticleData> _particles;

		// メインテクスチャ
		sampler2D _mainTexture;

		// 軸による回転
		float4 _rotationOffsetAxis;
		// 上方向ベクトル
		float3 _upVec;

		// 頂点シェーダー
		v2f vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
		{
			// 頂点データのバッファの番号を取得する
			uint vidx = _indices[vid];
			float4 pos = float4(_vertices[vidx].vertex, 1.0);
			float2 uv = _vertices[vidx].uv;
			float3 normal = _vertices[vidx].normal;
			float4 tangent = _vertices[vidx].tangent;

			//float4 Q = getAngleAxisRotation(_RotationOffsetAxis.xyz, _RotationOffsetAxis.w);

			// GPUパーティクルの番号を取得する
			uint iidx = GetParticleIndex(iid);

			//float4 rotation = qmul(float4(_particles[iidx].rotation, 1), Q);

			// 回転を取得する
			float4 rotation = getAngleAxisRotation(_rotationOffsetAxis.xyz, _rotationOffsetAxis.w);

			// 座標を計算する
			pos.xyz *= _particles[iidx].scale;
			pos.xyz = rotateWithQuaternion(pos.xyz, rotation);
			pos.xyz += _particles[iidx].position;

			// フラグメントシェーダーへの出力データ
			v2f o;
			o.pos = mul(UNITY_MATRIX_VP, pos);
			o.uv = uv;
			o.color = float4(1, 1, 1, 1);
		
			return o;
		}

		fixed4  frag(v2f i) : SV_Target
		{
			// ピクセルの設定
			fixed4 col = tex2D(_mainTexture, i.uv) * i.color;
			return col;
		}

	ENDCG

	SubShader
	{
		// パーティクルエフェクトのレンダリング設定
		Tags{ "RenderType" = "Transparent" "IgnoreProjector" = "True"}
		LOD 100

		Pass
		{
			Name "DEFERRED"
			// ブレンディングのタイプ
			Blend One OneMinusSrcAlpha
			// デプスバッファ書き込みモードを設定
			ZWrite Off
			// ポリゴンのカリングモードを設定
			Cull Off

		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma shader_feature GPUPARTICLE_CULLING_ON

		ENDCG
		}
	}
	Fallback Off
}
