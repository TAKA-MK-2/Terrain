using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // エディター上で設定する変数
    #region SerializeField
    [SerializeField] TerrainManager _terrain;
    #endregion

    void Start()
    {
        
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.UpArrow))
        {
            transform.Translate(transform.forward);
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            transform.Translate(-transform.forward);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            transform.Translate(transform.right);
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Translate(-transform.right);
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _terrain.PutFootsteps(transform.position);
        }
    }
}
