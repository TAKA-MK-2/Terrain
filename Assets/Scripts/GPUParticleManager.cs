using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Utility;

public struct GPUParticleData
{
    // アクティブか判断する
    public bool isActive;
    // 座標
    public Vector3 position;
    // 速度
    public Vector3 velocity;
    // 回転
    public Vector3 rotation;
    // 角速度
    public Vector3 angVelocity;
    // スケール
    public float scale;
    // 開始スケール
    public float startScale;
    // 最終スケール
    public float endScale;
    // 生存時間
    public float lifeTime;
    // 経過時間
    public float elapsedTime;
}

public class GPUParticleManager : MonoBehaviour
{
    // 定数
    #region define
    // ComputeShaderのスレッド数
    protected const int THREAD_NUM_X = 32;
    #endregion

    // エディターから設定する変数
    #region SerializeField
    // コンピュートシェーダー
    [SerializeField] ComputeShader _computeShader;
    // 最大パーティクル数
    [SerializeField] int _numMaxParticles = 1048576;
    // エミット数
    [SerializeField] int _numMaxEmitParticles = 1024;
    // エミッッターのサイズ
    [SerializeField] Vector3 _range = Vector3.zero;
    // エミットの方向
    [SerializeField] Vector3 _direction = Vector3.up;
    // 速度
    [SerializeField] Vector3 _velocity = Vector3.zero;
    // 重力
    [SerializeField] float _gravity = 2f;
    // 角速度
    [SerializeField] Vector3 _angVelocity = Vector3.zero;
    // 開始スケール
    [SerializeField] float _startScale = 1;
    // 最終スケール
    [SerializeField] float _endScale = 2;
    // 生存時間
    [SerializeField] float _lifeTime = 1;
    // 彩度
    [Range(0, 1)]
    [SerializeField] float _sai = 1;
    // 明るさ
    [Range(0, 1)]
    [SerializeField] float _val = 1;
    // カメラ
    [SerializeField] Camera _camera;
    #endregion

    // メンバ変数
    #region private
    // パーティクル構造体のバッファ
    private ComputeBuffer m_particlesBuffer;
    // 使用中のパーティクルのインデックスのリスト
    private ComputeBuffer m_particleActiveBuffer;
    // 未使用のパーティクルのインデックスのリスト
    private ComputeBuffer m_particlePoolBuffer;
    // particleActiveBuffer内の個数バッファ
    private ComputeBuffer m_particleActiveCountBuffer;
    // particlePoolBuffer内の個数バッファ
    private ComputeBuffer m_particlePoolCountBuffer;
    // パーティクル数
    private int m_numParticles = 0;
    // エミットパーティクル数
    private int m_numEmitParticles = 0;
    // [0]インスタンスあたりの頂点数 [1]インスタンス数 [2]開始する頂点位置 [3]開始するインスタンス
    private int[] m_particleCounts = { 1, 1, 0, 0 };
    // 初期化カーネル
    private int m_initKernel = -1;
    // エミットカーネル
    private int m_emitKernel = -1;
    // 更新カーネル
    private int m_updateKernel = -1;
    // 使用できるパーティクル数
    private int m_particlePoolNum = 0;

    // 初期化を行ったか判断する
    private bool m_isInitialized = false;
    #endregion

    // デバッグ用
    #region debug
    private int[] debugCounts = { 0, 0, 0, 0 };
    #endregion

    // ゲッター
    #region get
    // パーティクル数を取得する
    public int GetParticleNum() { return m_numParticles; }

    // アクティブなパーティクルの数を取得（デバッグ機能）
    public int GetActiveParticleNum()
    {
        m_particleActiveCountBuffer.GetData(debugCounts);
        return debugCounts[1];
    }

    // パーティクル構造体のバッファを取得する
    public ComputeBuffer GetParticleBuffer() { return m_particlesBuffer; }

    // 使用中のパーティクルのインデックスのリストを取得する
    public ComputeBuffer GetActiveParticleBuffer() { return m_particleActiveBuffer; }

    // particlePoolBuffer内の個数バッファ
    public ComputeBuffer GetParticleCountBuffer() { return m_particleActiveCountBuffer; }
    #endregion

    // セッター
    #region set
    // インスタンスあたりの頂点数を設定
    public void SetVertexCount(int numVertices)
    {
        m_particleCounts[0] = numVertices;
    }
    #endregion

    // 初期化処理
    #region initialize
    // 初期化
    public void Initialize()
    {
        // パーティクル数をスレッド数の倍数にする
        m_numParticles = Mathf.CeilToInt(_numMaxParticles / (float)THREAD_NUM_X) * THREAD_NUM_X;
       
        // エミット数をスレッド数の倍数にする
        m_numEmitParticles = Mathf.CeilToInt(_numMaxEmitParticles / (float)THREAD_NUM_X) * THREAD_NUM_X;
        //Debug.Log("particleNum " + m_numParticles + " emitNum " + m_numEmitParticles + " THREAD_NUM_X " + THREAD_NUM_X);

        // コンピュートバッファの生成
        m_particlesBuffer = new ComputeBuffer(m_numParticles, Marshal.SizeOf(typeof(GPUParticleData)), ComputeBufferType.Default);
        m_particleActiveBuffer = new ComputeBuffer(m_numParticles, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
        m_particleActiveBuffer.SetCounterValue(0);
        m_particlePoolBuffer = new ComputeBuffer(m_numParticles, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
        m_particlePoolBuffer.SetCounterValue(0);
        m_particleActiveCountBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
        m_particlePoolCountBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
        m_particlePoolCountBuffer.SetData(m_particleCounts);

        // カーネルの設定
        m_initKernel = _computeShader.FindKernel("Init");
        m_emitKernel = _computeShader.FindKernel("Emit");
        m_updateKernel = _computeShader.FindKernel("Update");
        //Debug.Log("initKernel " + m_initKernel + " emitKernel " + m_emitKernel + " updateKernel " + m_updateKernel);


        // バッファの設定
        _computeShader.SetBuffer(m_initKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particles), m_particlesBuffer);
        _computeShader.SetBuffer(m_initKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._deadList), m_particlePoolBuffer);

        // パーティクル数の分だけ初期化カーネルを実行する
        _computeShader.Dispatch(m_initKernel, m_numParticles / THREAD_NUM_X, 1, 1);

        // 初期化処理終了
        m_isInitialized = true;
    }
    #endregion

    // 更新処理
    #region update
    // パーティクルの更新
    public void UpdateParticle()
    {
        // 使用中のパーティクルのインデックスのリストを初期化する
        m_particleActiveBuffer.SetCounterValue(0);

        // コンピュートシェーダーの変数の設定
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._deltaTime), Time.deltaTime);

        // コンピュートバッファの設定
        _computeShader.SetBuffer(m_updateKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particles), m_particlesBuffer);
        _computeShader.SetBuffer(m_updateKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._deadList), m_particlePoolBuffer);
        _computeShader.SetBuffer(m_updateKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._activeList), m_particleActiveBuffer);

        // パーティクル数の分だけ更新カーネルを実行する
        _computeShader.Dispatch(m_updateKernel, m_numParticles / THREAD_NUM_X, 1, 1);

        // 使用中のパーティクルのインデックスのリストを更新する
        m_particleActiveCountBuffer.SetData(m_particleCounts);
        ComputeBuffer.CopyCount(m_particleActiveBuffer, m_particleActiveCountBuffer, 0);
    }
    #endregion

    // 発生処理
    #region emit
    // パーティクルの発生
    public void EmitParticle(Vector3 position)
    {
        // 未使用のパーティクルの個数を取得
        m_particlePoolCountBuffer.SetData(m_particleCounts);
        ComputeBuffer.CopyCount(m_particlePoolBuffer, m_particlePoolCountBuffer, 0);
        m_particlePoolCountBuffer.GetData(m_particleCounts);
        //Debug.Log("EmitParticle Pool Num " + m_particleCounts[0] + " position " + position);
        m_particlePoolNum = m_particleCounts[0];

        // エミット数未満なら発生させない
        if (m_particleCounts[0] < m_numEmitParticles) return;

        // コンピュートシェーダーの変数の設定
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._range), _range);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._direction), _direction);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._velocity), _velocity);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._gravity), _gravity);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._angVelocity), _angVelocity * Mathf.Deg2Rad);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._startScale), _startScale);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._endScale), _endScale);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._lifeTime), _lifeTime);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._sai), _sai);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._val), _val);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._position), position);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._elapsedTime), Time.time);

        // コンピュートバッファの設定
        _computeShader.SetBuffer(m_emitKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particlePool), m_particlePoolBuffer);
        _computeShader.SetBuffer(m_emitKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particles), m_particlesBuffer);

        // エミット数の分だけエミットカーネルを実行する
        _computeShader.Dispatch(m_emitKernel, m_numEmitParticles / THREAD_NUM_X, 1, 1); 
    }
    #endregion

    // 終了処理
    #region finalize
    // ComputeBufferの開放
    public void ReleaseBuffer()
    {
        if (m_particleActiveBuffer != null)
        {
            m_particleActiveBuffer.Release();
        }

        if (m_particlePoolBuffer != null)
        {
            m_particlePoolBuffer.Release();
        }

        if (m_particlesBuffer != null)
        {
            m_particlesBuffer.Release();
        }

        if (m_particlePoolCountBuffer != null)
        {
            m_particlePoolCountBuffer.Release();
        }

        if (m_particleActiveCountBuffer != null)
        {
            m_particleActiveCountBuffer.Release();
        }
    }
    #endregion

    void OnValidate()
    {
        // エミット方向の値を-1～1にする
        _direction.x = Mathf.Clamp(_direction.x, -1, 1);
        _direction.y = Mathf.Clamp(_direction.y, -1, 1);
        _direction.z = Mathf.Clamp(_direction.z, -1, 1);
    }

    void Awake()
    {
        ReleaseBuffer();
        Initialize();
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.Alpha1))
        {
            EmitParticle(Vector3.zero);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            EmitParticle(Vector3.zero);
        }
        UpdateParticle();
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }
}
