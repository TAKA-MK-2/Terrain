using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class GPUParticleRenderer : MonoBehaviour
{
    // 定数
    #region define
    public class CullingData
    {
        // ビュー内の追加バッファ
        public ComputeBuffer m_inViewsAppendBuffer;
        // inViewsAppendBufferの個数バッファ
        public ComputeBuffer m_inViewsCountBuffer;
        // [0]インスタンスあたりの頂点数 [1]インスタンス数 [2]開始する頂点位置 [3]開始するインスタンス
        public int[] inViewsCounts = { 0, 1, 0, 0 };

        // コンストラクタ
        public CullingData(int _numParticles)
        {
            // コンピュートバッファを生成する
            m_inViewsAppendBuffer = new ComputeBuffer(_numParticles, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            m_inViewsCountBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);

            // 追加バッファのカウンタを0にする
            m_inViewsAppendBuffer.SetCounterValue(0);

            // バッファにデータを設定する
            m_inViewsCountBuffer.SetData(inViewsCounts);
        }

        // 頂点数を設定する
        public void SetVertexCount(int num)
        {
            inViewsCounts[0] = num;
        }

        // バッファの解放処理
        public void Release()
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

        private int[] debugCount = { 0, 0, 0, 0 };

        // 視界内のパーティクルの数を取得（デバッグ機能）
        public int GetInViewNum()
        {
            m_inViewsCountBuffer.GetData(debugCount);
            return debugCount[1];
        }

        // ComputeShaderのスレッド数
        const int NUM_THREAD_X = 32;

        // 平面
        private Plane[] _planes = new Plane[4];
        // 4x3
        private float[] _normalsFloat = new float[12];
        Vector3 temp;

        private void CalculateFrustumPlanes(Matrix4x4 mat, ref Plane[] planes)
        {
            // left
            temp.x = mat.m30 + mat.m00;
            temp.y = mat.m31 + mat.m01;
            temp.z = mat.m32 + mat.m02;
            planes[0].normal = temp;
            //planes[0].distance = mat.m33 + mat.m03;

            // right
            temp.x = mat.m30 - mat.m00;
            temp.y = mat.m31 - mat.m01;
            temp.z = mat.m32 - mat.m02;
            planes[1].normal = temp;
            //planes[1].distance = mat.m33 - mat.m03;

            // bottom
            temp.x = mat.m30 + mat.m10;
            temp.y = mat.m31 + mat.m11;
            temp.z = mat.m32 + mat.m12;
            planes[2].normal = temp;
            //planes[2].distance = mat.m33 + mat.m13;

            // top
            temp.x = mat.m30 - mat.m10;
            temp.y = mat.m31 - mat.m11;
            temp.z = mat.m32 - mat.m12;
            planes[3].normal = temp;
            //planes[3].normal = new Vector3(mat.m30 - mat.m10, mat.m31 - mat.m11, mat.m32 - mat.m12);
            //planes[3].distance = mat.m33 - mat.m13;

            //// near
            //planes[4].normal = new Vector3(mat.m30 + mat.m20, mat.m31 + mat.m21, mat.m32 + mat.m22);
            //planes[4].distance = mat.m33 + mat.m23;

            //// far
            //planes[5].normal = new Vector3(mat.m30 - mat.m20, mat.m31 - mat.m21, mat.m32 - mat.m22);
            //planes[5].distance = mat.m33 - mat.m23;

            // normalize
            for (uint i = 0; i < planes.Length; i++)
            {
                float length = planes[i].normal.magnitude;
                temp = planes[i].normal;
                temp.x /= length;
                temp.y /= length;
                temp.z /= length;
                planes[i].normal = temp;
                //planes[i].normal /= length;
                //planes[i].distance /= length;
            }
        }

        public void Update(ComputeShader cs, Camera camera, int particleNum, ComputeBuffer particleBuffer, ComputeBuffer activeList)
        {
            int kernel = cs.FindKernel("CheckCameraCulling");

            CalculateFrustumPlanes(camera.projectionMatrix * camera.worldToCameraMatrix, ref _planes);
            for (int i = 0; i < 4; i++)
            {
                //Debug.DrawRay(camera.transform.position, _planes[i].normal * 10f, Color.yellow);
                _normalsFloat[i + 0] = _planes[i].normal.x;
                _normalsFloat[i + 4] = _planes[i].normal.y;
                _normalsFloat[i + 8] = _planes[i].normal.z;
            }
            m_inViewsAppendBuffer.SetCounterValue(0);

            var cPos = camera.transform.position;
            cs.SetFloats("_CameraPos", cPos.x, cPos.y, cPos.z);

            cs.SetInt("_numParticles", particleNum);
            cs.SetFloats("_cameraFrustumNormals", _normalsFloat);
            cs.SetBuffer(kernel, "_inViewAppend", m_inViewsAppendBuffer);
            cs.SetBuffer(kernel, "_particlesBuffer", particleBuffer);
            cs.SetBuffer(kernel, "_particleActiveList", activeList);
            cs.Dispatch(kernel, Mathf.CeilToInt((float)activeList.count / NUM_THREAD_X), 1, 1);

            m_inViewsCountBuffer.SetData(inViewsCounts);
            ComputeBuffer.CopyCount(m_inViewsAppendBuffer, m_inViewsCountBuffer, 4);    // インスタンス数
            //inViewsCountBuffer.GetData(inViewsCounts);
            //inViewsNum = inViewsCounts[0];
            //Debug.Log("inViewsCounts " + inViewsCounts[0]);

            // debug
            //if (Input.GetKeyDown(KeyCode.M))
            //{
            //    //inViewsCountBuffer.GetData(inViewsCounts);
            //    //Debug.Log("inViewsCounts " + inViewsCounts[0]);

            //    DumpAppendData(inViewsAppendBuffer, particleNum, "inviews");

            //    DumpAppendData(activeList, particleNum, "activeList");
            //}

        }

        void DumpAppendData(ComputeBuffer cb, int size, string name)
        {
            var data = new uint[size];
            cb.GetData(data);
            StreamWriter sw;
            FileInfo fi;
            string date = System.DateTime.Now.ToString("yyyyMMddHHmmss");
            fi = new FileInfo(Application.dataPath + "/../" + name + date + ".csv");
            sw = fi.AppendText();
            for (int i = 0; i < data.Length; i++)
            {
                //Debug.Log("[" + i + "] GridHash " + gridHashDataArray[i] + " index " + sortedIndexDataArray[i]);
                //sw.WriteLine("" + i + "," + debugData[i].isActive + "," + debugData[i].position + "," + debugData[i].velocity + "," + debugData[i].rotation + "," + debugData[i].animeTime + "," + debugData[i].speed + "," + debugData[i].offsetLimit);
                sw.WriteLine("" + i + "," + data[i]);
            }
            sw.Flush();
            sw.Close();
            Debug.Log("Dump AppendBuffer Data " + fi.FullName);
        }
    }
    struct VertexData
    {
        public Vector3 vertex;
        public Vector3 normal;
        public Vector2 uv;
        public Vector4 tangent;
    }

    #endregion

    // エディターから設定する変数
    #region SerializeField
    // シェーダーマテリアル
    [SerializeField] Material _shaderMaterial;
    // パーティクルのテクスチャ
    [SerializeField] Texture _texture;
    // カリングを計算するコンピュートシェーダー
    [SerializeField] ComputeShader _cullingComputeShader;
    // カリングを行うかどうか
    [SerializeField] bool _isCulling = true;
    // スケール
    [SerializeField] float _scale = 1;
    // パーティクルのメッシュ
    [SerializeField] Mesh _mesh;
    // 回転の軸
    [SerializeField] Vector3 _rotationOffsetAxis = Vector3.right;
    // 回転の角度
    [SerializeField] float _rotationOffsetAngle = 0;
    #endregion

    // メンバ変数
    #region member variable
    // GPUパーティクル
    private GPUParticleManager m_particle;
    // パーティクル数
    private int m_numParticles;
    // パーティクル構造体のバッファ
    private ComputeBuffer m_particlesBuffer;
    // 使用中のパーティクルの番号のバッファ
    private ComputeBuffer m_activeIndexBuffer;
    // activeIndexBuffer内の個数バッファ
    private ComputeBuffer m_activeCountBuffer;
    // カメラごとのカリングデータ
    private Dictionary<Camera, CullingData> m_cameraDatas = new Dictionary<Camera, CullingData>();
    // メッシュデータのバッファ
    private ComputeBuffer m_meshIndicesBuffer;
    private ComputeBuffer m_meshVertexDataBuffer;
    // メッシュの頂点番号数
    private int m_numMeshIndices;
    // 計算済みのRotationOffset
    private Vector4 rotateOffset;
    #endregion

    // メンバ関数
    #region method
    // メッシュデータのバッファの初期化処理
    void InitMeshDataBuffer(Mesh _mesh, out ComputeBuffer _vertexDataBuffer, out ComputeBuffer _indicesBuffer, out int _numIndices)
    {
        //Debug.Log("Mesh " + _mesh.name);
        //Debug.Log("Vertex " + _mesh.vertexCount);
        //Debug.Log("Normal " + _mesh.normals.Length);
        //Debug.Log("UV " + _mesh.uv.Length);
        //Debug.Log("TANGENTS " + _mesh.tangents.Length);

        // メッシュの頂点番号を取得
        int[] indices = _mesh.GetIndices(0);

        // 頂点データを取得
        VertexData[] vertexDataArray = Enumerable.Range(0, _mesh.vertexCount).Select(b =>
        {
            Debug.Log("b: " + b + " / " + _mesh.vertexCount);
            return new VertexData()
            {
                vertex = _mesh.vertices[b],
                normal = _mesh.normals[b],
                uv = _mesh.uv[b],
                tangent = _mesh.tangents[b],
            };
        }).ToArray();

        // 頂点番号数を取得
        _numIndices = indices.Length;

        // バッファを生成する
        _indicesBuffer = new ComputeBuffer(indices.Length, Marshal.SizeOf(typeof(uint)));
        _vertexDataBuffer = new ComputeBuffer(vertexDataArray.Length, Marshal.SizeOf(typeof(VertexData)));

        // バッファにデータを設定する
        _indicesBuffer.SetData(indices);
        _vertexDataBuffer.SetData(vertexDataArray);
    }

    // 頂点データバッファの更新処理
    void UpdateVertexDataBuffer(Camera _camera)
    {
        // カメラのカリングデータを取得
        CullingData data = m_cameraDatas[_camera];

        // データがあるか判定する
        if (data == null)
        {
            // データを設定する
            data = m_cameraDatas[_camera] = new CullingData(m_numParticles);
            data.SetVertexCount(m_numMeshIndices);
        }

        // カリングデータの更新処理
        data.Update(_cullingComputeShader, _camera, m_numParticles, m_particlesBuffer, m_activeIndexBuffer);
    }

    // 回転処理
    void UpdateRotationOffsetAxis()
    {
        rotateOffset.x = _rotationOffsetAxis.x;
        rotateOffset.y = _rotationOffsetAxis.y;
        rotateOffset.z = _rotationOffsetAxis.z;
        rotateOffset.w = _rotationOffsetAngle * Mathf.Deg2Rad;
    }

    // シェーダーの値を設定する
    void SetMaterialParam()
    {
        // 回転処理
        UpdateRotationOffsetAxis();

        // シェーダーに値を設定する
        _shaderMaterial.SetTexture("_mainTexture", _texture);
        _shaderMaterial.SetBuffer("_vertices", m_meshVertexDataBuffer);
        _shaderMaterial.SetBuffer("_indices", m_meshIndicesBuffer);
        _shaderMaterial.SetVector("_RotationOffsetAxis", rotateOffset);

        _shaderMaterial.SetBuffer("_particles", m_particlesBuffer);
        _shaderMaterial.SetBuffer("_particleActiveList", m_activeIndexBuffer);

        _shaderMaterial.SetVector("_upVec", Vector3.up);
        _shaderMaterial.SetPass(0);
    }

    // GPUでオブジェクトを描画する
    void OnRenderObjectInternal()
    {
        // カリングを行うか判定
        if (_isCulling)
        {
            // カメラを取得
            Camera cam = Camera.current;

            if (!m_cameraDatas.ContainsKey(cam))
            {
                // このフレームは登録だけ
                m_cameraDatas[cam] = null;
            }
            else
            {
                CullingData data = m_cameraDatas[cam];
                if (data != null)
                {
                    // シェーダーの値を設定する
                    SetMaterialParam();
                    _shaderMaterial.EnableKeyword("GPUPARTICLE_CULLING_ON");
                    _shaderMaterial.SetBuffer("_inViewsList", data.m_inViewsAppendBuffer);

                    //data.inViewsCountBuffer.GetData(debugCount);

                    // 描画処理
                    Graphics.DrawProceduralIndirect(MeshTopology.Triangles, data.m_inViewsCountBuffer);

                    //Debug.Log(name + " [0] " + debugCount[0] + " [1] " + debugCount[1] + " [2] " + debugCount[2] + " [3] " + debugCount[3]);

                }
            }
        }
        else
        {
            // シェーダーの値を設定する
            SetMaterialParam();
            _shaderMaterial.DisableKeyword("GPUPARTICLE_CULLING_ON");

            // 描画処理
            Graphics.DrawProceduralIndirect(MeshTopology.Triangles, m_particle.GetParticleCountBuffer());

        }
    }

    // バッファの解放処理
    void ReleaseBuffer()
    {
        m_cameraDatas.Values.Where(d => d != null).ToList().ForEach(d => d.Release());
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

    void Start()
    {
        // GPUパーティクルを取得する
        m_particle = GetComponent<GPUParticleManager>();

        // GPUパーティクルが見つかったか判定する
        if (m_particle != null)
        {
            // GPUパーティクルのデータを取得する
            m_numParticles = m_particle.GetParticleNum();
            m_particlesBuffer = m_particle.GetParticleBuffer();
            m_activeIndexBuffer = m_particle.GetActiveParticleBuffer();
            m_activeCountBuffer = m_particle.GetParticleCountBuffer();
            //Debug.Log("particleNum " + particleNum);
        }
        else
        {
            Debug.LogError("Particle Class Not Found!!" + typeof(GPUParticleManager).FullName);
        }

        // メッシュデータのバッファの初期化処理
        InitMeshDataBuffer(_mesh, out m_meshVertexDataBuffer, out m_meshIndicesBuffer, out m_numMeshIndices);
    }

    void OnRenderObject()
    {
        // 
        if ((Camera.current.cullingMask & (1 << gameObject.layer)) == 0) return;
        // GPUでオブジェクトを描画する
        OnRenderObjectInternal();
    }

    void LateUpdate()
    {
        // カリングを行うか判定
        if (_isCulling)
        {
            // カリングを行うカメラを取得する
            Camera[] cameras = m_cameraDatas.Keys.ToArray();
            // 頂点データバッファの更新処理
            for (int i = cameras.Length - 1; i >= 0; i--)
            {
                if (cameras[i] == null)
                {
                    m_cameraDatas.Remove(cameras[i]);
                }
                else if (cameras[i].isActiveAndEnabled)
                {
                    UpdateVertexDataBuffer(cameras[i]);
                }
            }
        }
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }

}