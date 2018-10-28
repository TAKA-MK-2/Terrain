using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class GPUParticleRenderer : MonoBehaviour
{
    #region define
    public class CullingData
    {
        public ComputeBuffer inViewsAppendBuffer;
        public ComputeBuffer inViewsCountBuffer;
        //public int inViewsNum;

        public int[] inViewsCounts = { 0, 1, 0, 0 };    // [0]インスタンスあたりの頂点数 [1]インスタンス数 [2]開始する頂点位置 [3]開始するインスタンス

        public CullingData(int particleNum)
        {
            inViewsAppendBuffer = new ComputeBuffer(particleNum, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            inViewsAppendBuffer.SetCounterValue(0);
            inViewsCountBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
            //inViewsCounts = new int[] { 0, 1, 0, 0 };
            inViewsCountBuffer.SetData(inViewsCounts);
        }

        /// <summary>
        /// 頂点数セット
        /// </summary>
        /// <param name="num"></param>
        public void SetVertexCount(int num)
        {
            inViewsCounts[0] = num;
        }

        public void Release()
        {
            if (inViewsAppendBuffer != null)
            {
                inViewsAppendBuffer.Release();
                inViewsAppendBuffer = null;
            }
            if (inViewsCountBuffer != null)
            {
                inViewsCountBuffer.Release();
                inViewsCountBuffer = null;
            }
        }

        private int[] debugCount = { 0, 0, 0, 0 };
        /// <summary>
        /// 視界内のパーティクルの数を取得（デバッグ機能）
        /// </summary>
        /// <returns></returns>
        public int GetInViewNum()
        {
            inViewsCountBuffer.GetData(debugCount);
            return debugCount[1];
        }

        const int NUM_THREAD_X = 32;

        private Plane[] _planes = new Plane[4];
        private float[] _normalsFloat = new float[12];  // 4x3
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
            inViewsAppendBuffer.SetCounterValue(0);

            var cPos = camera.transform.position;
            cs.SetFloats("_CameraPos", cPos.x, cPos.y, cPos.z);

            cs.SetInt("_ParticleNum", particleNum);
            cs.SetFloats("_CameraFrustumNormals", _normalsFloat);
            cs.SetBuffer(kernel, "_InViewAppend", inViewsAppendBuffer);
            cs.SetBuffer(kernel, "_ParticleBuffer", particleBuffer);
            cs.SetBuffer(kernel, "_ParticleActiveList", activeList);
            cs.Dispatch(kernel, Mathf.CeilToInt((float)activeList.count / NUM_THREAD_X), 1, 1);

            inViewsCountBuffer.SetData(inViewsCounts);
            ComputeBuffer.CopyCount(inViewsAppendBuffer, inViewsCountBuffer, 4);    // インスタンス数
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

    public Material material;
    public ComputeShader cullingCS;
    public bool isCulling = true;
    public float scale = 1;
    public Mesh mesh;
    public Vector3 rotationOffsetAxis = Vector3.right;
    public float rotationOffsetAngle = 0;
    // 計算済みのRotationOffset
    [HideInInspector]
    public Vector4 rotateOffset;    // 計算済みのRotationOffset

    private GPUParticleManager particle;
    private int particleNum;
    private ComputeBuffer particleBuffer;
    private ComputeBuffer activeIndexBuffer;
    private ComputeBuffer activeCountBuffer;
    private Dictionary<Camera, CullingData> cameraDatas = new Dictionary<Camera, CullingData>();
    // メッシュデータ
    private ComputeBuffer meshIndicesBuffer;
    private ComputeBuffer meshVertexBuffer;
    private int meshIndicesNum;

    void InitMeshDataBuffer(Mesh mesh, out ComputeBuffer vertexBuffer, out ComputeBuffer indicesBuffer, out int indicesNum)
    {
        //Debug.Log("Mesh " + mesh.name);
        //Debug.Log("Vertex " + mesh.vertexCount);
        //Debug.Log("Normal " + mesh.normals.Length);
        //Debug.Log("UV " + mesh.uv.Length);
        //Debug.Log("TANGENTS " + mesh.tangents.Length);

        var indices = mesh.GetIndices(0);
        var vertexDataArray = Enumerable.Range(0, mesh.vertexCount).Select(b =>
        {
            //Debug.Log("b: " + b + " / " + mesh.vertexCount);
            return new VertexData()
            {
                vertex = mesh.vertices[b],
                normal = mesh.normals[b],
                uv = mesh.uv[b],
                tangent = mesh.tangents[b],
            };
        }).ToArray();

        indicesNum = indices.Length;
        indicesBuffer = new ComputeBuffer(indices.Length, Marshal.SizeOf(typeof(uint)));
        vertexBuffer = new ComputeBuffer(vertexDataArray.Length, Marshal.SizeOf(typeof(VertexData)));
        indicesBuffer.SetData(indices);
        vertexBuffer.SetData(vertexDataArray);
    }

    void UpdateVertexBuffer(Camera camera)
    {
        CullingData data = cameraDatas[camera];
        if (data == null)
        {
            data = cameraDatas[camera] = new CullingData(particleNum);
            data.SetVertexCount(meshIndicesNum);
        }

        //_SetCommonParameterForCS(cullingCS);
        data.Update(cullingCS, camera, particleNum, particleBuffer, activeIndexBuffer);
    }

    void UpdateRotationOffsetAxis()
    {
        rotateOffset.x = rotationOffsetAxis.x;
        rotateOffset.y = rotationOffsetAxis.y;
        rotateOffset.z = rotationOffsetAxis.z;
        rotateOffset.w = rotationOffsetAngle * Mathf.Deg2Rad;
    }

    void SetMaterialParam()
    {
        UpdateRotationOffsetAxis();

        material.SetBuffer("_vertices", meshVertexBuffer);
        material.SetBuffer("_indices", meshIndicesBuffer);
        //material.SetVector("_RotationOffsetAxis", new Vector4(rotationOffsetAxis.x, rotationOffsetAxis.y, rotationOffsetAxis.z, rotationOffsetAngle * Mathf.Deg2Rad));
        material.SetVector("_RotationOffsetAxis", rotateOffset);

        material.SetBuffer("_particles", particleBuffer);
        material.SetBuffer("_ParticleActiveList", activeIndexBuffer);

        material.SetVector("_upVec", Vector3.up);
        material.SetPass(0);
    }

    void Start()
    {
        particle = GetComponent<GPUParticleManager>();
        if (particle != null)
        {
            particleNum = particle.GetParticleNum();
            particleBuffer = particle.GetParticleBuffer();
            activeIndexBuffer = particle.GetActiveParticleBuffer();
            activeCountBuffer = particle.GetParticleCountBuffer();
            //Debug.Log("particleNum " + particleNum);
        }
        else
        {
            Debug.LogError("Particle Class Not Found!!" + typeof(GPUParticleManager).FullName);
        }
        InitMeshDataBuffer(mesh, out meshVertexBuffer, out meshIndicesBuffer, out meshIndicesNum);
    }

    void OnRenderObjectInternal()
    {
        if (isCulling)
        {
            var cam = Camera.current;

            if (!cameraDatas.ContainsKey(cam))
            {
                cameraDatas[cam] = null; // このフレームは登録だけ
            }
            else
            {
                var data = cameraDatas[cam];
                if (data != null)
                {
                    SetMaterialParam();

                    material.EnableKeyword("GPUPARTICLE_CULLING_ON");

                    material.SetBuffer("_InViewsList", data.inViewsAppendBuffer);

                    //data.inViewsCountBuffer.GetData(debugCount);

                    //Graphics.DrawProcedural(MeshTopology.Triangles, meshIndicesNum, data.inViewsNum);   // 視界範囲内のものだけ描画
                    Graphics.DrawProceduralIndirect(MeshTopology.Triangles, data.inViewsCountBuffer);   // 視界範囲内のものだけ描画

                    //Debug.Log(name + " [0] " + debugCount[0] + " [1] " + debugCount[1] + " [2] " + debugCount[2] + " [3] " + debugCount[3]);

                }
            }
        }
        else
        {
            SetMaterialParam();

            material.DisableKeyword("GPUPARTICLE_CULLING_ON");

            //Graphics.DrawProcedural(MeshTopology.Triangles, meshIndicesNum, particle.GetActiveParticleNum());   // Activeなものをすべて描画
            Graphics.DrawProceduralIndirect(MeshTopology.Triangles, particle.GetParticleCountBuffer());   // 視界範囲内のものだけ描画

        }
    }

    void OnRenderObject()
    {
        if ((Camera.current.cullingMask & (1 << gameObject.layer)) == 0)
            return;

        OnRenderObjectInternal();
    }

    void LateUpdate()
    {
        if (isCulling)
        {
            //Dictionary<Camera, CullingData>.KeyCollection keys = cameraDatas.Keys;
            //int count = keys.Count;
            Camera[] cameras = cameraDatas.Keys.ToArray();
            for (int i = cameras.Length - 1; i >= 0; i--)
            {
                if (cameras[i] == null)
                {
                    cameraDatas.Remove(cameras[i]);
                }
                else if (cameras[i].isActiveAndEnabled)
                {
                    UpdateVertexBuffer(cameras[i]);
                }
            }
            //keys = cameraDatas.Keys;
            //			foreach (Camera cam in keys) {
            //				if(cam.isActiveAndEnabled){
            //					UpdateVertexBuffer(cam);
            //				}
            //			}

            ////
            //            cameraDatas.Keys.Where(cam => cam == null).ToList().ForEach(cam => cameraDatas.Remove(cam));
            //            cameraDatas.Keys
            //                .Where(cam => cam.isActiveAndEnabled)
            //                .ToList().ForEach(cam =>
            //                {
            //                    UpdateVertexBuffer(cam);
            //                });
        }
    }

    void ReleaseBuffer()
    {
        cameraDatas.Values.Where(d => d != null).ToList().ForEach(d => d.Release());
        cameraDatas.Clear();
        if (meshIndicesBuffer != null)
        {
            meshIndicesBuffer.Release();
            meshIndicesBuffer = null;
        }
        if (meshVertexBuffer != null)
        {
            meshVertexBuffer.Release();
            meshVertexBuffer = null;
        }
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }

}