// Amplify Occlusion 2 - Robust Ambient Occlusion for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/Amplify Occlusion/BlurPostProcessing"
{
	Properties { }
	HLSLINCLUDE
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
		#pragma exclude_renderers gles d3d11_9x n3ds

		#include "Common.hlsl"
		#include "../../Resources/BlurFunctions.cginc"
	ENDHLSL


	SubShader
	{
		ZTest Always Cull Off ZWrite Off

		// 0 => BLUR HORIZONTAL R:1
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_1x( IN, half2( _AO_CurrOcclusionDepth_TexelSize.x, 0 ) ); } ENDHLSL }

		// 1 => BLUR VERTICAL R:1
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_1x( IN, half2( 0, _AO_CurrOcclusionDepth_TexelSize.y ) ); } ENDHLSL }

		// 2 => BLUR HORIZONTAL R:2
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_2x( IN, half2( _AO_CurrOcclusionDepth_TexelSize.x, 0 ) ); } ENDHLSL }

		// 3 => BLUR VERTICAL R:2
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_2x( IN, half2( 0, _AO_CurrOcclusionDepth_TexelSize.y ) ); } ENDHLSL }

		// 4 => BLUR HORIZONTAL R:3
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_3x( IN, half2( _AO_CurrOcclusionDepth_TexelSize.x, 0 ) ); } ENDHLSL }

		// 5 => BLUR VERTICAL R:3
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_3x( IN, half2( 0, _AO_CurrOcclusionDepth_TexelSize.y ) ); } ENDHLSL }

		// 6 => BLUR HORIZONTAL R:4
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_4x( IN, half2( _AO_CurrOcclusionDepth_TexelSize.x, 0 ) ); } ENDHLSL }

		// 7 => BLUR VERTICAL R:4
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_4x( IN, half2( 0, _AO_CurrOcclusionDepth_TexelSize.y ) ); } ENDHLSL }

		// 8 => BLUR HORIZONTAL INTENSITY
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_Intensity( IN, half2( _AO_CurrMotionIntensity_TexelSize.x, 0 ) ); } ENDHLSL }

		// 9 => BLUR VERTICAL INTENSITY
		Pass { HLSLPROGRAM half4 frag( v2f_in IN ) : SV_Target { return blur1D_Intensity( IN, half2( 0, _AO_CurrMotionIntensity_TexelSize.y ) ); } ENDHLSL }
	}

	Fallback Off
}
