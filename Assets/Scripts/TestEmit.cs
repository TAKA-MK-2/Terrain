using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestEmit : MonoBehaviour
{

    #region SerializeField
    [SerializeField] GPUParticleEmitter _particleEmitter;
    #endregion

    #region member variable
    private bool m_toggle = false;
    #endregion

    // Update is called once per frame
    void Update()
    {
        // 1フレーム
        if (Input.GetMouseButtonDown(0))
        {
            _particleEmitter.Emit();
        }
        if (Input.GetMouseButtonDown(1))
        {
            m_toggle = !m_toggle;
        }
        if (m_toggle)
        {
            _particleEmitter.Emit();
        }
    }
}