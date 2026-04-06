using System;
using UnityEngine;

namespace ShortGeta.Core
{
    // XOR 난독화 정수/실수 wrapper.
    // 메모리 변조 도구(Cheat Engine 등) 가 score 변수를 평문으로 검색하지 못하도록 1차 방어.
    // BACKEND_PLAN.md §"Anti-cheat — 레이어 1" 의 클라이언트 메모리 변조 방어 항목 구현.
    [Serializable]
    public struct SafeInt
    {
        [SerializeField] private int _xorMask;
        [SerializeField] private int _stored;

        public int Value
        {
            get => _stored ^ _xorMask;
            set
            {
                _xorMask = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                _stored = value ^ _xorMask;
            }
        }

        public static SafeInt From(int v)
        {
            var s = new SafeInt();
            s.Value = v;
            return s;
        }

        public override string ToString() => Value.ToString();

        public static SafeInt operator +(SafeInt a, int b)
        {
            return From(a.Value + b);
        }
        public static SafeInt operator -(SafeInt a, int b)
        {
            return From(a.Value - b);
        }
    }

    [Serializable]
    public struct SafeFloat
    {
        [SerializeField] private int _xorMask;
        [SerializeField] private int _storedBits;

        public float Value
        {
            get
            {
                int bits = _storedBits ^ _xorMask;
                return BitConverter.Int32BitsToSingle(bits);
            }
            set
            {
                _xorMask = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                _storedBits = BitConverter.SingleToInt32Bits(value) ^ _xorMask;
            }
        }

        public static SafeFloat From(float v)
        {
            var s = new SafeFloat();
            s.Value = v;
            return s;
        }

        public override string ToString() => Value.ToString("F2");
    }
}
