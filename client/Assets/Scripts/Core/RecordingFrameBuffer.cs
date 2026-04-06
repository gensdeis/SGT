using System.Collections.Generic;
using UnityEngine;

namespace ShortGeta.Core
{
    // 순환 프레임 버퍼. capacity 만큼만 보관, 넘치면 가장 오래된 것을 drop.
    //
    // 사용:
    //   var buf = new RecordingFrameBuffer(30); // 3s × 10fps
    //   buf.Push(tex2D);
    //   var frames = buf.Snapshot(); // 시간순 (가장 오래된 → 최신)
    //
    // EditMode 테스트 가능 — UnityEngine 의존은 Texture2D 만.
    public class RecordingFrameBuffer
    {
        private readonly int _capacity;
        private readonly Texture2D[] _frames;
        private int _head;   // 다음에 쓸 위치
        private int _count;

        public int Capacity => _capacity;
        public int Count => _count;

        public RecordingFrameBuffer(int capacity)
        {
            if (capacity <= 0) throw new System.ArgumentException("capacity > 0");
            _capacity = capacity;
            _frames = new Texture2D[capacity];
        }

        public void Push(Texture2D frame)
        {
            // 기존 슬롯을 덮어쓰는 경우 텍스처 destroy
            if (_frames[_head] != null)
            {
                Object.DestroyImmediate(_frames[_head]);
            }
            _frames[_head] = frame;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;
        }

        // 시간순 snapshot. 가장 오래된 → 최신 순.
        // 반환된 Texture2D 들은 buffer 가 소유. 호출자가 destroy 하지 말 것.
        public List<Texture2D> Snapshot()
        {
            var list = new List<Texture2D>(_count);
            int start;
            if (_count < _capacity)
            {
                // 아직 한 바퀴 안 돌았음 — 0 부터 _head-1 까지가 시간순
                start = 0;
            }
            else
            {
                // 한 바퀴 돈 후 — _head 가 가장 오래된 것
                start = _head;
            }
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _capacity;
                if (_frames[idx] != null) list.Add(_frames[idx]);
            }
            return list;
        }

        // 모든 프레임 destroy + count reset.
        public void Clear()
        {
            for (int i = 0; i < _capacity; i++)
            {
                if (_frames[i] != null)
                {
                    Object.DestroyImmediate(_frames[i]);
                    _frames[i] = null;
                }
            }
            _head = 0;
            _count = 0;
        }
    }
}
