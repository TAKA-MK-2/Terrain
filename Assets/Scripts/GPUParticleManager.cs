using UnityEngine;
using System.Runtime.InteropServices;
using Utility;

public class GPUParticleManager : MonoBehaviour
{
    #region define
    // パーティクル情報
    struct GPUParticleData
    {
        // アクティブか判断する
        public bool isActive;
        // 座標
        public Vector3 position;
        // 速度
        public Vector3 velocity;
        // スケール
        public float scale;
        // 開始スケール
        public float startScale;
        // 最終スケール
        public float endScale;
        // 色
        public Vector4 color;
        // 生存時間
        public float lifeTime;
        // 経過時間
        public float elapsedTime;
    }

    // ComputeShaderのスレッド数
    private const int THREAD_NUM_X = 32;
    #endregion

    #region SerializeField
    // コンピュートシェーダー
    [SerializeField] ComputeShader _computeShader;
    // 最大パーティクル数
    [SerializeField] int _numMaxParticles = 1024;
    // エミット数
    [SerializeField] int _numMaxEmitParticles = 32;
    // エミッッターのサイズ
    [SerializeField] Vector3 _range = Vector3.zero;
    // 最低速度
    [SerializeField] Vector3 _minVelocity = Vector3.zero;
    // 最高速度
    [SerializeField] Vector3 _maxVelocity = Vector3.up;
    // 重力
    [SerializeField] float _gravity = 2f;
    // 開始スケール
    [SerializeField] float _startScale = 1;
    // 最終スケール
    [SerializeField] float _endScale = 2;
    // 色情報
    [SerializeField] Vector3 _color = Vector4.one;
    // 生存時間
    [SerializeField] float _lifeTime = 1;
    #endregion

    #region private variable
    // パーティクル情報
    private ComputeBuffer m_particlesBuffer;
    // 使用中のパーティクルの要素番号
    private ComputeBuffer m_activeParticlesBuffer;
    // 未使用のパーティクルの要素番号
    private ComputeBuffer m_particlePoolBuffer;
    // 使用中のパーティクルの個数
    private ComputeBuffer m_activeParticleCountsBuffer;
    // 未使用のパーティクルの個数
    private ComputeBuffer m_particlePoolCountsBuffer;
    // [0]インスタンスあたりの頂点数 [1]インスタンス数 [2]開始する頂点位置 [3]開始するインスタンス
    private int[] m_particleCounts = { 1, 1, 0, 0 };
    // パーティクル数
    private int m_numParticles = 0;
    // エミットパーティクル数
    private int m_numEmitParticles = 0;
    // 初期化カーネル
    private int m_initKernel = -1;
    // エミットカーネル
    private int m_emitKernel = -1;
    // 更新カーネル
    private int m_updateKernel = -1;
    // 使用できるパーティクル数
    private int m_particlePoolNum = 0;
    #endregion

    #region private method
    // 初期化処理
    private void Initialize()
    {
        // パーティクル数をスレッド数の倍数にする
        m_numParticles = Mathf.CeilToInt(_numMaxParticles / (float)THREAD_NUM_X) * THREAD_NUM_X;
       
        // エミット数をスレッド数の倍数にする
        m_numEmitParticles = Mathf.CeilToInt(_numMaxEmitParticles / (float)THREAD_NUM_X) * THREAD_NUM_X;

        // バッファを生成
        m_particlesBuffer = new ComputeBuffer(m_numParticles, Marshal.SizeOf(typeof(GPUParticleData)), ComputeBufferType.Default);
        m_activeParticlesBuffer = new ComputeBuffer(m_numParticles, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
        m_particlePoolBuffer = new ComputeBuffer(m_numParticles, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
        m_activeParticleCountsBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
        m_particlePoolCountsBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);

        // バッファにデータを渡す
        m_activeParticleCountsBuffer.SetData(m_particleCounts);
        m_particlePoolCountsBuffer.SetData(m_particleCounts);

        // カーネルを設定
        m_initKernel = _computeShader.FindKernel("Init");
        m_emitKernel = _computeShader.FindKernel("Emit");
        m_updateKernel = _computeShader.FindKernel("Update");

        // 変数を設定
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._gravity), _gravity);
        
        // バッファを設定
        _computeShader.SetBuffer(m_initKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particlesBuffer), m_particlesBuffer);
        _computeShader.SetBuffer(m_initKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._deadParticlesBuffer), m_particlePoolBuffer);

        // 初期化カーネルを実行
        _computeShader.Dispatch(m_initKernel, m_numParticles / THREAD_NUM_X, 1, 1);
    }

    // パーティクルの更新処理
    private void UpdateParticle()
    {
        // バッファのカウンターをリセット
        m_activeParticlesBuffer.SetCounterValue(0);

        // 変数を設定
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._deltaTime), Time.deltaTime);

        // バッファを設定
        _computeShader.SetBuffer(m_updateKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particlesBuffer), m_particlesBuffer);
        _computeShader.SetBuffer(m_updateKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._deadParticlesBuffer), m_particlePoolBuffer);
        _computeShader.SetBuffer(m_updateKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._activeParticlesBuffer), m_activeParticlesBuffer);

        // 更新カーネルを実行
        _computeShader.Dispatch(m_updateKernel, m_numParticles / THREAD_NUM_X, 1, 1);

        // 使用中のパーティクル数を取得
        ComputeBuffer.CopyCount(m_activeParticlesBuffer, m_activeParticleCountsBuffer, 0);
    }

    // バッファの開放処理
    private void ReleaseBuffers()
    {
        if (m_activeParticlesBuffer != null)
        {
            m_activeParticlesBuffer.Release();
            m_activeParticlesBuffer = null;
        }

        if (m_particlePoolBuffer != null)
        {
            m_particlePoolBuffer.Release();
            m_particlePoolBuffer = null;
        }

        if (m_particlesBuffer != null)
        {
            m_particlesBuffer.Release();
            m_particlesBuffer = null;
        }

        if (m_particlePoolCountsBuffer != null)
        {
            m_particlePoolCountsBuffer.Release();
            m_particlePoolCountsBuffer = null;
        }

        if (m_activeParticleCountsBuffer != null)
        {
            m_activeParticleCountsBuffer.Release();
            m_activeParticleCountsBuffer = null;
        }
    }
    #endregion

    #region public method
    // パーティクルの発生処理
    public void EmitParticle(Vector3 _position)
    {
        // 使用できるパーティクル数を取得
        ComputeBuffer.CopyCount(m_particlePoolBuffer, m_particlePoolCountsBuffer, 0);
        m_particlePoolCountsBuffer.GetData(m_particleCounts);
        m_particlePoolNum = m_particleCounts[0];

        // エミット数未満ならエミット処理をしない
        if (m_particleCounts[0] < m_numEmitParticles) return;

        // 変数を設定
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._elapsedTime), Time.time);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._position), _position);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._range), _range);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._minVelocity), _minVelocity);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._maxVelocity), _maxVelocity);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._startScale), _startScale);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._endScale), _endScale);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._color), _color);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._lifeTime), _lifeTime);

        // バッファを設定
        _computeShader.SetBuffer(m_emitKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particlePoolBuffer), m_particlePoolBuffer);
        _computeShader.SetBuffer(m_emitKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particlesBuffer), m_particlesBuffer);

        // エミットカーネルを実行
        _computeShader.Dispatch(m_emitKernel, m_numEmitParticles / THREAD_NUM_X, 1, 1);
    }
    #endregion

    #region getter
    // パーティクル数を取得する
    public int GetParticleNum() { return m_numParticles; }

    // パーティクル構造体のバッファを取得する
    public ComputeBuffer GetParticleBuffer() { return m_particlesBuffer; }

    // 使用中のパーティクルのインデックスのリストを取得する
    public ComputeBuffer GetActiveParticleBuffer() { return m_activeParticlesBuffer; }

    // 使用中のパーティクル個数バッファ
    public ComputeBuffer GetParticleCountBuffer() { return m_activeParticleCountsBuffer; }
    #endregion

    #region unity method
    void Awake()
    {
        ReleaseBuffers();
        Initialize();
    }

    void Update()
    {
        UpdateParticle();
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }
    #endregion
}
