// Amplify Occlusion 2 - Robust Ambient Occlusion for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AMPLIFY_AO_COMMON_HLSL
#define AMPLIFY_AO_COMMON_HLSL

//#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
//#include "../../Resources/PostProcessingStdLib.hlsl"

//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
//#include "ShaderGraphLibrary/Functions.hlsl"


#if !defined( UNITY_PI )
#define UNITY_PI PI
#endif

#if !defined( UNITY_HALF_PI )
#define UNITY_HALF_PI HALF_PI
#endif


float4		_ScreenToTargetScale;

float4 ComputeScreenPos( float4 pos, float projectionSign )
{
  float4 o = pos * 0.5f;
  o.xy = float2( o.x, o.y * projectionSign ) + o.w;
  o.zw = pos.zw;

  return o;
}


inline float2 AO_ComputeScreenPos( float4 aVertex )
{
	return ComputeScreenPos( aVertex, _ProjectionParams.x ).xy;
}


TEXTURE2D_SAMPLER2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture);
float4 _CameraMotionVectorsTexture_TexelSize;

inline half2 FetchMotion( const half2 aUV )
{
	return SAMPLE_TEXTURE2D( _CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, UnityStereoTransformScreenSpaceTex( aUV ) ).rg;
}


TEXTURE2D_SAMPLER2D( _AO_CurrMotionIntensity, sampler_AO_CurrMotionIntensity );
float4	_AO_CurrMotionIntensity_TexelSize;

inline half FetchMotionIntensity( const half2 aUV )
{
	return  SAMPLE_TEXTURE2D( _AO_CurrMotionIntensity, sampler_AO_CurrMotionIntensity, UnityStereoTransformScreenSpaceTex( aUV ) ).r;
}


TEXTURE2D_SAMPLER2D( _AO_CurrOcclusionDepth, sampler_AO_CurrOcclusionDepth );
float4	_AO_CurrOcclusionDepth_TexelSize;

inline half2 FetchOcclusionDepth( const half2 aUV )
{
	return SAMPLE_TEXTURE2D( _AO_CurrOcclusionDepth, sampler_AO_CurrOcclusionDepth, UnityStereoTransformScreenSpaceTex( aUV ) ).rg;
}


TEXTURE2D_SAMPLER2D( _CameraDepthTexture, sampler_CameraDepthTexture );
half4	_CameraDepthTexture_TexelSize;
//TEXTURE2D_ARRAY_FLOAT(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);

inline half SampleDepth0( const half2 aScreenPos )
{
	//return (aScreenPos).xxxx;
	return SAMPLE_TEXTURE2D( _CameraDepthTexture, sampler_CameraDepthTexture, aScreenPos );
    //return SampleSceneDepth(UnityStereoTransformScreenSpaceTex(uv)).r;
	//return SAMPLE_DEPTH_TEXTURE_LOD( _CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoTransformScreenSpaceTex( aScreenPos ), 0 );
}

TEXTURE2D_SAMPLER2D( _AO_SourceDepthMipmap, sampler_AO_SourceDepthMipmap );
half4		_AO_SourceDepthMipmap_TexelSize;

inline half SampleDepth( const half2 aScreenPos, half aLOD )
{
	return SAMPLE_TEXTURE2D_LOD( _AO_SourceDepthMipmap, sampler_AO_SourceDepthMipmap, UnityStereoTransformScreenSpaceTex( aScreenPos ), aLOD ).r;
}


// This is a dummy function, so the build will pass in the GTAO.cginc
// on PostProcessingStack, GTAO is using only normals from DepthBuffer ( None option )
inline half4 FetchDepthNormals( const half2 aScreenPos )
{
	return half4( (0).xxxx );
}


TEXTURE2D_SAMPLER2D( _GBufferTexture1, sampler_GBufferTexture1 );

inline half4 FetchGBufferNormals( const half2 aScreenPos )
{
	return SAMPLE_TEXTURE2D( _GBufferTexture1, sampler_GBufferTexture1, UnityStereoTransformScreenSpaceTex( aScreenPos * _ScreenToTargetScale.xy ) );
}


half2 Unpack888ToFloat2( half3 x )
{
	uint3 i = (uint3)( x * 255.0 );
	// 8 bit in lo, 4 bit in hi
	uint hi = i.z >> 4;
	uint lo = i.z & 15;
	uint2 cb = i.xy | uint2( lo << 8, hi << 8 );

	return cb / 4095.0;
}


half3 UnpackNormalOctQuadEncode( half2 f )
{
	half3 n = half3( f.x, f.y, 1.0 - abs( f.x ) - abs( f.y ) );
	half t = max( -n.z, 0.0 );
	n.xy += ( n.xy >= 0.0 ) ? -t.xx : t.xx;

	return normalize( n );
}

inline half3 FetchGBufferNormalWS( const half2 aScreenPos, const bool aIsGBufferOctaEncoded  )
{
	// HDSRP only uses OctaEncoded normals
	float4 normalBuffer = FetchGBufferNormals( aScreenPos );
	float2 octNormalWS = Unpack888ToFloat2( normalBuffer.rgb );
	float3 nWS = UnpackNormalOctQuadEncode( octNormalWS * 2.0 - 1.0 );

	return nWS;
}


TEXTURE2D_SAMPLER2D( _AO_TemporalAccumm, sampler_AO_TemporalAccumm );
float4	_AO_TemporalAccumm_TexelSize;

inline half4 FetchTemporal( const half2 aScreenPos )
{
	return SAMPLE_TEXTURE2D( _AO_TemporalAccumm, sampler_AO_TemporalAccumm, UnityStereoTransformScreenSpaceTex( aScreenPos ) );
}

TEXTURE2D_SAMPLER2D( _AO_CurrDepthSource, sampler_AO_CurrDepthSource );
half4		_AO_CurrDepthSource_TexelSize;

inline half FetchCurrDepthSource( const half2 aScreenPos )
{
	return SAMPLE_TEXTURE2D( _AO_CurrDepthSource, sampler_AO_CurrDepthSource, UnityStereoTransformScreenSpaceTex( aScreenPos ) ).r;
}


inline float2 EncodeFloatRG( float v )
{
	float2 kEncodeMul = float2(1.0, 255.0);
	float kEncodeBit = 1.0/255.0;
	float2 enc = kEncodeMul * v;
	enc = frac (enc);
	enc.x -= enc.y * kEncodeBit;
	return enc;
}

inline float DecodeFloatRG( float2 enc )
{
	float2 kDecodeDot = float2(1.0, 1/255.0);
	return dot( enc, kDecodeDot );
}


#include "../../Resources/CommonFunctions.cginc"


v2f_out vert( appdata v )
{
	v2f_out o;
	UNITY_SETUP_INSTANCE_ID( v );
	UNITY_TRANSFER_INSTANCE_ID( v, o );
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

	o.pos = float4( v.vertex.xy, 0.0, 1.0 );
	o.uv = TransformTriangleVertexToUV( v.vertex.xy );

#if UNITY_UV_STARTS_AT_TOP
	o.uv = o.uv * float2( 1.0, -1.0 ) + float2( 0.0, 1.0 );
#endif

	return o;
}


#endif
