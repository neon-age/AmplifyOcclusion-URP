// Amplify Occlusion 2 - Robust Ambient Occlusion for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/Amplify Occlusion/OcclusionPostProcessing"
{
	HLSLINCLUDE
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
		#pragma exclude_renderers gles d3d11_9x n3ds

		#include "Common.hlsl"
		#include "../../Resources/GTAO.cginc"
		#include "../../Resources/OcclusionFunctions.cginc"
		#include "../../Resources/TemporalFilter.cginc"
	ENDHLSL

	SubShader
	{
		ZTest Always
		Cull Off
		ZWrite Off

		// 0-3 => FULL OCCLUSION - LOW QUALITY                    directionCount / sampleCount
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 2, 4, NORMALS_NONE ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 2, 4, NORMALS_CAMERA ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 2, 4, NORMALS_GBUFFER ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 2, 4, NORMALS_GBUFFER_OCTA_ENCODED ); } ENDHLSL }

		// 04-07 => FULL OCCLUSION / MEDIUM QUALITY
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 2, 6, NORMALS_NONE ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 2, 6, NORMALS_CAMERA ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 2, 6, NORMALS_GBUFFER ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 2, 6, NORMALS_GBUFFER_OCTA_ENCODED ); } ENDHLSL }

		// 08-11 => FULL OCCLUSION - HIGH QUALITY
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 3, 8, NORMALS_NONE ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 3, 8, NORMALS_CAMERA ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 3, 8, NORMALS_GBUFFER ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 3, 8, NORMALS_GBUFFER_OCTA_ENCODED ); } ENDHLSL }

		// 12-15 => FULL OCCLUSION / VERYHIGH QUALITY
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 4, 10, NORMALS_NONE ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 4, 10, NORMALS_CAMERA ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 4, 10, NORMALS_GBUFFER ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, false, 4, 10, NORMALS_GBUFFER_OCTA_ENCODED ); } ENDHLSL }




		// 16-19 => FULL OCCLUSION - LOW QUALITY                    directionCount / sampleCount
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 2, 4, NORMALS_NONE ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 2, 4, NORMALS_CAMERA ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 2, 4, NORMALS_GBUFFER ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 2, 4, NORMALS_GBUFFER_OCTA_ENCODED ); } ENDHLSL }

		// 20-23 => FULL OCCLUSION / MEDIUM QUALITY
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 2, 6, NORMALS_NONE ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 2, 6, NORMALS_CAMERA ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 2, 6, NORMALS_GBUFFER ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 2, 6, NORMALS_GBUFFER_OCTA_ENCODED ); } ENDHLSL }

		// 24-27 => FULL OCCLUSION - HIGH QUALITY
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 3, 8, NORMALS_NONE ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 3, 8, NORMALS_CAMERA ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 3, 8, NORMALS_GBUFFER ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 3, 8, NORMALS_GBUFFER_OCTA_ENCODED ); } ENDHLSL }

		// 28-31 => FULL OCCLUSION / VERYHIGH QUALITY
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 4, 10, NORMALS_NONE ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 4, 10, NORMALS_CAMERA ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 4, 10, NORMALS_GBUFFER ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return GTAO( IN, true, 4, 10, NORMALS_GBUFFER_OCTA_ENCODED ); } ENDHLSL }


		// 32 => CombineDownsampledOcclusionDepth
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return CombineDownsampledOcclusionDepth( IN );	} ENDHLSL	}

		// 33 => Neighbor Motion Intensity
		Pass { HLSLPROGRAM half2 frag( v2f_in IN ) : SV_Target { return NeighborMotionIntensity( IN ); } ENDHLSL }

		// 34 => ClearTemporal
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return ClearTemporal( IN ); } ENDHLSL }

		// 35 => ScaleDownCloserDepthEven
		Pass { HLSLPROGRAM float frag( v2f_in IN ) : SV_Target { return ScaleDownCloserDepthEven( IN ); } ENDHLSL }
		
		// 36 => ScaleDownCloserDepthEven_CameraDepthTexture
		Pass { HLSLPROGRAM float frag( v2f_in IN ) : SV_Target { return ScaleDownCloserDepthEven_CameraDepthTexture( IN ); } ENDHLSL }

		// 37 => Temporal
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return Temporal( IN, false ); } ENDHLSL }
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return Temporal( IN, true ); } ENDHLSL }
	}
}

