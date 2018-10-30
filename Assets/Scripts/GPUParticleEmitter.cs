using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUParticleEmitter : MonoBehaviour
{
    #region SerializeField
    [SerializeField] List<GameObject> _particleSystems;
    #endregion

    void OnValidate()
    {
        foreach (GameObject particleSystem in _particleSystems)
        {
            if (!particleSystem.CompareTag("GPUParticleSystem"))
            {
                _particleSystems.Remove(particleSystem);
            }
        }
    }

    // 座標とパーティクルシステムの番号指定のエミット
    public void Emit(Vector3 _position, int _particleSystemIndex)
    {
        _particleSystems[_particleSystemIndex].GetComponent<GPUParticleManager>().EmitParticle(_position);
    }

    // 座標指定のエミット
    public void Emit(Vector3 _position)
    {
        foreach (GameObject particleSystem in _particleSystems)
        {
            particleSystem.GetComponent<GPUParticleManager>().EmitParticle(_position);
        }
    }

    // パーティクルシステムの番号指定のエミット
    public void Emit(int _particleSystemIndex)
    {
        _particleSystems[_particleSystemIndex].GetComponent<GPUParticleManager>().EmitParticle(transform.position);
    }

    // 指定なしのエミット
    public void Emit()
    {
        foreach (GameObject particleSystem in _particleSystems)
        {
            particleSystem.GetComponent<GPUParticleManager>().EmitParticle(transform.position);
        }
    }
}
