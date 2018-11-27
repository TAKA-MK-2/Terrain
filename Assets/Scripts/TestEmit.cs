using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestEmit : MonoBehaviour
{

    #region SerializeField
    [SerializeField] GPUParticleEmitter _particleEmitter;
    #endregion

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePosition = Input.mousePosition;
            mousePosition.z = 10;
            mousePosition = Camera.main.ScreenToWorldPoint(mousePosition);
            _particleEmitter.Emit(mousePosition);
        }
    }
}