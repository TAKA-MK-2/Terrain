Shader "Custom/GPUInstancing"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}

	CGINCLUDE
		#include "UnityCG.cginc"
		#include "Assets/Shaders/Libs/ColorUtil.cginc"
		#include "Assets/Shaders/Libs/Quaternion.cginc"
		#include "GPUParticleCommon.cginc"

		struct VertexData
		{
			float3 vertex;
			float3 normal;
			float2 uv;
			float4 tangent;
		};

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
			// 色
			float4 color;
			// 生存時間
			float lifeTime;
			// 経過時間
			float elapsedTime;
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
		StructuredBuffer<GPUParticleData> _particles;

		sampler2D _MainTex;

		float4 _MainTex_ST;
		float4 _RotationOffsetAxis;

		float3 _upVec;

		v2f vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
		{
			uint idx = _indices[vid];
			float4 pos = float4(_vertices[idx].vertex, 1.0);
			float2 uv = _vertices[idx].uv;
			float3 normal = _vertices[idx].normal;
			float4 tangent = _vertices[idx].tangent;

			float4 Q = getAngleAxisRotation(_RotationOffsetAxis.xyz, _RotationOffsetAxis.w);

			uint iidx = GetParticleIndex(iid);

			float4 rotation = qmul(float4(_particles[iidx].rotation, 1), Q);
			//float4 rotation = getAngleAxisRotation(_RotationOffsetAxis.xyz, _RotationOffsetAxis.w);

			pos.xyz *= _particles[iidx].scale;
			pos.xyz = rotateWithQuaternion(pos.xyz, rotation);
			pos.xyz += _particles[iidx].position;

			v2f o;
			//// TODO
			// 非ビルボード、座標ずれなし
			//o.pos = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_MV, pos));
			// ビルボード、座標ずれあり
			o.pos = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_MV, float4(0, 0, pos.z, 1)) + float4(pos.x, pos.y, 0, 0));
			////
			o.uv = uv;
			o.color = _particles[iidx].color;
		
			return o;
		}

		fixed4  frag(v2f i) : SV_Target
		{
			fixed4 col = tex2D(_MainTex, i.uv) * i.color;
			return col;
		}
	ENDCG

	SubShader
	{
		//Tags{ "RenderType" = "Opaque" }
		Tags{ "RenderType" = "Transparent" "IgnoreProjector" = "True"}
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
			#pragma shader_feature GPUPARTICLE_CULLING_ON
			#pragma multi_compile_instancing

		ENDCG
		}
	}

	Fallback Off
}
