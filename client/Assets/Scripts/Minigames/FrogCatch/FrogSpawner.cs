using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShortGeta.Minigames.FrogCatch
{
    // 개구리 스폰 + 라이프사이클 관리.
    // FrogCatchGame.Begin() 으로 시작, Stop() 으로 중지.
    public class FrogSpawner : MonoBehaviour
    {
        [SerializeField] private Frog frogPrefab;
        [SerializeField] private float minSpawnInterval = 0.6f;
        [SerializeField] private float maxSpawnInterval = 1.4f;
        [SerializeField] private int maxConcurrent = 5;
        [SerializeField] private Vector2 spawnArea = new Vector2(3f, 5f); // world units

        private Action _onCaught;
        private Action _onMissed;
        private float _nextSpawnAt;
        private bool _running;
        private readonly List<Frog> _alive = new List<Frog>();

        public void Begin(Action onCaught, Action onMissed)
        {
            _onCaught = onCaught;
            _onMissed = onMissed;
            _running = true;
            _nextSpawnAt = Time.realtimeSinceStartup + 0.5f;
        }

        public void Stop()
        {
            _running = false;
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] != null) Destroy(_alive[i].gameObject);
            }
            _alive.Clear();
        }

        private void Update()
        {
            if (!_running) return;
            if (Time.realtimeSinceStartup >= _nextSpawnAt && _alive.Count < maxConcurrent)
            {
                SpawnOne();
                _nextSpawnAt = Time.realtimeSinceStartup +
                    UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
            }
        }

        private void SpawnOne()
        {
            if (frogPrefab == null) return;
            var pos = new Vector3(
                UnityEngine.Random.Range(-spawnArea.x, spawnArea.x),
                UnityEngine.Random.Range(-spawnArea.y, spawnArea.y),
                0f);
            var f = Instantiate(frogPrefab, pos, Quaternion.identity, transform);
            f.OnCaught = (frog) =>
            {
                _onCaught?.Invoke();
                _alive.Remove(frog);
                Destroy(frog.gameObject);
            };
            f.OnLifetimeExpired = (frog) =>
            {
                // 자연 사라짐은 페널티 없음 (놓침은 빈 화면 탭만 -2)
                _alive.Remove(frog);
                Destroy(frog.gameObject);
            };
            _alive.Add(f);
        }

#if UNITY_EDITOR
        public int __TestAliveCount => _alive.Count;
#endif
    }
}
