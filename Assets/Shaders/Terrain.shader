Shader "Custum/Terrain"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}

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

			sampler2D _MainTex;

			StructuredBuffer<float4> positionBuffer;
			StructuredBuffer<float3> _vertices;
			int _numInstances;
			int _numVertices;
			float _distance;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color: COLOR;
			};

			v2f vert(appdata_full v, uint instanceID : SV_InstanceID)
			{
				int index = instanceID + (v.texcoord.y * _numVertices + v.texcoord.x) + (instanceID / (_numVertices - 1));
				float3 vertex = _vertices[index];
				//float4 position = positionBuffer[instanceID];
				//float3 localPosition = v.vertex.xyz * _distance;
				//float4 q = quaternion(float3(1, 0, 0), 90);
				//localPosition = rotateWithQuaternion(localPosition, quaternion(float3(1, 0, 0), 90));
				//float3 worldPosition = vertex + localPosition;
				vertex = rotateWithQuaternion(vertex, quaternion(float3(1, 0, 0), 0));
				v2f o;
				o.pos = mul(UNITY_MATRIX_VP, float4(vertex, 1.0f));
				o.uv = v.texcoord;
				float col = instanceID / (float)_numInstances;
				o.color = float4(col, col, col, 1);

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				//fixed4 col = tex2D(_MainTex, i.uv) * i.color;
				//return col;
				return i.color;
			}

			ENDCG
		}
	}
}
