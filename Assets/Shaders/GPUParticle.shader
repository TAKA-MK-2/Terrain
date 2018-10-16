Shader "GPUParticle/GPUParticle"
{
	Properties
	{
		// パーティクルのテクスチャ
		_Tex("Tex2D", 2D) = "white"{}
	}

	SubShader
	{
		// 不透明で描画する
		Tags { "RenderType" = "Opaque" }

		// 複数パス間での共通設定を設定する
		CGINCLUDE
			// ファイルのインクルード
			#include "UnityCG.cginc"
			#include "UnityStandardShadow.cginc"

			// GPUパーティクル
			struct GPUParticle
			{
				// 識別番号
				int index;
				// 活動状態
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
				// カラー
				float3 color;
				// 経過時間
				float elapsedTime;
				// 生存時間
				float lifeTime;
			};

			#ifdef SHADER_API_D3D11
				// パーティクルのバッファ
				StructuredBuffer<GPUParticle> _particles;
			#endif

			// メッシュのID
			int _idOffset;

			// 頂点シェーダーの引数用構造体
			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv1 : TEXCOORD1;
			};

			// 頂点シェーダーからフラグメントシェーダーに渡す引数用構造体
			struct v2f
			{
				float4 position : SV_POSITION;
				float3 normal : NORMAL;
				float2 uv1 : TEXCOORD1;
			};

			// ShadowCaster用v2f
			struct v2f_shadow
			{
				V2F_SHADOW_CASTER;
			};

			// GBuffer出力用
			struct gbuffer_out
			{
				float4 diffuse  : SV_Target0; // rgb: diffuse,  a: occlusion
				float4 specular : SV_Target1; // rgb: specular, a: smoothness
				float4 normal   : SV_Target2; // rgb: normal,   a: unused
				float4 emission : SV_Target3; // rgb: emission, a: unused
				float  depth : SV_Depth;
			};

			// IDの取得
			inline int GetID(float2 uv1)
			{
				return (int)(uv1.x + 0.5) + _idOffset;
			}

			// 頂点を回転する
			float3 Rotate(float3 p, float3 rotation)
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
			v2f vert(appdata v)
			{
			#ifdef SHADER_API_D3D11
				// パーティクルを取得
				GPUParticle p = _particles[GetID(v.uv1)];
				// 頂点にスケールを掛ける
				v.vertex.xyz *= p.scale;
				// 頂点を回転する
				v.vertex.xyz = Rotate(v.vertex.xyz, p.rotation);
				// 頂点を移動する
				v.vertex.xyz += p.position;
				// 頂点の法線を計算
				v.normal = Rotate(v.normal, p.rotation);
			#endif
				// 出力
				v2f o;
				// uvを設定
				o.uv1 = v.uv1;
				// 頂点の座標に現在のビュー行列×射影行列を掛ける
				o.position = mul(UNITY_MATRIX_VP, v.vertex);
				// 法線を設定
				o.normal = v.normal;
				// フラグメントシェーダーに情報を渡す
				return o;
			}

			// フラグメントシェーダー
			gbuffer_out frag(v2f i) : SV_Target
			{
				// パーティクルを取得
				GPUParticle p;
			#ifdef SHADER_API_D3D11
				p = _particles[GetID(i.uv1)];
			#endif
				// パーティクルの速度を取得
				float3 v = p.velocity;
				// GBufferへの出力用
				gbuffer_out o;
				// 拡散色を設定
				o.diffuse = float4(v.y * 0.5, (abs(v.x) + abs(v.z)) * 0.1, -v.y * 0.5, 0);
				// 法線を設定
				o.normal = float4(i.normal, 1);
				// 拡散色の強度を設定
				o.emission = o.diffuse * 0.1;
				// 鏡面反射させない
				o.specular = 0;
				// 深度に座標を設定
				o.depth = i.position;
				// 出力
				return o;
			}

			// ShadowCaster用頂点シェーダー
			v2f_shadow vert_shadow(appdata v)
			{
			#ifdef SHADER_API_D3D11
				// パーティクルを取得
				GPUParticle p = _particles[GetID(v.uv1)];
				// 頂点にスケールを掛ける
				v.vertex.xyz *= p.scale;
				// 頂点にスケールを掛ける
				v.vertex.xyz = Rotate(v.vertex.xyz, p.rotation);
				// 頂点を移動する
				v.vertex.xyz += p.position;
			#endif
				// 出力
				v2f_shadow o;
				// ShadowCaster用に変換
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
				// 頂点の座標に現在のビュー行列×射影行列を掛ける
				o.pos = mul(UNITY_MATRIX_VP, v.vertex);
				return o;
			}

			// ShadowCaster用フラグメントシェーダー
			float4 frag_shadow(v2f_shadow i) : SV_Target
			{
				// ShadowCaster用に変換
				SHADOW_CASTER_FRAGMENT(i)
			}
		ENDCG

		// 遅延シェーディング
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

		// オブジェクトの深さをシャドウマップまたは深度テクスチャにレンダリング
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
	// 動作するシェーダーが見つからなかった場合
	FallBack "Diffuse"
}