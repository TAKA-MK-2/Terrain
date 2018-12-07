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
        foreach (Vector3 position in _positions)
        {
            _particleEmitter.Emit(position);
        }
    }
}