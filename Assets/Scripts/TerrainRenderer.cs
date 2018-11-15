using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainRenderer : MonoBehaviour
{
    // エディター上で設定する変数
    #region SerializeField
    [SerializeField] Mesh _mesh;
    [SerializeField] Material _material;
    #endregion

    // メンバ変数
    #region member variable
    private ComputeBuffer m_argsBuffer;
    private ComputeBuffer m_vertexBuffer;
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
