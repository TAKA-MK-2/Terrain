//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class TestFootsteps : MonoBehaviour
//{
//    // エディター上で設定する変数
//    #region SerializeField
//    // コンピュートシェーダー
//    [SerializeField] ComputeShader _computeShader;
//    // 地形を生成するスクリプト
//    [SerializeField] GenerateTerrain _generateTerrain;
//    #endregion

//    // private変数
//    #region private variable
//    // 頂点数
//    private int m_numVertices;
//    // カーネルID
//    private int m_kernelID;
//    // 頂点座標
//    private ComputeBuffer m_verticesBuffer;
//    #endregion

//    // private関数
//    #region private method
//    // 初期化処理
//    void Initialize()
//    {
//        // メンバ変数の初期化処理
//        m_numVertices = _generateTerrain.GetNumVertices();
//        m_kernelID = _computeShader.FindKernel("CalculateVertices");
//        m_verticesBuffer = _generateTerrain.GetVerticesBuffer();
//        // バッファを設定
//        _computeShader.SetBuffer(m_kernelID, "_verticesBuffer", m_verticesBuffer);

//    }

//    // 
//    void FootSteps()
//    {
//        // 変数を設定
//        _computeShader.SetVector("_footPosition", transform.position);

//        // カーネルの実行
//        _computeShader.Dispatch(m_kernelID, m_numVertices, 1, 1);

//        _generateTerrain.SetVerticesBuffer(m_verticesBuffer);
//    }
//    #endregion

//    void Start()
//    {
//        Initialize();
//    }

//    void Update()
//    {
//        if (Input.GetKey(KeyCode.UpArrow))
//        {
//            transform.position += transform.forward;
//        }
//        if (Input.GetKey(KeyCode.DownArrow))
//        {
//            transform.position -= transform.forward;
//        }
//        if (Input.GetKey(KeyCode.LeftArrow))
//        {
//            transform.position -= transform.right;
//        }
//        if (Input.GetKey(KeyCode.RightArrow))
//        {
//            transform.position += transform.right;
//        }
//        if (Input.GetKeyDown(KeyCode.Space))
//        {
//            FootSteps();
//        }
//    }
//}
