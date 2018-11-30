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
    [SerializeField] ComputeShader m_computeShader;
    // 1辺の頂点数
    [SerializeField] [Range(256, 1024)] int m_numVertice = 256;
    // 高さ
    [SerializeField] [Range(1.0f, 10.0f)] float m_height = 5.0f;
    // 滑らかさ
    [SerializeField] [Range(0.1f, 1f)] float m_smoothness = 0.5f;
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
    // 頂点データバッファ
    private ComputeBuffer m_vertexBuffer;
    // メインカーネル
    private int m_mainKernelID;
    // スレッドグループ数
    private int m_numThreadGroups;
    // Mesh
    private Mesh m_mesh;
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
        m_vertexBuffer = new ComputeBuffer(m_numVertices, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
        m_mainKernelID = m_computeShader.FindKernel("CSMain");
        m_numThreadGroups = Mathf.CeilToInt(m_numVertice);
        m_mesh = new Mesh();
    }

    void RegenerateMesh()
    {
        //float time = Time.realtimeSinceStartup;
        // 頂点の計算
        CalculateVertex();
        //Debug.Log(Time.realtimeSinceStartup - time);

        // 三角形の計算
        CalculateTriangle();

        // メッシュの再設定
        ResettingMesh();
    }

    void CalculateVertex()
    {
       
        // バッファを設定
        m_computeShader.SetBuffer(m_mainKernelID, "_vertices", m_vertexBuffer);

        // 変数を設定
        m_computeShader.SetInt("_numVertice", m_numVertice);
        m_computeShader.SetFloat("_distance", m_distance);
        m_computeShader.SetFloat("_height", m_height);
        m_computeShader.SetFloat("_smoothness", m_smoothness * m_numVertice);
        m_computeShader.SetVector("_offset", new Vector2(m_offsetX, m_offsetY));

        // コンピュートシェーダーを実行する
        m_computeShader.Dispatch(m_mainKernelID, m_numThreadGroups, 1, m_numThreadGroups);

        // バッファからデータを受け取る
        Vector3[] vertexData = new Vector3[m_numVertices];
        m_vertexBuffer.GetData(vertexData);

        // 頂点配列にデータを格納する
        m_vertices.Clear();
        m_vertices.AddRange(vertexData);
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
        m_mesh.SetVertices(m_vertices);
        m_mesh.SetTriangles(m_triangles, 0);
        // メッシュの法線の再計算
        m_mesh.RecalculateNormals();
    }

    void RenderTerrain()
    {
        Graphics.DrawMesh(m_mesh, transform.position, transform.rotation, m_material, 0);
    }

    void ReleaseBuffer()
    {
        if (m_vertexBuffer != null)
        {
            m_vertexBuffer.Release();
        }
    }
    #endregion

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