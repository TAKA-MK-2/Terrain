using System.Collections.Generic;
using UnityEngine;

public class GenerateTerrain : MonoBehaviour
{
    // 定数
    #region define
    // ComputeShaderのスレッド数
    protected const int THREAD_NUM = (32);
    //フィールドのサイズ
    private const float FIELD_SIZE = (100.0f);
    #endregion

    // エディター上で設定する変数
    #region SerializeField
    // コンピュートシェーダー
    [SerializeField] ComputeShader _computeShader;
    // 1辺の頂点数
    [SerializeField] [Range(256, 1024)] int _numVertices = 256;
    // 高さ
    [SerializeField] [Range(0.0f, 100.0f)] float _height = 20.0f;
    // 滑らかさ
    [SerializeField] [Range(0.001f, 1f)] float _smoothness = 0.5f;
    // ノイズの取得位置
    [SerializeField] [Range(0.0f, 1000.0f)] float _offsetX = 0.0f;
    [SerializeField] [Range(0.0f, 1000.0f)] float _offsetY = 0.0f;
    #endregion

    // メンバ変数
    #region member variable
    // 頂点と頂点の距離
    private float m_distance;
    // 頂点数
    private int m_numVertices;
    // 頂点データバッファ
    private ComputeBuffer m_vertexBuffer;
    // メインカーネル
    private int m_mainKernelID;
    // スレッドグループ数
    private int m_numThreadGroups;

    // インスタンスのメッシュ
    public Mesh _instanceMesh;
    // インスタンスのマテリアル
    public Material _instanceMaterial;

    // インスタンス数
    private int m_numInstances;
    // 引数
    private ComputeBuffer m_argsBuffer;
    // 引数
    private uint[] m_args = new uint[5] { 0, 0, 0, 0, 0 };

    #endregion

    /// デバッグ用
    #region debug
    private int m_count = 0;
    private float m_time = 0;
    #endregion

    #region method
    void Initialize()
    {
        // 初期化処理
        _numVertices = Mathf.CeilToInt(_numVertices / (float)THREAD_NUM) * THREAD_NUM;
        m_distance = FIELD_SIZE / _numVertices;
        m_numVertices = _numVertices * _numVertices;
        m_vertexBuffer = new ComputeBuffer(m_numVertices, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
        m_mainKernelID = _computeShader.FindKernel("CSMain");
        m_numThreadGroups = _numVertices / THREAD_NUM;

        m_numInstances = (_numVertices - 1) * (_numVertices - 1);
        m_argsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        // メッシュの頂点数
        uint numIndices = (_instanceMesh != null) ? (uint)_instanceMesh.GetIndexCount(0) : 0;
        m_args[0] = numIndices;
        // インスタンス数
        m_args[1] = (uint)m_numInstances;
        // バッファにデータを渡す
        m_argsBuffer.SetData(m_args);
    }

    void CalculateVertex()
    {
        // バッファを設定
        _computeShader.SetBuffer(m_mainKernelID, "_vertices", m_vertexBuffer);

        // 変数を設定
        _computeShader.SetInt("_numVertice", _numVertices);
        _computeShader.SetFloat("_distance", m_distance);
        _computeShader.SetFloat("_height", _height);
        _computeShader.SetFloat("_smoothness", _smoothness * _numVertices);
        _computeShader.SetVector("_offset", new Vector2(_offsetX, _offsetY));

        // コンピュートシェーダーを実行する
        _computeShader.Dispatch(m_mainKernelID, m_numThreadGroups, 1, m_numThreadGroups);
    }

    void RenderTerrain()
    {
        _instanceMaterial.SetInt("_numInstances", m_numInstances);
        _instanceMaterial.SetInt("_numVertices", _numVertices);
        _instanceMaterial.SetFloat("_distance", m_distance);
        _instanceMaterial.SetBuffer("_vertices", m_vertexBuffer);
        Graphics.DrawMeshInstancedIndirect(_instanceMesh, 0, _instanceMaterial, new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), m_argsBuffer);
    }

    void ReleaseBuffer()
    {
        if (m_vertexBuffer != null)
        {
            m_vertexBuffer.Release();
        }
        //if (m_positionBuffer != null)
        //{
        //    m_positionBuffer.Release();
        //}
        if (m_argsBuffer != null)
        {
            m_argsBuffer.Release();
        }
    }
    #endregion

    void OnValidate()
    {
        ReleaseBuffer();
        Initialize();
        CalculateVertex();
    }

    void Start()
    {
        ReleaseBuffer();
        Initialize();
        CalculateVertex();
    }

    void OnRenderObject()
    {
        RenderTerrain();
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }
}