using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

// パーティクル
struct GPUParticle
{
    // アクティブ状態
    public bool isActive;
    // 座標
    public Vector3 position;
    // 速度
    public Vector3 velocity;
    // 回転
    public Vector3 rotation;
    // 角速度
    public Vector3 angVelocity;
    // 色
    public Color color;
    // スケール
    public float scale;
    // 経過時間
    public float elapsedTime;
    // 生存時間
    public float lifeTime;
}

public class GPUParticleManager : MonoBehaviour
{
    // スレッド数
    const int NUM_THREAD = 1;
    // 最大頂点数
    const int MAX_NUM_VERTICES = 65534;
    // 最大パーティクル数
    [SerializeField, Tooltip("This cannot be changed while running.")]
    int maxNumParticles;
    // パーティクルのメッシュ
    [SerializeField]
    Mesh mesh;
    // パーティクルの描画を行うマテリアルを生成するシェーダー
    [SerializeField]
    Shader shader;
    // パーティクルの計算を行うコンピュートシェーダー
    [SerializeField]
    ComputeShader computeShader;
    // パーティクルの移動速度
    [SerializeField]
    Vector3 velocity = new Vector3(2f, 5f, 2f);
    // パーティクルの角速度
    [SerializeField]
    Vector3 angVelocity = new Vector3(45f, 45f, 45f);
    // エミット範囲
    [SerializeField]
    Vector3 range = Vector3.one;
    // パーティクルのスケール
    [SerializeField]
    float scale = 0.2f;
    // パーティクルの生存時間
    [SerializeField]
    float lifeTime = 2f;
    // 一度にエミットするパーティクル数
    [SerializeField, Range(1, 10000)]
    int numEmitParticles = 10;
    // 複合メッシュ
    Mesh m_combinedMesh;
    // パーティクルバッファ
    ComputeBuffer m_particlesBuffer;
    // パーティクルプールのバッファ
    ComputeBuffer m_particlePoolBuffer;
    // パーティクル数を取得するバッファ
    ComputeBuffer m_particleCountBuffer;
    // パーティクル数
    int[] m_particleCounts;
    // 更新カーネル
    int m_updateKernel;
    // エミットカーネル
    int m_emitKernel;
    // マテリアル
    Material m_material;
    // マテリアル情報を適用するブロック要素
    List<MaterialPropertyBlock> m_propertyBlocks = new List<MaterialPropertyBlock>();
    // 1メッシュ当たりのパーティクル数
    int m_numParticlesPerMesh;
    // メッシュ数
    int m_numMeshs;

    // 複合メッシュを生成する
    Mesh CreateCombinedMesh(Mesh mesh, int numParticles)
    {
        // 最大頂点数を超えないようにする
        Assert.IsTrue(mesh.vertexCount * numParticles <= MAX_NUM_VERTICES);
        // メッシュのindexバッファを取得する
        int[] meshIndices = mesh.GetIndices(0);
        // インデックス数
        int numIndices = meshIndices.Length;
        // 頂点
        List<Vector3> vertices = new List<Vector3>();
        // インデックス
        int[] indices = new int[numParticles * numIndices];
        // 法線
        List<Vector3> normals = new List<Vector3>();
        // 接線
        List<Vector4> tangents = new List<Vector4>();
        // 
        List<Vector2> uv0 = new List<Vector2>();
        List<Vector2> uv1 = new List<Vector2>();
        // 
        for (int id = 0; id < numParticles; ++id)
        {
            // 
            vertices.AddRange(mesh.vertices);
            normals.AddRange(mesh.normals);
            tangents.AddRange(mesh.tangents);
            uv0.AddRange(mesh.uv);
            // 各メッシュのインデックスは（1 つのモデルの頂点数 * ID）分ずらす
            for (int n = 0; n < numIndices; ++n)
            {
                indices[id * numIndices + n] = id * mesh.vertexCount + meshIndices[n];
            }
            // 2 番目の UV に ID を格納しておく
            for (int n = 0; n < mesh.uv.Length; ++n)
            {
                uv1.Add(new Vector2(id, id));
            }
        }
        // 複合メッシュ
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
        return combinedMesh;
    }

    // ビュー、射影行列の配列を取得する
    float[] GetViewProjectionArray()
    {
        // メインカメラ
        Camera camera = Camera.main;
        // ビュー行列
        Matrix4x4 view = camera.worldToCameraMatrix;
        // 射影行列
        Matrix4x4 proj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        // ビュー、射影行列
        Matrix4x4 vp = proj * view;
        // ビュー、射影行列を配列にして返す
        return new float[] 
        {
            vp.m00, vp.m10, vp.m20, vp.m30,
            vp.m01, vp.m11, vp.m21, vp.m31,
            vp.m02, vp.m12, vp.m22, vp.m32,
            vp.m03, vp.m13, vp.m23, vp.m33
        };
    }

    // パーティクルプールのサイズを取得する
    int GetParticlePoolSize()
    {
        m_particleCountBuffer.SetData(m_particleCounts);
        ComputeBuffer.CopyCount(m_particlePoolBuffer, m_particleCountBuffer, 0);
        m_particleCountBuffer.GetData(m_particleCounts);
        return m_particleCounts[0];
    }

    void OnEnable()
    {
        // メッシュの結合
        {
            m_numParticlesPerMesh = MAX_NUM_VERTICES / mesh.vertexCount;
            m_numMeshs = (int)Mathf.Ceil((float)maxNumParticles / m_numParticlesPerMesh);
            m_combinedMesh = CreateCombinedMesh(mesh, m_numParticlesPerMesh);
        }
        // メッシュの数だけマテリアルを作成
        m_material = new Material(shader);
        for (int i = 0; i < m_numMeshs; ++i)
        {
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            props.SetFloat("_IdOffset", m_numParticlesPerMesh * i);
            m_propertyBlocks.Add(props);
        }
        // ComputeBufferの初期化
        {
            m_particlesBuffer = new ComputeBuffer(maxNumParticles, Marshal.SizeOf(typeof(GPUParticle)), ComputeBufferType.Default);
            m_particlePoolBuffer = new ComputeBuffer(maxNumParticles, sizeof(int), ComputeBufferType.Append);
            m_particlePoolBuffer.SetCounterValue(0);
            m_particleCountBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            m_particleCounts = new int[] { 0, 1, 0, 0 };
        }
        // カーネルの設定
        m_updateKernel = computeShader.FindKernel("Update");
        m_emitKernel = computeShader.FindKernel("Emit");
        // 初期化カーネルを実行
        DispatchInit();
    }

    void OnDisable()
    {
        // バッファを開放する
        m_particlesBuffer.Release();
        m_particlePoolBuffer.Release();
        m_particleCountBuffer.Release();
    }

    void Update()
    {
        //if (Input.GetMouseButton(0))
        //{
        //    RaycastHit hit;
        //    var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        //    if (Physics.Raycast(ray, out hit))
        //    {
        //        var toNormal = Quaternion.FromToRotation(Vector3.up, hit.normal);
        //        computeShader.SetVector("_Position", hit.point + hit.normal * 0.1f);
        //        computeShader.SetVector("_Velocity", toNormal * velocity);
        //        DispatchEmit(emitGroupNum);
        //    }
        //}
        // エミットカーネルを実行
        if (Input.GetKey(KeyCode.Space))
        {
            computeShader.SetVector("_position", transform.position);
            computeShader.SetVector("_velocity", velocity);
            DispatchEmit(numEmitParticles);
        }
        // 更新カーネルを実行
        DispatchUpdate();
        // 描画処理
        RegisterDraw(Camera.main);
#if UNITY_EDITOR
        if (SceneView.lastActiveSceneView)
        {
            RegisterDraw(SceneView.lastActiveSceneView.camera);
        }
#endif
    }

    // 初期化カーネルを実行
    void DispatchInit()
    {
        int initKernel = computeShader.FindKernel("Init");
        computeShader.SetBuffer(initKernel, "_particles", m_particlesBuffer);
        computeShader.SetBuffer(initKernel, "_deadList", m_particlePoolBuffer);
        computeShader.Dispatch(initKernel, maxNumParticles, 1, 1);
    }

    // エミットカーネルを実行
    void DispatchEmit(int groupNum)
    {
        if (GetParticlePoolSize() / NUM_THREAD <= 0)
        {
            return;
        }
        Camera camera = Camera.main;
        computeShader.SetBuffer(m_emitKernel, "_particles", m_particlesBuffer);
        computeShader.SetBuffer(m_emitKernel, "_particlePool", m_particlePoolBuffer);
        computeShader.SetVector("_angVelocity", angVelocity * Mathf.Deg2Rad);
        computeShader.SetVector("_range", range);
        computeShader.SetFloat("_scale", scale);
        computeShader.SetFloat("_deltaTime", Time.deltaTime);
        computeShader.SetFloat("_screenWidth", camera.pixelWidth);
        computeShader.SetFloat("_screenHeight", camera.pixelHeight);
        computeShader.SetFloat("_lifeTime", lifeTime);
        computeShader.Dispatch(m_emitKernel, Mathf.Min(groupNum, GetParticlePoolSize() / NUM_THREAD), 1, 1);
    }

    // 更新カーネルを実行
    void DispatchUpdate()
    {
        computeShader.SetFloat("_deltaTime", Time.deltaTime);
        computeShader.SetFloats("_viewProj", GetViewProjectionArray());
        computeShader.SetTexture(m_updateKernel, "_cameraDepthTexture", GBufferUtils.GetDepthTexture());
        computeShader.SetTexture(m_updateKernel, "_cameraGBufferTexture2", GBufferUtils.GetGBufferTexture(2));
        computeShader.SetBuffer(m_updateKernel, "_particles", m_particlesBuffer);
        computeShader.SetBuffer(m_updateKernel, "_deadList", m_particlePoolBuffer);
        computeShader.Dispatch(m_updateKernel, maxNumParticles, 1, 1);

    }

    // 描画処理
    void RegisterDraw(Camera camera)
    {
        m_material.SetBuffer("_particles", m_particlesBuffer);
        for (int i = 0; i < m_numMeshs; ++i)
        {
            var props = m_propertyBlocks[i];
            props.SetFloat("_IdOffset", m_numParticlesPerMesh * i);
            Graphics.DrawMesh(m_combinedMesh, transform.position, transform.rotation, m_material, 0, camera, 0, props);
        }
    }
}