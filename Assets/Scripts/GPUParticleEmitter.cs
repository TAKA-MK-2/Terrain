using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUParticleEmitter : MonoBehaviour
{
    [SerializeField] List<GPUParticleManager> _particleSystems;

    public void Emit(Vector3 _position, int _particleSystemIndex)
    {
        _particleSystems[_particleSystemIndex].EmitParticle(_position);
    }

    public void Emit(Vector3 _position)
    {
        foreach (GPUParticleManager particleSystem in _particleSystems)
        {
            particleSystem.EmitParticle(_position);
        }
    }

    public void Emit(int _particleSystemIndex)
    {
        _particleSystems[_particleSystemIndex].EmitParticle(transform.position);
    }

    public void Emit()
    {
        foreach (GPUParticleManager particleSystem in _particleSystems)
        {
            particleSystem.EmitParticle(transform.position);
        }
    }
}
