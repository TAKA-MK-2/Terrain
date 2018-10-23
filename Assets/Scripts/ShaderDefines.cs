using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Utility
{
    /// <summary>
    /// シェーダー定義
    /// </summary>
    public static class ShaderDefines
    {
        public enum TextureID
        {
            _MainTex,
            _MainTex2,

            _length
        }

        public enum IntID
        {
            // GPUParticle関係
            _emitNum,

            _length,
        }

        public enum BoolID
        {
            _length,
        }

        public enum FloatID
        {
            _deltaTime,
            _elapsedTime,

            _length,
        }

        public enum ColorID
        {
            _color,

            _length
        }

        public enum VectorID
        {
            _rotationOffsetAxis,
            
            _length
        }

        public enum BufferID
        {
            // GPUParticle関係
            _particles,
            _particlePool,
            _deadList,
            _activeList,
            _emitList,

            _length
        }

        private static readonly int[] _TextureIDs;
        private static readonly int[] _intIDs;
        private static readonly int[] _floatIDs;
        private static readonly int[] _colorIDs;
        private static readonly int[] _vectorIDs;
        private static readonly int[] _boolIDs;
        private static readonly int[] _bufferIDs;

        static ShaderDefines()
        {
            _TextureIDs = new int[(int)TextureID._length];
            for (int i = 0; i < (int)TextureID._length; i++)
            {
                _TextureIDs[i] = Shader.PropertyToID(((TextureID)i).ToString());
            }

            _intIDs = new int[(int)IntID._length];
            for (int i = 0; i < (int)IntID._length; i++)
            {
                _intIDs[i] = Shader.PropertyToID(((IntID)i).ToString());
            }

            _floatIDs = new int[(int)FloatID._length];
            for (int i = 0; i < (int)FloatID._length; i++)
            {
                _floatIDs[i] = Shader.PropertyToID(((FloatID)i).ToString());
            }

            _colorIDs = new int[(int)ColorID._length];
            for (int i = 0; i < (int)ColorID._length; i++)
            {
                _colorIDs[i] = Shader.PropertyToID(((ColorID)i).ToString());
            }

            _vectorIDs = new int[(int)VectorID._length];
            for (int i = 0; i < (int)VectorID._length; i++)
            {
                _vectorIDs[i] = Shader.PropertyToID(((VectorID)i).ToString());
            }

            _boolIDs = new int[(int)BoolID._length];
            for (int i = 0; i < (int)BoolID._length; i++)
            {
                _boolIDs[i] = Shader.PropertyToID(((BoolID)i).ToString());
            }

            _bufferIDs = new int[(int)BufferID._length];
            for (int i = 0; i < (int)BufferID._length; i++)
            {
                _bufferIDs[i] = Shader.PropertyToID(((BufferID)i).ToString());
            }
        }

        public static void SetGlobalTexture(TextureID id, Texture value)
        {
            Shader.SetGlobalTexture(_TextureIDs[(int)id], value);
        }

        public static void SetGlobalInt(IntID id, int value)
        {
            Shader.SetGlobalInt(_intIDs[(int)id], value);
        }

        public static void SetGlobalFloat(FloatID id, float value)
        {
            Shader.SetGlobalFloat(_floatIDs[(int)id], value);
        }

        public static void SetGlobalColor(ColorID id, Color value)
        {
            Shader.SetGlobalColor(_colorIDs[(int)id], value);
        }

        public static void SetGlobalBool(BoolID id, bool value)
        {
            Shader.SetGlobalInt(_boolIDs[(int)id], value ? 1 : 0);
        }

        public static void SetGlobalVector(VectorID id, Vector4 value)
        {
            Shader.SetGlobalVector(_vectorIDs[(int)id], value);
        }

        public static void SetGlobalBuffer(BufferID id, ComputeBuffer value)
        {
            Shader.SetGlobalBuffer(_bufferIDs[(int)id], value);
        }

        public static int GetTexturePropertyID(TextureID id)
        {
            return _TextureIDs[(int)id];
        }

        public static int GetIntPropertyID(IntID id)
        {
            return _intIDs[(int)id];
        }

        public static int GetFloatPropertyID(FloatID id)
        {
            return _floatIDs[(int)id];
        }

        public static int GetColorPropertyID(ColorID id)
        {
            return _colorIDs[(int)id];
        }

        public static int GetBoolPropertyID(BoolID id)
        {
            return _boolIDs[(int)id];
        }

        public static int GetVectorPropertyID(VectorID id)
        {
            return _vectorIDs[(int)id];
        }

        public static int GetBufferPropertyID(BufferID id)
        {
            return _bufferIDs[(int)id];
        }
    }
}