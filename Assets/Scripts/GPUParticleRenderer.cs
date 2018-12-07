using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Utility;

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


        #region private variable
        // 視錘台
        private Plane[] m_planes = new Plane[4];
        // 視錘台の法線を分割した配列
        private float[] m_normalsFloat = new float[12];
        // 視界内のパーティクルの要素番号
        private ComputeBuffer m_inViewParticlesBuffer;
        // 視界内のパーティクルの個数
        private ComputeBuffer m_inViewCountsBuffer;
        // [0]インスタンスあたりの頂点数 [1]インスタンス数 [2]開始する頂点位置 [3]開始するインスタンス
        private int[] m_inViewCounts = { 0, 0, 0, 0 };
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
        public void Update(ComputeShader _computeShader, Camera _camera, int _numParticles, ComputeBuffer _particlesBuffer, ComputeBuffer _activeParticlesBuffer)
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
            m_inViewParticlesBuffer.SetCounterValue(0);

            // カメラの座標
            Vector3 cameraPosition = _camera.transform.position;

            // 変数を設定
            _computeShader.SetInt(ShaderDefines.GetIntPropertyID(ShaderDefines.IntID._numParticles), _numParticles);
            _computeShader.SetFloats(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._cameraPosition), cameraPosition.x, cameraPosition.y, cameraPosition.z);
            _computeShader.SetFloats(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._cameraFrustumNormals), m_normalsFloat);

            // バッファを設定
            _computeShader.SetBuffer(kernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particlesBuffer), _particlesBuffer);
            _computeShader.SetBuffer(kernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._activeParticlesBuffer), _activeParticlesBuffer);
            _computeShader.SetBuffer(kernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._inViewParticlesBuffer), m_inViewParticlesBuffer);

            // カーネルを実行
            _computeShader.Dispatch(kernel, Mathf.CeilToInt((float)_activeParticlesBuffer.count / NUM_THREAD_X), 1, 1);

            // バッファにデータを渡す
            m_inViewCountsBuffer.SetData(m_inViewCounts);

            // 視界内のパーティクル数を取得
            ComputeBuffer.CopyCount(m_inViewParticlesBuffer, m_inViewCountsBuffer, 4);
        }
        #endregion

        #region getter
        // 視界内のパーティクルの要素番号バッファを取得する
        public ComputeBuffer GetInViewParticlesBuffer() { return m_inViewParticlesBuffer; }

        // 視界内のパーティクルの個数を取得する
        public ComputeBuffer GetInViewCountsBuffer() { return m_inViewCountsBuffer; }
        #endregion

        #region public method
        // コンストラクタ
        public CullingData(int _numParticles)
        {
            // コンピュートバッファの生成
            m_inViewParticlesBuffer = new ComputeBuffer(_numParticles, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            m_inViewCountsBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
        }

        // インスタンスあたりの頂点数を設定
        public void SetVertexCount(int vertexCount)
        {
            m_inViewCounts[0] = vertexCount;
        }

        // バッファの開放処理
        public void ReleaseBuffers()
        {
            if (m_inViewParticlesBuffer != null)
            {
                m_inViewParticlesBuffer.Release();
                m_inViewParticlesBuffer = null;
            }
            if (m_inViewCountsBuffer != null)
            {
                m_inViewCountsBuffer.Release();
                m_inViewCountsBuffer = null;
            }
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
    private ComputeBuffer m_activeParticlesBuffer;
    // 使用中のパーティクルの個数
    private ComputeBuffer m_activeCountBuffer;
    // カメラごとのカリング情報
    private Dictionary<Camera, CullingData> m_cameraDatas = new Dictionary<Camera, CullingData>();
    // メッシュ情報のバッファ
    private ComputeBuffer m_meshIndicesBuffer;
    private ComputeBuffer m_meshVertexDatasBuffer;
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
            m_activeParticlesBuffer = particle.GetActiveParticleBuffer();
            m_activeCountBuffer = particle.GetParticleCountBuffer();
        }
        else
        {
            Debug.LogError("Particle Class Not Found!!" + typeof(GPUParticleManager).FullName);
        }

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
        m_numMeshIndices = indices.Length;

        // バッファを生成
        m_meshVertexDatasBuffer = new ComputeBuffer(vertexDatas.Length, Marshal.SizeOf(typeof(VertexData)));
        m_meshIndicesBuffer = new ComputeBuffer(indices.Length, Marshal.SizeOf(typeof(uint)));

        // バッファにデータを渡す
        m_meshVertexDatasBuffer.SetData(vertexDatas);
        m_meshIndicesBuffer.SetData(indices);
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
        data.Update(_cullingComputeShader, _camera, m_numParticles, m_particlesBuffer, m_activeParticlesBuffer);
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
        _shaderMaterial.SetTexture(ShaderDefines.GetTexturePropertyID(ShaderDefines.TextureID._mainTexture), _texture);

        // バッファの設定
        _shaderMaterial.SetBuffer(ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._meshIndicesBuffer), m_meshIndicesBuffer);
        _shaderMaterial.SetBuffer(ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._meshVertexDatasBuffer), m_meshVertexDatasBuffer);
        _shaderMaterial.SetBuffer(ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._particlesBuffer), m_particlesBuffer);

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
                    _shaderMaterial.SetBuffer(ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._inViewParticlesBuffer), data.GetInViewParticlesBuffer());

                    // キーワードを有効化
                    _shaderMaterial.EnableKeyword("GPUPARTICLE_CULLING_ON");

                    // 描画処理
                    Graphics.DrawProceduralIndirect(MeshTopology.Triangles, data.GetInViewCountsBuffer());
                }
            }
        }
        else
        {
            // シェーダーの値の設定
            SetMaterialParam();

            // バッファを設定
            _shaderMaterial.SetBuffer(ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._activeParticlesBuffer), m_activeParticlesBuffer);

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
        if (m_meshVertexDatasBuffer != null)
        {
            m_meshVertexDatasBuffer.Release();
            m_meshVertexDatasBuffer = null;
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