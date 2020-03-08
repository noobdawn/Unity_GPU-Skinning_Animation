// 可定制尺寸和深度的顶点动画蒙皮写法
Shader "Mobile-Game/VertexAnim_Improved"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_AnimTex ("Animation", 2D) = "black" {}
		_VertexCount("Vertex Count", int) = 50
		_FrameCount("Frame Count", int) = 50
		_Interval("Interval", Range(0.001, 1)) = 0.03333
		[KeywordEnum(_1, _2, _3)]_Accuracy("Accuracy", float) = 0
		_Random("Random", Range(0, 1)) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma multi_compile _ACCURACY__1 _ACCURACY__2 _ACCURACY__3
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			}; 
			
			UNITY_INSTANCING_BUFFER_START(Props)
				// 差异化控制
				UNITY_DEFINE_INSTANCED_PROP(float, _Random)
			UNITY_INSTANCING_BUFFER_END(Props)

			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _AnimTex;
			float4 _AnimTex_TexelSize;
			int _VertexCount, _FrameCount;
			float _Interval;

			// 转换成图片空间下的uv
			float2 uvConvert(float total)
			{
				float new_y = total / _AnimTex_TexelSize.z;
				float new_x = floor(fmod(new_y, 1.0) * _AnimTex_TexelSize.z);
				new_y = floor(new_y);
				return float2(new_x, new_y);
			}
			
			v2f vert (appdata v, uint vid : SV_VertexID)
			{
				UNITY_SETUP_INSTANCE_ID(v);
				v2f o;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				float y = _Time.y / _Interval + UNITY_ACCESS_INSTANCED_PROP(Props, _Random) * _FrameCount;
				y = floor(y - floor(y / _FrameCount) * _FrameCount);

#if _ACCURACY__1
				float total = y * _VertexCount + vid;
				float2 newUv = uvConvert(total);
				float2 animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
				float4 modelPos = tex2Dlod(_AnimTex, float4(animUv, 0, 0));
#endif

#if _ACCURACY__2
				float total = y * _VertexCount * 2 + vid * 2;
				float2 newUv = uvConvert(total);
				float2 animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
				fixed4 original = tex2Dlod(_AnimTex, float4(animUv, 0, 0));
				newUv = uvConvert(total + 1);
				animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
				fixed4 addon = tex2Dlod(_AnimTex, float4(animUv, 0, 0));
				float4 modelPos = float4(original.xyz + addon.xyz * 0.00390625, 1);
#endif

#if _ACCURACY__3
				float total = y * _VertexCount * 3 + vid * 3;
				float2 newUv = uvConvert(total);
				float2 animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
				fixed4 original = tex2Dlod(_AnimTex, float4(animUv, 0, 0));
				newUv = uvConvert(total + 1);
				animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
				fixed4 addon = tex2Dlod(_AnimTex, float4(animUv, 0, 0));
				float4 modelPos = float4(original.xyz + addon.xyz * 0.00390625, 1);
				newUv = uvConvert(total + 2);
				animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
				addon = tex2Dlod(_AnimTex, float4(animUv, 0, 0));
				modelPos.xyz += addon.xyz / 65536;
#endif

				modelPos.xyz -= 0.5;
				o.vertex = UnityObjectToClipPos(modelPos);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				return col;
			}
			ENDCG
		}
	}
}
