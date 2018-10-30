using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestEmit : MonoBehaviour
{

    #region SerializeField
    [SerializeField] GPUParticleEmitter _particleEmitter;
    #endregion

	// Update is called once per frame
	void Update ()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _particleEmitter.Emit();
        }
	}
}
