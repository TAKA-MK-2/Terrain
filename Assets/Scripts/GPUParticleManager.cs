using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

// GPUパーティクル
struct GPUParticle
{
    // 識別番号
    public int index;
    // 活動状態
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
    // カラー
    public Vector3 color;
    // 経過時間
    public float elapsedTime;
    // 生存時間
    public float lifeTime;
};

public class GPUParticleManager : MonoBehaviour
{
    // 最大頂点数
    const int MAX_NUM_VERTICES = 65534;
    // 最大パーティクル数
    [SerializeField, Tooltip("This cannot be changed while running.")]
    private int MAX_NUM_PARTICLES;
    // パーティクルのメッシュ
    [SerializeField]
    private Mesh mesh;
    // シェーダー
    [SerializeField]
    private Shader shader;
    // コンピュートシェーダー
    [SerializeField]
    private ComputeShader computeShader;
    // パーティクルのテクスチャ
    [SerializeField]
    private Texture2D texture;
    // 速度
    [SerializeField]
    private Vector3 velocity = new Vector3(2f, 5f, 2f);
    // 角速度
    [SerializeField]
    private Vector3 angVelocity = new Vector3(45f, 45f, 45f);
    // 範囲
    [SerializeField]
    private Vector3 range = Vector3.one;
    // 複合メッシュ
    Mesh m_combinedMesh;
    // コンピュートバッファ
    ComputeBuffer m_computeBuffer;
    // 更新処理カーネル
    int m_updateKernel;
    // 放出処理カーネル
    int m_emitKernel;
    // マテリアル
    List<Material> m_materials = new List<Material>();
    // 1メッシュ当たりのパーティクル数
    int numParticlesPerMesh;
    // メッシュの数
    int m_numMeshs;

    // 複合メッシュの作成
    Mesh CreateCombinedMesh(Mesh mesh, int num)
    {
        // Meshの合計の頂点数が最大頂点数を超えないようにする
        Assert.IsTrue(mesh.vertexCount * num <= MAX_NUM_VERTICES);
        // メッシュ番号
        int[] meshIndices = mesh.GetIndices(0);
        // 識別番号の数
        int numMeshs = meshIndices.Length;
        // 頂点
        List<Vector3> vertices = new List<Vector3>();
        // 識別番号
        int[] indices = new int[num * numMeshs];
        // 法線
        List<Vector3> normals = new List<Vector3>();
        // 接線
        List<Vector4> tangents = new List<Vector4>();
        // 
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();

        for (int index = 0; index < num; index++)
        {
            // 領域を確保する
            vertices.AddRange(mesh.vertices);
            normals.AddRange(mesh.normals);
            tangents.AddRange(mesh.tangents);
            uv0.AddRange(mesh.uv);
            // 各メッシュのインデックスは（1 つのモデルの頂点数 * ID）分ずらす
            for (int n = 0; n < numMeshs; n++)
            {
                indices[index * numMeshs + n] = index * mesh.vertexCount + meshIndices[n];
            }
            // 2 番目の UV に ID を格納しておく
            for (int n = 0; n < mesh.uv.Length; ++n)
            {
                uv1.Add(new Vector2(index, index));
            }
        }
        // 複合メッシュを生成
        Mesh combinedMesh = new Mesh();
        combinedMesh.SetVertices(vertices);
        combinedMesh.SetIndices(indices, MeshTopology.Triangles, 0);
        combinedMesh.SetNormals(normals);
        combinedMesh.RecalculateNormals();
        combinedMesh.SetTangents(tangents);
        combinedMesh.SetUVs(0, uv0);
        combinedMesh.SetUVs(1, uv1);
        combinedMesh.RecalculateBounds();
        combinedMesh.bounds.SetMinMax(Vector3.one * -100f, Vector3.one * 100f);
        // 生成した複合メッシュを返す
        return combinedMesh;
    }

    void OnEnable()
    {
        // 1メッシュ当たりのパーティクル数を計算
        numParticlesPerMesh = MAX_NUM_VERTICES / mesh.vertexCount;
        // メッシュ数を計算
        m_numMeshs = (int)Mathf.Ceil((float)MAX_NUM_PARTICLES / numParticlesPerMesh);
        // メッシュ数分のマテリアルを生成
        for (int i = 0; i < m_numMeshs; ++i)
        {
            // シェーダーからマテリアルを生成
            Material material = new Material(shader);
            // マテリアルにメッシュのIDを設定
            material.SetInt("_idOffset", numParticlesPerMesh * i);
            // マテリアルを追加
            m_materials.Add(material);
        }
        // 複合メッシュを生成
        m_combinedMesh = CreateCombinedMesh(mesh, numParticlesPerMesh);
        // コンピュートバッファを生成
        m_computeBuffer = new ComputeBuffer(MAX_NUM_PARTICLES, Marshal.SizeOf(typeof(GPUParticle)), ComputeBufferType.Default);
        // カーネルの設定
        var initKernel = computeShader.FindKernel("Initialize");
        m_updateKernel = computeShader.FindKernel("Update");
        m_emitKernel = computeShader.FindKernel("Emit");
        // バッファを設定
        computeShader.SetBuffer(initKernel, "_particles", m_computeBuffer);
        // 変数を設定
        computeShader.SetVector("_range", range);
        computeShader.SetVector("_velocity", velocity);
        computeShader.SetVector("_angVelocity", angVelocity * Mathf.Deg2Rad);
        // コンピュートシェーダーの実行
        computeShader.Dispatch(initKernel, (int)Mathf.Ceil((float)MAX_NUM_PARTICLES / 1024), 1, 1);
    }

    void Update()
    {
        // 変数を設定
        computeShader.SetVector("_velocity", velocity);
        computeShader.SetVector("_angVelocity", angVelocity * Mathf.Deg2Rad);
        computeShader.SetVector("_range", range);
        // バッファを設定
        computeShader.SetBuffer(m_emitKernel, "_particles", m_computeBuffer);
        // コンピュートシェーダーを実行
        computeShader.Dispatch(m_emitKernel, (int)Mathf.Ceil((float)MAX_NUM_PARTICLES / 1024), 1, 1);
        // 変数を設定
        computeShader.SetFloat("_deltaTime", Time.deltaTime);
        // バッファを設定
        computeShader.SetBuffer(m_updateKernel, "_particles", m_computeBuffer);
        // コンピュートシェーダーを実行
        computeShader.Dispatch(m_updateKernel, (int)Mathf.Ceil((float)MAX_NUM_PARTICLES / 1024), 1, 1);
        // メッシュの描画
        for (int i = 0; i < m_numMeshs; ++i)
        {
            // 描画するマテリアルを取り出す
            Material material = m_materials[i];
            // 変数を設定
            material.SetInt("_idOffset", numParticlesPerMesh * i);
            material.SetBuffer("_particles", m_computeBuffer);
            // 描画する
            Graphics.DrawMesh(m_combinedMesh, transform.position, transform.rotation, material, 0, Camera.main);
        }
    }

    void OnDisable()
    {
        // コンピュートバッファの開放    
        m_computeBuffer.Release();
    }
}