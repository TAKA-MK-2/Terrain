using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestEmit : MonoBehaviour
{
    [SerializeField] GPUParticleEmitter _particleEmitter;
    [SerializeField] List<Vector3> _positions = new List<Vector3>();

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 mouseposition = Input.mousePosition;
            mouseposition.z = (Vector3.zero - Camera.main.transform.position).magnitude;
            mouseposition = Camera.main.ScreenToWorldPoint(mouseposition);
            _particleEmitter.Emit(mouseposition);
        }
        for (int i = 0; i < _positions.Count; i++)
        {
            if (Input.GetKey(KeyCode.Alpha0 + (i + 1)))
            {
                _particleEmitter.Emit(_positions[i]);
            }
        }
    }
}