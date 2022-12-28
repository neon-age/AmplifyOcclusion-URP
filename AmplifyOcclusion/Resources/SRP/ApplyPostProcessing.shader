// Amplify Occlusion 2 - Robust Ambient Occlusion for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/Amplify Occlusion/ApplyPostProcessing"
{
	HLSLINCLUDE
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
		#pragma exclude_renderers gles d3d11_9x n3ds
		#pragma multi_compile_instancing

		#include "Common.hlsl"
		#include "../../Resources/TemporalFilter.cginc"
		#include "../../Resources/ApplyPostEffect.cginc"

		//TEXTURE2D_SAMPLER2D( _MainTex, sampler_MainTex );
		sampler2D _MainTex;
		SamplerState sampler_MainTex;

		PostEffectOutputTemporal ApplyPostEffectTemporal( v2f_in IN, const bool aUseMotionVectors )
		{
			UNITY_SETUP_INSTANCE_ID( IN );
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

			const float2 screenPos = IN.uv.xy;

			const half2 occlusionDepth = FetchOcclusionDepth( screenPos );

			PostEffectOutputTemporal OUT;

			const half4 srcColor = tex2D( _MainTex, UnityStereoTransformScreenSpaceTex( screenPos ) );

			if( occlusionDepth.y < HALF_MAX )
			{
				const half linear01Depth = occlusionDepth.y * ONE_OVER_DEPTH_SCALE;
				const half sampledDepth = Linear01ToSampledDepth( linear01Depth );
				const half linearEyeDepth = occlusionDepth.y * _AO_BufDepthToLinearEye;

				half occlusion;
				const half4 temporalAcc = TemporalFilter( screenPos, occlusionDepth.x, sampledDepth, linearEyeDepth, linear01Depth, aUseMotionVectors, occlusion );

				const half4 occlusionRGBA = CalcOcclusion( occlusion, linearEyeDepth );

				const half4 outColor = half4( srcColor.rgb * lerp( (1).xxx, occlusionRGBA.rgb, (srcColor.a).xxx ), srcColor.a );

				OUT.occlusionColor = outColor;
				OUT.temporalAcc = temporalAcc;
			}
			else
			{
				OUT.occlusionColor = srcColor;
				OUT.temporalAcc = half4( (1).xxxx );
			}

			return OUT;
		}


		half4 ApplyPostEffectCombineFromTemporal( v2f_in IN )
		{
			UNITY_SETUP_INSTANCE_ID( IN );
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

			const half2 screenPos = IN.uv.xy;

			const half depthSample = SampleDepth0( screenPos );

			const half2 occlusionLinearEyeDepth = ComputeCombineDownsampledOcclusionFromTemporal( screenPos, depthSample );

			const half4 occlusionRGBA = CalcOcclusion( occlusionLinearEyeDepth.x, occlusionLinearEyeDepth.y );
			
			const half4 srcColor = tex2D( _MainTex, UnityStereoTransformScreenSpaceTex( screenPos ) );

			const half4 outColor = half4( srcColor.rgb * lerp( (1).xxx, occlusionRGBA.rgb, (srcColor.a).xxx ), srcColor.a );

			return half4( ( occlusionLinearEyeDepth.y < HALF_MAX )?outColor: srcColor );
		}


		half4 ApplyPostEffect( v2f_in IN )
		{
			UNITY_SETUP_INSTANCE_ID( IN );
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

			const half4 srcColor = tex2D( _MainTex, IN.uv);
			half4 outColor = _ApplyPostEffect(IN, srcColor);

			return lerp(srcColor, outColor, _AO_Levels.a);
		}
	ENDHLSL


	SubShader
	{
		ZTest Always Cull Off ZWrite Off

		// -- APPLICATION METHODS --------------------------------------------------------------
		// 0 => APPLY DEBUG
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return ApplyDebug( IN ); } ENDHLSL }
		// 1 => APPLY DEBUG Temporal
		Pass { HLSLPROGRAM PostEffectOutputTemporal frag( v2f_in IN ) { return ApplyDebugTemporal( IN, false ); } ENDHLSL }
		Pass { HLSLPROGRAM PostEffectOutputTemporal frag( v2f_in IN ) { return ApplyDebugTemporal( IN, true ); } ENDHLSL }

		// 3 => NOT USED
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return half4( 255, 0, 255, 1 ); } ENDHLSL }
		// 4 => NOT USED
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return half4( 255, 0, 255, 1 ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return half4( 255, 0, 255, 1 ); } ENDHLSL }

		// 6 => NOT USED
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return half4( 255, 0, 255, 1 ); } ENDHLSL }
		// 7 => NOT USED
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return half4( 255, 0, 255, 1 ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return half4( 255, 0, 255, 1 ); } ENDHLSL }

		// 9 => APPLY POST-EFFECT
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return ApplyPostEffect( IN ); } ENDHLSL }
		// 10 => APPLY POST-EFFECT Temporal
		Pass { HLSLPROGRAM PostEffectOutputTemporal frag( v2f_in IN ) { return ApplyPostEffectTemporal( IN, false ); } ENDHLSL }
		Pass { HLSLPROGRAM PostEffectOutputTemporal frag( v2f_in IN ) { return ApplyPostEffectTemporal( IN, true ); } ENDHLSL }

		// 12 => NOT USED
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return half4( 255, 0, 255, 1 ); } ENDHLSL }

		// 13 => NOT USED
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return half4( 255, 0, 255, 1 ); } ENDHLSL }

		// 14 => APPLY DEBUG Combine from Temporal
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return ApplyDebugCombineFromTemporal( IN ); } ENDHLSL }

		// 15 => APPLY POST-EFFECT Combine from Temporal
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return ApplyPostEffectCombineFromTemporal( IN ); } ENDHLSL }
	}

	Fallback Off
}
