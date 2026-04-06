using System;
using UnityEngine;

namespace ShortGeta.Minigames.FrogCatch
{
    // 단일 개구리 인스턴스. 클릭 이벤트만 처리하고, 일정 시간 후 자동으로 사라짐.
    // 스폰/디스폰은 FrogSpawner 가 관리.
    [RequireComponent(typeof(SpriteRenderer))]
    public class Frog : MonoBehaviour
    {
        [SerializeField] private float lifetimeSec = 1.5f;

        private float _bornAt;
        private bool _caught;
        public Action<Frog> OnLifetimeExpired;
        public Action<Frog> OnCaught;

        private void OnEnable()
        {
            _bornAt = Time.realtimeSinceStartup;
            _caught = false;
        }

        private void Update()
        {
            if (_caught) return;
            if (Time.realtimeSinceStartup - _bornAt > lifetimeSec)
            {
                OnLifetimeExpired?.Invoke(this);
            }
        }

        private void OnMouseDown()
        {
            // Unity 의 OnMouseDown 은 collider 가 있어야 작동 (Unity 6 + URP 호환).
            if (_caught) return;
            _caught = true;
            OnCaught?.Invoke(this);
        }
    }
}
