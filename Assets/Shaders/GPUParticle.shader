Shader "GPUParticle/GPUParticle"
{

	SubShader
	{
	// ライティングパイプライン
	Tags { "RenderType" = "Opaque" }
	// 共通設定
	CGINCLUDE
	// ファイルの読み込み
	#include "UnityCG.cginc"
	#include "UnityStandardShadow.cginc"
	// パーティクル
	struct GPUParticle
	{
		// アクティブ状態
		bool isActive;
		// 座標
		float3 position;
		// 速度
		float3 velocity;
		// 回転
		float3 rotation;
		// 角速度
		float3 angVelocity;
		// 色
		float4 color;
		// スケール
		float scale;
		// 経過時間
		float elapsedTime;
		// 生存時間
		float lifeTime;
	};

#ifdef SHADER_API_D3D11
	// パーティクルバッファ
	StructuredBuffer<GPUParticle> _particles;
#endif
	// 
	float _idOffset;

	// 頂点シェーダーへの入力
	struct Appdata
	{
		float4 vertex : POSITION;
		float3 normal : NORMAL;
		float2 uv1 : TEXCOORD1;
	};

	// 頂点シェーダーからフラグメントシェーダーへの出力
	struct V2F
	{
		float4 position : SV_POSITION;
		float3 normal : NORMAL;
		float2 uv1 : TEXCOORD1;
	};

	// ShadowCaster用v2f
	struct V2F_shadow
	{
		V2F_SHADOW_CASTER;
	};

	// フラグメントシェーダーからのGBuffer出力
	struct GBuffer_out
	{
		float4 diffuse  : SV_Target0; // rgb: diffuse,  a: occlusion
		float4 specular : SV_Target1; // rgb: specular, a: smoothness
		float4 normal   : SV_Target2; // rgb: normal,   a: unused
		float4 emission : SV_Target3; // rgb: emission, a: unused
		float  depth : SV_Depth;
	};

	// パーティクルのIDを取得する
	inline int GetId(float2 uv1)
	{
		return (int)(uv1.x + 0.5) + (int)_idOffset;
	}

	float3 rotate(float3 p, float3 rotation)
	{
		float3 a = normalize(rotation);
		float angle = length(rotation);
		if (abs(angle) < 0.001) return p;
		float s = sin(angle);
		float c = cos(angle);
		float r = 1.0 - c;
		float3x3 m = float3x3(
			a.x * a.x * r + c,
			a.y * a.x * r + a.z * s,
			a.z * a.x * r - a.y * s,
			a.x * a.y * r - a.z * s,
			a.y * a.y * r + c,
			a.z * a.y * r + a.x * s,
			a.x * a.z * r + a.y * s,
			a.y * a.z * r - a.x * s,
			a.z * a.z * r + c
		);
		return mul(m, p);
	}

	// 頂点シェーダー 
	V2F vert(Appdata v)
	{
	#ifdef SHADER_API_D3D11
		// パーティクルを取り出す
		GPUParticle p = _particles[GetId(v.uv1)];
		// 頂点を設定
		v.vertex.xyz *= p.scale;
		v.vertex.xyz = rotate(v.vertex.xyz, p.rotation);
		v.vertex.xyz += p.position;
		v.normal = rotate(v.normal, p.rotation);
	#endif
		// フラグメントシェーダーへの出力
		V2F o;
		o.uv1 = v.uv1;
		o.position = mul(UNITY_MATRIX_VP, v.vertex);
		o.normal = v.normal;
		return o;
	}

	// フラグメントシェーダー
	GBuffer_out frag(V2F i) : SV_Target
	{
		// GBufferへの出力
		GBuffer_out o;
		o.diffuse = 0;
		o.normal = float4(0.5 * i.normal + 0.5, 1);
		o.emission = o.diffuse * 0.1;
		o.specular = 0;
		o.depth = i.position;

	#ifdef SHADER_API_D3D11
		// 色を変更
		GPUParticle p;
		p = _particles[GetId(i.uv1)];
		o.diffuse = p.color;
	#endif

		return o;
	}

	// ShadowCaster時の頂点シェーダー
	V2F_shadow vert_shadow(Appdata v)
	{
	#ifdef SHADER_API_D3D11
		// パーティクルを取り出す
		GPUParticle p = _particles[GetId(v.uv1)];
		// 頂点を設定
		v.vertex.xyz = rotate(v.vertex.xyz, p.rotation);
		v.vertex.xyz *= p.scale;
		v.vertex.xyz += p.position;
	#endif
		// ShadowCaster用にv2fを変換
		V2F_shadow o;
		TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
		o.pos = mul(UNITY_MATRIX_VP, v.vertex);
		return o;
	}

	// ShadowCaster時のフラグメントシェーダー
	float4 frag_shadow(V2F_shadow i) : SV_Target
	{
		SHADOW_CASTER_FRAGMENT(i)
	}

	ENDCG

	Pass
	{
		Tags { "LightMode" = "Deferred" }
		ZWrite On

		CGPROGRAM
		#pragma target 3.0
		#pragma vertex vert 
		#pragma fragment frag 
		ENDCG
	}

	Pass
	{
		Tags { "LightMode" = "ShadowCaster" }
		Fog { Mode Off }
		ZWrite On
		ZTest LEqual
		Cull Off
		Offset 1, 1

		CGPROGRAM
		#pragma target 3.0
		#pragma vertex vert_shadow
		#pragma fragment frag_shadow
		#pragma multi_compile_shadowcaster
		#pragma fragmentoption ARB_precision_hint_fastest
		ENDCG
	}

	}

		FallBack "Diffuse"

}