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
    // サイズ
    public float scale;
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

    // パブリック
    #region public
    // 最大パーティクル数
    public int numMaxParticles = 1024;
    // エミット数
    public int numMaxEmitParticles = 8;
    // コンピュートシェーダー
    public ComputeShader computeShader;
    // エミッターの範囲
    public Vector3 range = Vector3.one;

    public Vector3 velocity = Vector3.zero;
    public float lifeTime = 1;
    public float scaleMin = 1;
    public float scaleMax = 2;
    public float gravity = 9.8f;

    [Range(0, 1)]
    public float sai = 1;   // 彩度
    [Range(0, 1)]
    public float val = 1;   // 明るさ
    public Camera camera;
    #endregion

    // プライベート
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
    //protected int particleActiveNum = 0;
    private int m_particlePoolNum = 0;

    private int m_cspropid_Particles;
    private int m_cspropid_DeadList;
    private int m_cspropid_ActiveList;
    private int m_cspropid_EmitNum;
    private int m_cspropid_ParticlePool;

    // 初期化を行ったか判断する
    private bool m_isInitialized = false;
    #endregion

    // パーティクル数を取得する
    public int GetParticleNum() { return m_numParticles; }

    private int[] debugCounts = { 0, 0, 0, 0 };
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
    //public virtual ComputeBuffer GetActiveCountBuffer() { return particleActiveCountBuffer; }

    // インスタンスあたりの頂点数を設定
    public void SetVertexCount(int numVertices)
    {
        m_particleCounts[0] = numVertices;
    }

    // 初期化
    public void Initialize()
    {
        // パーティクル数をスレッド数の倍数にする
        m_numParticles = (numMaxParticles / THREAD_NUM_X) * THREAD_NUM_X;
        // エミット数をスレッド数の倍数にする
        m_numEmitParticles = (numMaxEmitParticles / THREAD_NUM_X) * THREAD_NUM_X;
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
        m_initKernel = computeShader.FindKernel("Init");
        m_emitKernel = computeShader.FindKernel("Emit");
        m_updateKernel = computeShader.FindKernel("Update");

        // 
        m_cspropid_Particles = ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particles);
        m_cspropid_DeadList = ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._deadList);
        m_cspropid_ActiveList = ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._activeList);
        m_cspropid_ParticlePool = ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particlePool);
        m_cspropid_EmitNum = ShaderDefines.GetIntPropertyID(ShaderDefines.IntID._emitNum);

        //Debug.Log("initKernel " + m_initKernel + " emitKernel " + m_emitKernel + " updateKernel " + m_updateKernel);

        // バッファの設定
        computeShader.SetBuffer(m_initKernel, m_cspropid_Particles, m_particlesBuffer);
        computeShader.SetBuffer(m_initKernel, m_cspropid_DeadList, m_particlePoolBuffer);

        // パーティクル数の分だけ初期化カーネルを実行する
        computeShader.Dispatch(m_initKernel, m_numParticles / THREAD_NUM_X, 1, 1);

        // 初期化処理終了
        m_isInitialized = true;
    }

    // パーティクルの更新
    public void UpdateParticle()
    {
        // 使用中のパーティクルのインデックスのリストを初期化する
        m_particleActiveBuffer.SetCounterValue(0);

        // コンピュートシェーダーの変数の設定
        computeShader.SetFloat("_deltaTime", Time.deltaTime);
        computeShader.SetFloat("_lifeTime", lifeTime);
        computeShader.SetFloat("_gravity", gravity);

        // コンピュートバッファの設定
        computeShader.SetBuffer(m_updateKernel, "_particles", m_particlesBuffer);
        computeShader.SetBuffer(m_updateKernel, "_deadList", m_particlePoolBuffer);
        computeShader.SetBuffer(m_updateKernel, "_activeList", m_particleActiveBuffer);

        // パーティクル数の分だけ更新カーネルを実行する
        computeShader.Dispatch(m_updateKernel, m_numParticles / THREAD_NUM_X, 1, 1);

        // 使用中のパーティクルのインデックスのリストを更新する
        m_particleActiveCountBuffer.SetData(m_particleCounts);
        ComputeBuffer.CopyCount(m_particleActiveBuffer, m_particleActiveCountBuffer, 0);
        //particleActiveCountBuffer.GetData(particleCounts);
        //particleActiveNum = particleCounts[0];
    }


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
        computeShader.SetVector("_range", range);
        computeShader.SetVector("_emitPosition", position);
        computeShader.SetVector("_velocity", velocity);
        computeShader.SetFloat("_lifeTime", lifeTime);
        computeShader.SetFloat("_scaleMin", scaleMin);
        computeShader.SetFloat("_scaleMax", scaleMax);
        computeShader.SetFloat("_sai", sai);
        computeShader.SetFloat("_val", val);
        computeShader.SetFloat("_elapsedTime", Time.time);

        // コンピュートバッファの設定
        computeShader.SetBuffer(m_emitKernel, "_particlePool", m_particlePoolBuffer);
        computeShader.SetBuffer(m_emitKernel, "_particles", m_particlesBuffer);

        // エミット数の分だけエミットカーネルを実行する
        //cs.Dispatch(emitKernel, particleCounts[0] / THREAD_NUM_X, 1, 1);
        computeShader.Dispatch(m_emitKernel, m_numEmitParticles / THREAD_NUM_X, 1, 1); 
    }


    // ComputeBufferの解放
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

    void Awake()
    {
        ReleaseBuffer();
        Initialize();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mpos = Input.mousePosition;
            mpos.z = 10;
            Vector3 pos = camera.ScreenToWorldPoint(mpos);
            EmitParticle(pos);
        }
        UpdateParticle();
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }
}
