using System.Collections.Generic;
using UnityEngine;

public class GenerateTerrain : MonoBehaviour
{
    // 定数
    #region define
    //フィールドのサイズ
    private const float FIELD_SIZE = (20f);
    #endregion

    // エディター上で設定する変数
    #region SerializeField
    // コンピュートシェーダー
    [SerializeField] ComputeShader _computeShader;
    // 1辺の頂点数
    [SerializeField] [Range(8, 64)] int _numVertice = 8;
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
    // 頂点数
    private int m_numVertices;
    // 頂点と頂点の距離
    private float m_distance;
    // 三角形の数
    private int m_numTriangles;
    // 頂点座標
    private List<Vector3> m_vertices;
    // Meshを構成する三角形の頂点の識別番号
    //int[] m_triangles;
    // 頂点データバッファ
    private ComputeBuffer m_vertexBuffer;
    // メインカーネル
    private int m_mainKernelID;
    // スレッドグループ数
    private int m_numThreadGroups;
    // Mesh
    private List<Mesh> m_meshs;
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
        m_distance = FIELD_SIZE / _numVertice;
        m_numVertices = _numVertice * _numVertice;
        m_numTriangles = ((_numVertice - 1) * (_numVertice - 1)) * 6;
        m_vertices = new List<Vector3>();
        //m_triangles = new int[m_numTriangles];
        m_vertexBuffer = new ComputeBuffer(m_numVertices, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
        m_mainKernelID = _computeShader.FindKernel("CSMain");
        m_numThreadGroups = Mathf.CeilToInt(_numVertice);
        m_meshs = new List<Mesh>();
    }

    void RegenerateMesh()
    {
        //float time = Time.realtimeSinceStartup;
        // 頂点の計算
        CalculateVertex();
        //Debug.Log(Time.realtimeSinceStartup - time);

        //// 三角形の計算
        //CalculateTriangle();

        // メッシュの再設定
        ResettingMesh();
    }

    void CalculateVertex()
    {
       
        // バッファを設定
        _computeShader.SetBuffer(m_mainKernelID, "_vertices", m_vertexBuffer);

        // 変数を設定
        _computeShader.SetInt("_numVertice", _numVertice);
        _computeShader.SetFloat("_distance", m_distance);
        _computeShader.SetFloat("_height", _height);
        _computeShader.SetFloat("_smoothness", _smoothness * _numVertice);
        _computeShader.SetVector("_offset", new Vector2(_offsetX, _offsetY));

        // コンピュートシェーダーを実行する
        _computeShader.Dispatch(m_mainKernelID, m_numThreadGroups, 1, m_numThreadGroups);

        // バッファからデータを受け取る
        Vector3[] vertexData = new Vector3[m_numVertices];
        m_vertexBuffer.GetData(vertexData);

        // 頂点配列にデータを格納する
        m_vertices.Clear();
        m_vertices.AddRange(vertexData);
    }

    //void CalculateTriangle()
    //{
    //    // 三角形の計算
    //    int triangleIndex = 0;
    //    for (int z = 0; z < _numVertice - 1; z++)
    //    {
    //        for (int x = 0; x < _numVertice - 1; x++)
    //        {
    //            // 基準の頂点の識別番号
    //            int index = z * _numVertice + x;
    //            // 左上
    //            int a = index;
    //            // 右上
    //            int b = index + 1;
    //            // 左下
    //            int c = index + _numVertice;
    //            // 右下
    //            int d = index + _numVertice + 1;
    //            // 三角形１
    //            m_triangles[triangleIndex] = a;
    //            m_triangles[triangleIndex + 1] = c;
    //            m_triangles[triangleIndex + 2] = b;
    //            // 三角形２
    //            m_triangles[triangleIndex + 3] = c;
    //            m_triangles[triangleIndex + 4] = d;
    //            m_triangles[triangleIndex + 5] = b;
    //            // 次のポリゴンの三角形の頂点の識別番号
    //            triangleIndex += 6;
    //        }
    //    }
    //}

    void ResettingMesh()
    {
        m_meshs.Clear();
        // 三角形の頂点番号
        int[] triangls = { 0, 2, 1, 2, 3, 1 };
        for (int z = 0; z < _numVertice - 1; z++)
        {
            for (int x = 0; x < _numVertice - 1; x++)
            {
                List<Vector3> vertices = new List<Vector3>();
                // 基準の頂点の識別番号
                int index = z * _numVertice + x;
                // 左上
                vertices.Add(m_vertices[index]);
                // 右上
                vertices.Add(m_vertices[index + 1]);
                // 左下
                vertices.Add(m_vertices[index + _numVertice]);
                // 右下
                vertices.Add(m_vertices[index + _numVertice + 1]);
                // 新しいMeshを生成
                Mesh mesh = new Mesh();
                // メッシュの頂点の再割り当て
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangls, 0);
                // メッシュの法線の再計算
                mesh.RecalculateNormals();
                m_meshs.Add(mesh);
            }
        }
    }

    void RenderTerrain()
    {
        foreach (Mesh mesh in m_meshs)
        {
            Graphics.DrawMesh(mesh, transform.position, transform.rotation, _material, 0);
        }
    }

    void ReleaseBuffer()
    {
        if (m_vertexBuffer != null)
        {
            m_vertexBuffer.Release();
        }
    }
    #endregion

    void OnValidate()
    {
        ReleaseBuffer();
        Initialize();
        RegenerateMesh();
    }

    void Start()
    {
        ReleaseBuffer();
        Initialize();
        RegenerateMesh();
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