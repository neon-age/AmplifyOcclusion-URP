// Amplify Occlusion 2 - Robust Ambient Occlusion for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AMPLIFY_AO_APPLY_POSTEFFECT
#define AMPLIFY_AO_APPLY_POSTEFFECT

struct PostEffectOutputTemporal
{
	half4 occlusionColor : SV_Target0;
	half4 temporalAcc : SV_Target1;
};

inline half4 _CalcOcclusionColor( const half aOcclusion )
{
	const half distanceFade = saturate(_AO_FadeParams.x * _AO_FadeParams.y);
	const half exponent = lerp( _AO_PowExponent, _AO_FadeValues.z, distanceFade );
	//const half occlusion = pow( max( aOcclusion, 0.0 ), exponent );
	half3 tintedOcclusion = lerp( _AO_Levels.rgb, _AO_FadeToTint.rgb, distanceFade );
	//tintedOcclusion = lerp( tintedOcclusion, ( 1 ).xxx, aOcclusion.xxx );
	//const half intensity = lerp( _AO_Levels.a, _AO_FadeValues.x, distanceFade );
	return half4( tintedOcclusion.rgb, aOcclusion );
	//return lerp( 0, half4( tintedOcclusion.rgb, occlusion ), intensity );
}
sampler2D _OcclusionRamp;
half4 _ApplyPostEffect( v2f_in IN, half4 srcColor )
{
	half2 screenPos = IN.uv.xy;
	//const half linearEyeDepth = occlusionDepth.y * _AO_BufDepthToLinearEye;
	half2 occlusionPacked = FetchOcclusionDepth( screenPos );
	half occlusion = occlusionPacked.r;
	occlusion = occlusion;
	const half distanceFade = saturate(_AO_FadeParams.x * _AO_FadeParams.y);
	const half exponent = lerp( _AO_PowExponent, _AO_FadeValues.z, distanceFade );
	
	half occ = occlusion;
	occ =  pow( max( occ, 0.0 ), exponent );
	//occ = smoothstep(occ - exponent, occ + exponent, exponent);
	half4 occlusionRGBA = _CalcOcclusionColor( occ );
	occ = occlusionRGBA.a;
	half3 ramp = tex2D(_OcclusionRamp, half2(clamp(occ, 0.01, 0.99), 0)).rgb;
	
	// could we use mipmaps to mimic blur for cheap and dirty color bleed?
	//half2 uv =  UnityStereoTransformScreenSpaceTex( screenPos ) ;
	//float2 uv_dx = clamp( ddx( uv ), minval, maxval );
	//float2 uv_dy = clamp( ddy( uv ), minval, maxval );
	//half4 texsample = tex2D( _MainTex, uv, uv_dx, uv_dy );
	
	//const half4 srcColor = tex2D( _MainTex, uv);
	occlusionRGBA.rgb *= ramp;
	
	//half4 outColor = half4( srcColor.rgb * lerp( (1).xxx, (srcColor + occlusionRGBA.rgb), srcColor.a), srcColor.a );
	return half4(lerp(srcColor.rgb, (srcColor.rgb * _AO_Levels.rgb * ramp.rgb), 1 - ramp), srcColor.a);
	//return lerp(srcColor, outColor, _AO_Levels.a);
}

half4 ApplyDebug( const v2f_in IN )
{
	UNITY_SETUP_INSTANCE_ID( IN );
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );
/*
	const half2 screenPos = IN.uv.xy;

	const half2 occlusionDepth = FetchOcclusionDepth( screenPos );

	const half linearEyeDepth = occlusionDepth.y * _AO_BufDepthToLinearEye;

	const half4 occlusionRGBA = CalcOcclusion( occlusionDepth.x, linearEyeDepth );

	return half4( occlusionRGBA.rgb, 1 );*/

	return _ApplyPostEffect(IN, 1);
}


half4 ApplyDebugCombineFromTemporal( const v2f_in IN )
{
	UNITY_SETUP_INSTANCE_ID( IN );
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

	const half2 screenPos = IN.uv.xy;

	const half depthSample = SampleDepth0( screenPos );

	const half2 occlusionLinearEyeDepth = ComputeCombineDownsampledOcclusionFromTemporal( screenPos, depthSample );

	const half4 occlusionRGBA = CalcOcclusion( occlusionLinearEyeDepth.x, occlusionLinearEyeDepth.y );

	return half4( occlusionRGBA.rgb, 1 );
}


half4 ApplyCombineFromTemporal( const v2f_in IN )
{
	UNITY_SETUP_INSTANCE_ID( IN );
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

	const half2 screenPos = IN.uv.xy;

	const half depthSample = SampleDepth0( screenPos );

	const half2 occlusionLinearEyeDepth = ComputeCombineDownsampledOcclusionFromTemporal( screenPos, depthSample );

	const half4 occlusionRGBA = CalcOcclusion( occlusionLinearEyeDepth.x, occlusionLinearEyeDepth.y );

	return half4( ( occlusionLinearEyeDepth.y < HALF_MAX )?occlusionRGBA.rgb:(1).xxx, 1 );
}


PostEffectOutputTemporal ApplyDebugTemporal( const v2f_in IN, const bool aUseMotionVectors )
{
	UNITY_SETUP_INSTANCE_ID( IN );
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

	const float2 screenPos = IN.uv.xy;

	const half2 occlusionDepth = FetchOcclusionDepth( screenPos );

	PostEffectOutputTemporal OUT;

	if( occlusionDepth.y < HALF_MAX )
	{
		const half linear01Depth = occlusionDepth.y * ONE_OVER_DEPTH_SCALE;
		const half sampledDepth = Linear01ToSampledDepth( linear01Depth );
		const half linearEyeDepth = occlusionDepth.y * _AO_BufDepthToLinearEye;

		half occlusion;
		const half4 temporalAcc = TemporalFilter( screenPos, occlusionDepth.x, sampledDepth, linearEyeDepth, linear01Depth, aUseMotionVectors, occlusion );

		const half4 occlusionRGBA = CalcOcclusion( occlusion, linearEyeDepth );

		OUT.occlusionColor = occlusionRGBA;
		OUT.temporalAcc = temporalAcc;
	}
	else
	{
		OUT.occlusionColor = half4( (1).xxxx );
		OUT.temporalAcc = half4( (1).xxxx );
	}

	return OUT;
}


#endif
