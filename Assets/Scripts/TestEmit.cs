using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestEmit : MonoBehaviour
{

    #region SerializeField
    [SerializeField] GPUParticleEmitter _particleEmitter;
    [SerializeField] List<GameObject> _parents;
    #endregion

    #region member variable
    private List<bool> m_toggles;
    #endregion

    void Start()
    {
        m_toggles = new List<bool>();
        for (int i = 0; i < _parents.Count; i++)
        {
            m_toggles.Add(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < _parents.Count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                m_toggles[i] = !m_toggles[i];
            }
            if (m_toggles[i])
            {
                _particleEmitter.Emit(_parents[i].transform.position);
            }
        }
    }
}