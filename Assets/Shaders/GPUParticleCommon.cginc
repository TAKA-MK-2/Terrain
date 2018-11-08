#ifndef GPUPARTICLE_COMMON_INCLUDED
	#define GPUPARTICLE_COMMON_INCLUDED

	StructuredBuffer<uint> _particleActiveList;
	StructuredBuffer<uint> _inViewsList;

	// パーティクルの番号を取得
	uint GetParticleIndex(int index)
	{
		#ifdef GPUPARTICLE_CULLING_ON
			// 写っているパーティクルの番号のバッファから取得
			return _inViewsList[index];
		#else
			// アクティブ状態のパーティクルの番号のバッファから取得
			return _particleActiveList[index];
		#endif
	}
#endif // GPUPARTICLE_COMMON_INCLUDED
