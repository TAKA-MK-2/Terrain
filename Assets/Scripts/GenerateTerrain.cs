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
    // インスタンスのメッシュ
    [SerializeField] Mesh _mesh;
    // インスタンスのマテリアル
    [SerializeField] Material _shaderMaterial;
    // 1辺の頂点数
    [SerializeField] [Range(256, 1024)] int _numVertices = 256;
    // 高さ
    [SerializeField] [Range(0.0f, 100.0f)] float _height = 50.0f;
    // ノイズの滑らかさ
    [SerializeField] [Range(0.001f, 1f)] float _smoothness = 0.5f;
    // ノイズの取得位置
    [SerializeField] [Range(0.0f, 1000.0f)] float _offsetX = 0.0f;
    [SerializeField] [Range(0.0f, 1000.0f)] float _offsetY = 0.0f;
    #endregion

    // private変数
    #region private variable
    // 頂点数
    private int m_numVertices;
    // 頂点間の距離
    private float m_distance;
    // カーネルID
    private int m_kernelID;
    // スレッドグループ数
    private int m_numThreadGroups;
    // 頂点データバッファ
    private ComputeBuffer m_vertexBuffer;
    // GPUインスタンシングの引数バッファ
    private ComputeBuffer m_argsBuffer;
    // GPUインスタンシングの引数
    private uint[] m_args = new uint[5] { 0, 0, 0, 0, 0 };
    // 当たり判定用のMesh
    private Mesh m_collisionMesh;
    #endregion

    // private 関数
    #region private method
    // 初期化処理
    void Initialize()
    {
        // メンバ変数の初期化処理
        _numVertices = Mathf.CeilToInt(_numVertices / (float)THREAD_NUM) * THREAD_NUM;
        m_numVertices = _numVertices * _numVertices;
        m_distance = FIELD_SIZE / _numVertices;
        m_kernelID = _computeShader.FindKernel("CalculateVertex");
        m_numThreadGroups = _numVertices / THREAD_NUM;
        m_vertexBuffer = new ComputeBuffer(m_numVertices, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
        m_argsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        // メッシュの頂点数
        m_args[0] = (_mesh != null) ? _mesh.GetIndexCount(0) : 0;
        // インスタンス数
        m_args[1] = (uint)((_numVertices - 1) * (_numVertices - 1));

        // バッファにデータを渡す
        m_argsBuffer.SetData(m_args);
    }

    // 頂点の計算
    void CalculateVertex()
    {
        // バッファを設定
        _computeShader.SetBuffer(m_kernelID, "_vertexBuffer", m_vertexBuffer);

        // 変数を設定
        _computeShader.SetInt("_numVertice", _numVertices);
        _computeShader.SetFloat("_distance", m_distance);
        _computeShader.SetFloat("_height", _height);
        _computeShader.SetFloat("_smoothness", _smoothness * _numVertices);
        _computeShader.SetVector("_offset", new Vector2(_offsetX, _offsetY));

        // コンピュートシェーダーを実行する
        _computeShader.Dispatch(m_kernelID, m_numThreadGroups, 1, m_numThreadGroups);
    }

    //地形描画
    void RenderTerrain()
    {
        // 変数を設定
        _shaderMaterial.SetInt("_numVertices", _numVertices);
        _shaderMaterial.SetFloat("_distance", m_distance);
        _shaderMaterial.SetBuffer("_vertexBuffer", m_vertexBuffer);
        // 描画処理
        Graphics.DrawMeshInstancedIndirect(_mesh, 0, _shaderMaterial, new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), m_argsBuffer);
    }

    // バッファの開放
    void ReleaseBuffer()
    {
        if (m_vertexBuffer != null)
        {
            m_vertexBuffer.Release();
        }
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