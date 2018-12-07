using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class GPUParticleRenderer : MonoBehaviour
{
    #region define
    // カリング
    public class CullingData
    {
        #region define
        // ComputeShaderのスレッド数
        const int NUM_THREAD_X = 32;
        #endregion

        #region public variable
        // 視界内のパーティクルの要素番号
        public ComputeBuffer m_inViewsAppendBuffer;
        // 視界内のパーティクルの個数
        public ComputeBuffer m_inViewsCountBuffer;
        // [0]インスタンスあたりの頂点数 [1]インスタンス数 [2]開始する頂点位置 [3]開始するインスタンス
        public int[] m_inViewsCounts = { 0, 0, 0, 0 };
        #endregion

        #region private variable
        // 視錘台
        private Plane[] m_planes = new Plane[4];
        // 視錘台の法線を分割した配列
        private float[] m_normalsFloat = new float[12];
        #endregion

        #region public method
        // コンストラクタ
        public CullingData(int _numParticles)
        {
            // コンピュートバッファの生成
            m_inViewsAppendBuffer = new ComputeBuffer(_numParticles, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            m_inViewsCountBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
        }

        // インスタンスあたりの頂点数を設定
        public void SetVertexCount(int vertexCount)
        {
            m_inViewsCounts[0] = vertexCount;
        }

        // バッファの開放処理
        public void ReleaseBuffers()
        {
            if (m_inViewsAppendBuffer != null)
            {
                m_inViewsAppendBuffer.Release();
                m_inViewsAppendBuffer = null;
            }
            if (m_inViewsCountBuffer != null)
            {
                m_inViewsCountBuffer.Release();
                m_inViewsCountBuffer = null;
            }
        }
        #endregion

        #region private method
        // 視錐台の計算
        private void CalculateFrustumPlanes(Matrix4x4 _mat, ref Plane[] _planes)
        {
            // 一時保存
            Vector3 temp;

            // 左面
            temp.x = _mat.m30 + _mat.m00;
            temp.y = _mat.m31 + _mat.m01;
            temp.z = _mat.m32 + _mat.m02;
            _planes[0].normal = temp;

            // 右面
            temp.x = _mat.m30 - _mat.m00;
            temp.y = _mat.m31 - _mat.m01;
            temp.z = _mat.m32 - _mat.m02;
            _planes[1].normal = temp;

            // 下面
            temp.x = _mat.m30 + _mat.m10;
            temp.y = _mat.m31 + _mat.m11;
            temp.z = _mat.m32 + _mat.m12;
            _planes[2].normal = temp;

            // 上面
            temp.x = _mat.m30 - _mat.m10;
            temp.y = _mat.m31 - _mat.m11;
            temp.z = _mat.m32 - _mat.m12;
            _planes[3].normal = temp;

            // 法線の正規化
            for (int i = 0; i < _planes.Length; i++)
            {
                float length = _planes[i].normal.magnitude;
                temp = _planes[i].normal;
                temp.x /= length;
                temp.y /= length;
                temp.z /= length;
                _planes[i].normal = temp;
            }
        }

        // 更新処理
        public void Update(ComputeShader _computeShader, Camera _camera, int _numParticles, ComputeBuffer _particleBuffer, ComputeBuffer _activeList)
        {
            // 視錘台カリングを行うカーネル
            int kernel = _computeShader.FindKernel("FrustumCulling");

            // 視錘台の計算
            CalculateFrustumPlanes(_camera.projectionMatrix * _camera.worldToCameraMatrix, ref m_planes);

            // 視錘台の法線を分解
            for (int i = 0; i < 4; i++)
            {
                Debug.DrawRay(_camera.transform.position, m_planes[i].normal * 10f, Color.yellow);
                m_normalsFloat[i + 0] = m_planes[i].normal.x;
                m_normalsFloat[i + 4] = m_planes[i].normal.y;
                m_normalsFloat[i + 8] = m_planes[i].normal.z;
            }

            // バッファのカウンターをリセット
            m_inViewsAppendBuffer.SetCounterValue(0);

            // カメラの座標
            Vector3 cameraPosition = _camera.transform.position;

            // 変数を設定
            _computeShader.SetInt("_numParticles", _numParticles);
            _computeShader.SetFloats("_cameraPosition", cameraPosition.x, cameraPosition.y, cameraPosition.z);
            _computeShader.SetFloats("_cameraFrustumNormals", m_normalsFloat);

            // バッファを設定
            _computeShader.SetBuffer(kernel, "_particlesBuffer", _particleBuffer);
            _computeShader.SetBuffer(kernel, "_particleActiveList", _activeList);
            _computeShader.SetBuffer(kernel, "_inViewAppend", m_inViewsAppendBuffer);

            // カーネルを実行
            _computeShader.Dispatch(kernel, Mathf.CeilToInt((float)_activeList.count / NUM_THREAD_X), 1, 1);

            // バッファにデータを渡す
            m_inViewsCountBuffer.SetData(m_inViewsCounts);

            // 視界内のパーティクル数を取得
            ComputeBuffer.CopyCount(m_inViewsAppendBuffer, m_inViewsCountBuffer, 4);
        }
        #endregion
    }

    // 頂点情報
    struct VertexData
    {
        // 座標
        public Vector3 vertex;
        // 法線
        public Vector3 normal;
        // uv
        public Vector2 uv;
    }
    #endregion

    #region SerializeField
    // シェーダーマテリアル
    [SerializeField] Material _shaderMaterial;
    // メッシュ
    [SerializeField] Mesh _mesh;
    // テクスチャ
    [SerializeField] Texture _texture;
    // カリングを行うコンピュートシェーダー
    [SerializeField] ComputeShader _cullingComputeShader;
    // カリングを行うかどうか
    [SerializeField] bool _isCulling = true;
    #endregion

    // メンバ変数
    #region private variable
    // パーティクル数
    private int m_numParticles;
    // パーティクル情報
    private ComputeBuffer m_particlesBuffer;
    // 使用中のパーティクルの要素番号
    private ComputeBuffer m_activeIndexBuffer;
    // 使用中のパーティクルの個数
    private ComputeBuffer m_activeCountBuffer;
    // カメラごとのカリング情報
    private Dictionary<Camera, CullingData> m_cameraDatas = new Dictionary<Camera, CullingData>();
    // メッシュ情報のバッファ
    private ComputeBuffer m_meshIndicesBuffer;
    private ComputeBuffer m_meshVertexDataBuffer;
    // メッシュの頂点番号数
    private int m_numMeshIndices;
    #endregion

    // メンバ関数
    #region private method
    // 初期化処理
    private void Initialize()
    {
        // パーティクルマネージャーを取得
        GPUParticleManager particle = GetComponent<GPUParticleManager>();

        // パーティクルマネージャーが見つかったか判定
        if (particle != null)
        {
            // パーティクルマネージャーの情報を取得する
            m_numParticles = particle.GetParticleNum();
            m_particlesBuffer = particle.GetParticleBuffer();
            m_activeIndexBuffer = particle.GetActiveParticleBuffer();
            m_activeCountBuffer = particle.GetParticleCountBuffer();
        }
        else
        {
            Debug.LogError("Particle Class Not Found!!" + typeof(GPUParticleManager).FullName);
        }

        // メッシュ情報バッファの初期化処理
        InitMeshDataBuffer(_mesh, out m_meshVertexDataBuffer, out m_meshIndicesBuffer, out m_numMeshIndices);
    }

    // メッシュ情報バッファの初期化処理
    private void InitMeshDataBuffer(Mesh _mesh, out ComputeBuffer _vertexDatasBuffer, out ComputeBuffer _indicesBuffer, out int _numIndices)
    {
        // メッシュの頂点番号を取得
        int[] indices = _mesh.GetIndices(0);

        // 頂点データを取得
        VertexData[] vertexDatas = Enumerable.Range(0, _mesh.vertexCount).Select(b =>
        {
            return new VertexData()
            {
                vertex = _mesh.vertices[b],
                normal = _mesh.normals[b],
                uv = _mesh.uv[b],
            };
        }).ToArray();

        // 頂点番号数を取得
        _numIndices = indices.Length;

        // バッファを生成
        _vertexDatasBuffer = new ComputeBuffer(vertexDatas.Length, Marshal.SizeOf(typeof(VertexData)));
        _indicesBuffer = new ComputeBuffer(indices.Length, Marshal.SizeOf(typeof(uint)));

        // バッファにデータを渡す
        _vertexDatasBuffer.SetData(vertexDatas);
        _indicesBuffer.SetData(indices);
    }

    // カメラごとのカリングデータの更新処理
    private void UpdateCullingDatas(Camera _camera)
    {
        // カメラのカリングデータを取得
        CullingData data = m_cameraDatas[_camera];

        //　カリングデータが見つかったか判定する
        if (data == null)
        {
            // カリングデータを設定
            data = m_cameraDatas[_camera] = new CullingData(m_numParticles);
            data.SetVertexCount(m_numMeshIndices);
        }

        // カリングデータの更新処理
        data.Update(_cullingComputeShader, _camera, m_numParticles, m_particlesBuffer, m_activeIndexBuffer);
    }

    // カリングを行うカメラの更新処理
    private void UpdateCullingCameras()
    {
        // カリングを行うカメラを取得
        Camera[] cameras = m_cameraDatas.Keys.ToArray();

        // カメラごとのカリングデータの更新処理
        for (int i = cameras.Length - 1; i >= 0; i--)
        {
            // カメラが見つかったか判定
            if (cameras[i] == null)
            {
                m_cameraDatas.Remove(cameras[i]);
            }
            else if (cameras[i].isActiveAndEnabled)
            {
                UpdateCullingDatas(cameras[i]);
            }
        }
    }

    // シェーダーの値の設定
    private void SetMaterialParam()
    {
        // 変数の設定
        _shaderMaterial.SetTexture("_mainTexture", _texture);
        _shaderMaterial.SetBuffer("_vertices", m_meshVertexDataBuffer);
        _shaderMaterial.SetBuffer("_indices", m_meshIndicesBuffer);

        // バッファの設定
        _shaderMaterial.SetBuffer("_particles", m_particlesBuffer);
        _shaderMaterial.SetBuffer("_particleActiveList", m_activeIndexBuffer);

        // パスの設定
        _shaderMaterial.SetPass(0);
    }

    // 描画処理
    private void OnRenderObjectInternal()
    {
        // カリングを行うか判定
        if (_isCulling)
        {
            // カメラを取得
            Camera cam = Camera.current;

            // カメラが存在するか判定
            if (!m_cameraDatas.ContainsKey(cam))
            {
                // このフレームは登録だけ
                m_cameraDatas[cam] = null;
            }
            else
            {
                // カメラのカリングデータを取得
                CullingData data = m_cameraDatas[cam];

                // カリングデータが存在するか判定
                if (data != null)
                {
                    // シェーダーの値の設定
                    SetMaterialParam();

                    // バッファを設定
                    _shaderMaterial.SetBuffer("_inViewsList", data.m_inViewsAppendBuffer);

                    // キーワードを有効化
                    _shaderMaterial.EnableKeyword("GPUPARTICLE_CULLING_ON");

                    // 描画処理
                    Graphics.DrawProceduralIndirect(MeshTopology.Triangles, data.m_inViewsCountBuffer);
                }
            }
        }
        else
        {
            // シェーダーの値の設定
            SetMaterialParam();

            // キーワードを無効化
            _shaderMaterial.DisableKeyword("GPUPARTICLE_CULLING_ON");

            // 描画処理
            Graphics.DrawProceduralIndirect(MeshTopology.Triangles, m_activeCountBuffer);

        }
    }

    // バッファの開放処理
    private void ReleaseBuffers()
    {
        m_cameraDatas.Values.Where(d => d != null).ToList().ForEach(d => d.ReleaseBuffers());
        m_cameraDatas.Clear();
        if (m_meshIndicesBuffer != null)
        {
            m_meshIndicesBuffer.Release();
            m_meshIndicesBuffer = null;
        }
        if (m_meshVertexDataBuffer != null)
        {
            m_meshVertexDataBuffer.Release();
            m_meshVertexDataBuffer = null;
        }
    }
    #endregion

    #region unity method
    void Start()
    {
        Initialize();
    }

    void LateUpdate()
    {
        if (_isCulling)
        {
            UpdateCullingCameras();
        }
    }

    void OnRenderObject()
    {
        if ((Camera.current.cullingMask & (1 << gameObject.layer)) == 0) return;
        OnRenderObjectInternal();
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }
    #endregion
}