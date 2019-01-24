using UnityEngine;
using Utility;

public class GenerateTerrain : MonoBehaviour
{
    // 定数
    #region define
    // ComputeShaderのスレッド数
    protected const int THREAD_NUM = (8);
    //フィールドのサイズ
    private const float FIELD_SIZE = (100.0f);
    #endregion

    // エディター上で設定する変数
    #region SerializeField
    // コンピュートシェーダー
    [SerializeField] ComputeShader _computeShader;
    // メッシュ
    [SerializeField] Mesh _mesh;
    // シェーダーマテリアル
    [SerializeField] Material _shaderMaterial;
    // テクスチャ
    [SerializeField] Texture _texture;
    // 1辺の頂点数
    [SerializeField] [Range(8, 1024)] int _numVertices = 256;
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
    // 頂点計算カーネル
    private int m_calculateKernel;
    // 足跡カーネル
    private int m_putKernel;
    // スレッドグループ数
    private int m_numThreadGroups;
    // 頂点座標
    private ComputeBuffer m_verticesBuffer;
    // GPUインスタンシングの引数
    private ComputeBuffer m_argsBuffer;
    // GPUインスタンシングの引数
    private uint[] m_args = new uint[5] { 0, 0, 0, 0, 0 };
    #endregion

    // private関数
    #region private method
    // 初期化処理
    void Initialize()
    {
        // メンバ変数の初期化処理
        _numVertices = Mathf.CeilToInt(_numVertices / (float)THREAD_NUM) * THREAD_NUM;
        m_numVertices = _numVertices * _numVertices;
        m_distance = FIELD_SIZE / _numVertices;
        m_calculateKernel = _computeShader.FindKernel("CalculateVertices");
        m_putKernel = _computeShader.FindKernel("PutFootsteps");
        m_numThreadGroups = _numVertices / THREAD_NUM;
        m_verticesBuffer = new ComputeBuffer(m_numVertices, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)));
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
        _computeShader.SetBuffer(m_calculateKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._verticesBuffer), m_verticesBuffer);

        // 変数を設定
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._fieldSize), FIELD_SIZE);
        _computeShader.SetInt(ShaderDefines.GetIntPropertyID(ShaderDefines.IntID._numVertices), _numVertices);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._distance), m_distance);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._height), _height);
        _computeShader.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._smoothness), _smoothness * _numVertices);
        _computeShader.SetVector(ShaderDefines.GetVectorPropertyID(ShaderDefines.VectorID._offset), new Vector2(_offsetX, _offsetY));

        // カーネルの実行
        _computeShader.Dispatch(m_calculateKernel, m_numThreadGroups, 1, m_numThreadGroups);
    }

    // 地形描画
    void RenderTerrain()
    {
        // テクスチャを設定
        _shaderMaterial.SetTexture(ShaderDefines.GetTexturePropertyID(ShaderDefines.TextureID._mainTexture), _texture);
        
        // 変数を設定
        _shaderMaterial.SetInt(ShaderDefines.GetIntPropertyID(ShaderDefines.IntID._numVertices), _numVertices);
        _shaderMaterial.SetFloat(ShaderDefines.GetFloatPropertyID(ShaderDefines.FloatID._height), _height);

        // バッファの設定
        _shaderMaterial.SetBuffer(ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._verticesBuffer), m_verticesBuffer);

        // 描画処理
        Graphics.DrawMeshInstancedIndirect(_mesh, 0, _shaderMaterial, new Bounds(Vector3.zero, new Vector3(1000.0f, 1000.0f, 1000.0f)), m_argsBuffer);
    }

    // バッファの開放処理
    void ReleaseBuffer()
    {
        if (m_verticesBuffer != null)
        {
            m_verticesBuffer.Release();
            m_verticesBuffer = null;
        }
        if (m_argsBuffer != null)
        {
            m_argsBuffer.Release();
            m_verticesBuffer = null;
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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            int x = Random.Range(-50, 50);
            int y = Random.Range(0, 25);
            int z = Random.Range(-50, 50);
            _computeShader.SetVector("_playerPosition", new Vector3(x, y, z));
            _computeShader.SetBuffer(m_putKernel, ShaderDefines.GetBufferPropertyID(ShaderDefines.BufferID._verticesBuffer), m_verticesBuffer);
            _computeShader.Dispatch(m_putKernel, m_numThreadGroups * m_numThreadGroups, 1, 1);
        }
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