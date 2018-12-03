using System.Collections.Generic;
using UnityEngine;

public class GenerateTerrain : MonoBehaviour
{
    // 定数
    #region define
    // ComputeShaderのスレッド数
    protected const int THREAD_NUM = (8);
    //フィールドのサイズ
    private const float FIELD_SIZE = (20f);
    #endregion

    // エディター上で設定する変数
    #region SerializeField
    // コンピュートシェーダー
    [SerializeField] ComputeShader _computeShader;
    // 1辺の頂点数
    [SerializeField] [Range(8, 256)] int _numVertices = 8;
    // 高さ
    [SerializeField] [Range(1.0f, 10.0f)] float _height = 5.0f;
    // 滑らかさ
    [SerializeField] [Range(0.1f, 1f)] float _smoothness = 0.5f;
    // ノイズの取得位置
    [SerializeField] [Range(0.0f, 1000.0f)] float _offsetX = 0.0f;
    [SerializeField] [Range(0.0f, 1000.0f)] float _offsetY = 0.0f;
    // 生成後に割り当てるマテリアル
    [SerializeField] Material _material;
    #endregion

    // メンバ変数
    #region member variable
    // 頂点と頂点の距離
    private float m_distance;
    //// 頂点数
    //private int m_numVertices;
    //// 頂点座標
    //private List<Vector3> m_vertices;
    //// 頂点データバッファ
    //private ComputeBuffer m_vertexBuffer;
    //// メインカーネル
    //private int m_mainKernelID;
    //// スレッドグループ数
    //private int m_numThreadGroups;
    //// Mesh
    //private List<Mesh> m_meshs;

    // インスタンスのメッシュ
    public Mesh _instanceMesh;
    // インスタンスのマテリアル
    public Material _instanceMaterial;

    // インスタンス数
    private int m_numInstances;
    // 座標
    private ComputeBuffer m_positionBuffer;
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
        //m_numVertices = _numVertices * _numVertices;
        //m_vertices = new List<Vector3>();
        //m_vertexBuffer = new ComputeBuffer(m_numVertices, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
        //m_mainKernelID = _computeShader.FindKernel("CSMain");
        //m_numThreadGroups = _numVertices / THREAD_NUM;
        //m_meshs = new List<Mesh>();

        m_numInstances = (_numVertices - 1) * (_numVertices - 1);
        m_argsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        m_positionBuffer = new ComputeBuffer(m_numInstances, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4)));
        // メッシュの頂点数
        uint numIndices = (_instanceMesh != null) ? (uint)_instanceMesh.GetIndexCount(0) : 0;
        m_args[0] = numIndices;
        // インスタンス数
        m_args[1] = (uint)m_numInstances;
        // バッファにデータを渡す
        m_argsBuffer.SetData(m_args);
    }

    void UpdateBuffers()
    {
        int kernelID = _computeShader.FindKernel("CSMain2");
        _computeShader.SetBuffer(kernelID, "_positions", m_positionBuffer);
        _computeShader.SetInt("_numVertice", _numVertices);
        _computeShader.SetFloat("_distance", m_distance);
        _computeShader.SetFloat("_height", _height);
        _computeShader.SetFloat("_smoothness", _smoothness * _numVertices);
        _computeShader.SetVector("_offset", new Vector2(_offsetX, _offsetY));
        _computeShader.Dispatch(kernelID, _numVertices - 1, 1, _numVertices - 1);
    }

    //void RegenerateMesh()
    //{
    //    // 頂点の計算
    //    CalculateVertex();

    //    // メッシュの再設定
    //    ResettingMesh();
    //}

    //void CalculateVertex()
    //{      
    //    // バッファを設定
    //    _computeShader.SetBuffer(m_mainKernelID, "_vertices", m_vertexBuffer);

    //    // 変数を設定
    //    _computeShader.SetInt("_numVertice", _numVertices);
    //    _computeShader.SetFloat("_distance", m_distance);
    //    _computeShader.SetFloat("_height", _height);
    //    _computeShader.SetFloat("_smoothness", _smoothness * _numVertices);
    //    _computeShader.SetVector("_offset", new Vector2(_offsetX, _offsetY));

    //    // コンピュートシェーダーを実行する
    //    _computeShader.Dispatch(m_mainKernelID, m_numThreadGroups, 1, m_numThreadGroups);

    //    // バッファからデータを受け取る
    //    Vector3[] vertexData = new Vector3[m_numVertices];
    //    m_vertexBuffer.GetData(vertexData);

    //    // 頂点配列にデータを格納する
    //    m_vertices.Clear();
    //    m_vertices.AddRange(vertexData);
    //}

    //void ResettingMesh()
    //{
    //    m_meshs.Clear();
    //    // 三角形の頂点番号
    //    int[] triangles = { 0, 2, 1, 2, 3, 1 };
    //    for (int z = 0; z < _numVertices - 1; z++)
    //    {
    //        for (int x = 0; x < _numVertices - 1; x++)
    //        {
    //            List<Vector3> vertices = new List<Vector3>();
    //            // 基準の頂点の識別番号
    //            int index = z * _numVertices + x;
    //            // 左上
    //            vertices.Add(m_vertices[index]);
    //            // 右上
    //            vertices.Add(m_vertices[index + 1]);
    //            // 左下
    //            vertices.Add(m_vertices[index + _numVertices]);
    //            // 右下
    //            vertices.Add(m_vertices[index + _numVertices + 1]);
    //            // 新しいMeshを生成
    //            Mesh mesh = new Mesh();
    //            // メッシュの頂点の再割り当て
    //            mesh.SetVertices(vertices);
    //            mesh.SetTriangles(triangles, 0);
    //            // メッシュの法線の再計算
    //            mesh.RecalculateNormals();
    //            m_meshs.Add(mesh);
    //        }
    //    }
    //}

    //void RenderTerrain()
    //{
    //    foreach (Mesh mesh in m_meshs)
    //    {
    //        Graphics.DrawMesh(mesh, transform.position, transform.rotation, _material, 0);
    //    }
    //}

    void ReleaseBuffer()
    {
        //if (m_vertexBuffer != null)
        //{
        //    m_vertexBuffer.Release();
        //}
        if (m_positionBuffer != null)
        {
            m_positionBuffer.Release();
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
        UpdateBuffers();
        //RegenerateMesh();
    }

    void Start()
    {
        ReleaseBuffer();
        Initialize();
        UpdateBuffers();
        //RegenerateMesh();
    }

    void OnRenderObject()
    {
        _instanceMaterial.SetBuffer("positionBuffer", m_positionBuffer);
        Graphics.DrawMeshInstancedIndirect(_instanceMesh, 0, _instanceMaterial, new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), m_argsBuffer);
        //RenderTerrain();
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }
}