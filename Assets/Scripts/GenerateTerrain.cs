using System.Collections.Generic;
using UnityEngine;

/// 必須コンポーネント
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]

public class GenerateTerrain : MonoBehaviour
{
    // 定数
    private const float FIELD_SIZE = (20f);

    /// エディター上で初期化する変数
    // コンピュートシェーダー
    public ComputeShader m_computeShader;
    // 1辺の頂点数
    [Range(64, 1024)]
    [SerializeField]
    private int m_size = 128;
    // 高さ
    [SerializeField]
    [Range(0.0f, 100.0f)]
    private float m_height = 10.0f;
    // 滑らかさ
    [SerializeField]
    [Range(0.01f, 1f)]
    private float m_smoothness = 1.0f;
    // ノイズの取得位置
    [SerializeField]
    [Range(0.0f, 1000.0f)]
    private float m_offsetX;
    [SerializeField]
    [Range(0.0f, 1000.0f)]
    private float m_offsetY;
    // 生成後に割り当てるマテリアル
    public Material m_material;
    // 生成後に割り当てる物理マテリアル
    public PhysicMaterial m_physicMaterial;

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

    /// デバッグ用
    private int m_count = 0;
    private float m_time = 0;

    void Start()
    {
        Initialize();
        RegenerateMesh();
        // メッシュコライダーを取得
        m_meshCollider.sharedMesh = m_meshFilter.sharedMesh;
        m_meshCollider.sharedMaterial = m_physicMaterial;
    }

    void Update()
    {
        //m_offset.x += Time.deltaTime * 10;
        //RegenerateMesh();
        //m_meshCollider.sharedMesh = m_meshFilter.sharedMesh;
    }

    void OnValidate()
    {
        Initialize();
        RegenerateMesh();
    }

    void Initialize()
    {
        // 初期化処理
        m_distance = FIELD_SIZE / m_size;
        m_numVertices = m_size * m_size;
        m_numTriangles = ((m_size - 1) * (m_size - 1)) * 6;
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
        // GPUで頂点座標計算
        // 頂点データを受け取るバッファ
        ComputeBuffer vertexBuffer = new ComputeBuffer(m_numVertices, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
        // メインカーネル
        int mainKernelID = m_computeShader.FindKernel("CSMain");
        // バッファを設定
        m_computeShader.SetBuffer(mainKernelID, "_vertices", vertexBuffer);
        // 変数を設定
        m_computeShader.SetInt("_size", m_size);
        m_computeShader.SetFloat("_distance", m_distance);
        m_computeShader.SetFloat("_height", m_height);
        m_computeShader.SetFloat("_smoothness", m_smoothness);
        m_computeShader.SetVector("_offset", new Vector2(m_offsetX, m_offsetY));
        // コンピュートシェーダーの実行時グループ数
        int numThreadGroups = Mathf.CeilToInt(m_size / 1.0f);
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

        //// CPUで頂点座標計算
        //m_vertices.Clear();
        //float sampleX;
        //float sampleZ;
        //for (int index_z = 0; index_z < m_size; index_z++)
        //{
        //    for (int index_x = 0; index_x < m_size; index_x++)
        //    {
        //        float x = (index_x * m_distance);
        //        float z = (index_z * m_distance);
        //        sampleX = (index_x + m_offset.x) * m_smoothness;
        //        sampleZ = (index_z + m_offset.y) * m_smoothness;
        //        float y = Mathf.PerlinNoise(sampleX, sampleZ) * m_height;
        //        m_vertices.Add(new Vector3(x, y, z));
        //    }
        //}
    }

    void CalculateTriangle()
    {
        // 三角形の計算
        int triangleIndex = 0;
        for (int z = 0; z < m_size - 1; z++)
        {
            for (int x = 0; x < m_size - 1; x++)
            {
                // 基準の頂点の識別番号
                int index = z * m_size + x;
                // 左上
                int a = index;
                // 右上
                int b = index + 1;
                // 左下
                int c = index + m_size;
                // 右下
                int d = index + m_size + 1;
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
}