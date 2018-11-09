using System.Collections.Generic;
using UnityEngine;

/// 必須コンポーネント
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]

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
    [SerializeField] ComputeShader m_computeShader;
    // 1辺の頂点数
    [SerializeField] [Range(64, 255)] int m_numVertice = 160;
    // 高さ
    [SerializeField] [Range(0.0f, 10.0f)] float m_height = 5.0f;
    // 滑らかさ
    [SerializeField] [Range(0.01f, 1f)] float m_smoothness = 0.5f;
    // ノイズの取得位置
    [SerializeField] [Range(0.0f, 1000.0f)] float m_offsetX = 0.0f;
    [SerializeField] [Range(0.0f, 1000.0f)] float m_offsetY = 0.0f;
    // 生成後に割り当てるマテリアル
    [SerializeField] Material m_material;
    // 生成後に割り当てる物理マテリアル
    [SerializeField] PhysicMaterial m_physicMaterial;
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
    int[] m_triangles;
    // メッシュフィルター
    private MeshFilter m_meshFilter;
    // メッシュレンダラー
    private MeshRenderer m_meshRenderer;
    // メッシュコライダー
    private MeshCollider m_meshCollider;
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
        m_distance = FIELD_SIZE / m_numVertice;
        m_numVertices = m_numVertice * m_numVertice;
        m_numTriangles = ((m_numVertice - 1) * (m_numVertice - 1)) * 6;
        m_vertices = new List<Vector3>();
        m_triangles = new int[m_numTriangles];
        m_meshFilter = gameObject.GetComponent<MeshFilter>();
        m_meshRenderer = gameObject.GetComponent<MeshRenderer>();
        m_meshCollider = gameObject.GetComponent<MeshCollider>();
    }

    void RegenerateMesh()
    {
        float time = Time.realtimeSinceStartup;
        // 頂点の計算
        CalculateVertex();
        Debug.Log(Time.realtimeSinceStartup - time);

        // 三角形の計算
        CalculateTriangle();

        // メッシュの再設定
        ResettingMesh();
    }

    void CalculateVertex()
    {
        // 頂点データを受け取るバッファ
        ComputeBuffer vertexBuffer = new ComputeBuffer(m_numVertices, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
       
        // メインカーネル
        int mainKernelID = m_computeShader.FindKernel("CSMain");
       
        // バッファを設定
        m_computeShader.SetBuffer(mainKernelID, "_vertices", vertexBuffer);

        // 変数を設定
        m_computeShader.SetInt("_numVertice", m_numVertice);
        m_computeShader.SetFloat("_distance", m_distance);
        m_computeShader.SetFloat("_height", m_height);
        m_computeShader.SetFloat("_smoothness", m_smoothness * m_numVertice);
        m_computeShader.SetVector("_offset", new Vector2(m_offsetX, m_offsetY));

        // コンピュートシェーダーの実行時グループ数
        int numThreadGroups = Mathf.CeilToInt(m_numVertice);

        // コンピュートシェーダーを実行する
        m_computeShader.Dispatch(mainKernelID, numThreadGroups, 1, numThreadGroups);

        // バッファからデータを受け取る
        Vector3[] vertexData = new Vector3[m_numVertices];
        vertexBuffer.GetData(vertexData);

        // バッファを開放する
        vertexBuffer.Release();

        // 頂点データの初期化
        m_vertices.Clear();

        // 頂点配列にデータを格納する
        for (int index = 0; index < m_numVertices; index++)
        {
            m_vertices.Add(vertexData[index]);
        }
    }

    void CalculateTriangle()
    {
        // 三角形の計算
        int triangleIndex = 0;
        for (int z = 0; z < m_numVertice - 1; z++)
        {
            for (int x = 0; x < m_numVertice - 1; x++)
            {
                // 基準の頂点の識別番号
                int index = z * m_numVertice + x;
                // 左上
                int a = index;
                // 右上
                int b = index + 1;
                // 左下
                int c = index + m_numVertice;
                // 右下
                int d = index + m_numVertice + 1;
                // 三角形１
                m_triangles[triangleIndex] = a;
                m_triangles[triangleIndex + 1] = c;
                m_triangles[triangleIndex + 2] = b;
                // 三角形２
                m_triangles[triangleIndex + 3] = c;
                m_triangles[triangleIndex + 4] = d;
                m_triangles[triangleIndex + 5] = b;
                // 次のポリゴンの三角形の頂点の識別番号
                triangleIndex += 6;
            }
        }
    }

    void ResettingMesh()
    {
        // メッシュの頂点の再割り当て
        Mesh mesh = new Mesh();
        mesh.SetVertices(m_vertices);
        mesh.triangles = m_triangles;
        // メッシュの法線の再計算
        mesh.RecalculateNormals();
        // メッシュの再設定
        m_meshFilter.mesh = mesh;
        m_meshRenderer.sharedMaterial = m_material;
        m_meshCollider.sharedMaterial = m_physicMaterial;
    }
    #endregion

    void Start()
    {
        Initialize();
        RegenerateMesh();
        // メッシュコライダーを取得
        m_meshCollider.sharedMesh = m_meshFilter.sharedMesh;
        m_meshCollider.sharedMaterial = m_physicMaterial;
    }

    void OnValidate()
    {
        Initialize();
        RegenerateMesh();
    }
}