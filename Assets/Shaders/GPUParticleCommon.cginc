#ifndef GPUPARTICLE_COMMON_INCLUDED
	#define GPUPARTICLE_COMMON_INCLUDED

	StructuredBuffer<uint> _activeParticlesBuffer;
	StructuredBuffer<uint> _inViewParticlesBuffer;

	// パーティクルの番号を取得
	uint GetParticleIndex(int index)
	{
		#ifdef GPUPARTICLE_CULLING_ON
			// 写っているパーティクルの番号のバッファから取得
			return _inViewParticlesBuffer[index];
		#else
			// アクティブ状態のパーティクルの番号のバッファから取得
			return _activeParticlesBuffer[index];
		#endif
	}
#endif // GPUPARTICLE_COMMON_INCLUDED
