using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.FrogCatch
{
    // 개구리 잡아라 — 30초 동안 화면에 등장하는 개구리 탭.
    // SerializeField 의존 없이 Canvas UI 로 완전 자급자족 동작.
    public class FrogCatchGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "frog_catch_v1";
        public string Title => "개구리 잡아라";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 30f;
        public string[] Tags => new[] { GameTags.Reflex, GameTags.Animal, GameTags.Bmovie };

        private const int MaxScore = 1000;
        private const int CatchScore = 10;
        private const int MissPenalty = -2;
        private const int MaxFrogs = 5;

        private SafeInt _score;
        private bool _running;
        private int _difficulty;

        private float _minInterval = 0.6f;
        private float _maxInterval = 1.4f;
        private float _lifetimeSec = 1.5f;
        private float _nextSpawnAt;

        private RectTransform[] _frogRts;
        private float[] _frogBornAt;
        private bool[] _frogActive;

        private GameObject _root;
        private TextMeshProUGUI _label;

        public void SetDifficulty(int intensity)
        {
            _difficulty = Mathf.Clamp(intensity, -1, 1);
            float mult = _difficulty == -1 ? 1.5f : (_difficulty == 1 ? 0.7f : 1.0f);
            _minInterval = 0.6f * mult;
            _maxInterval = 1.4f * mult;
        }

        public void OnGameStart()
        {
            _score = SafeInt.From(0);
            _running = true;
            _nextSpawnAt = Time.realtimeSinceStartup + 0.5f;
            BuildUI();
            UpdateLabel();
        }

        public void OnGameEnd()
        {
            _running = false;
            _score.Value = Mathf.Clamp(_score.Value, 0, MaxScore);
            if (_root != null) Destroy(_root);
        }

        public int GetScore() => Mathf.Max(0, _score.Value);

        public void OnInput(InputEvent input) { /* 탭 처리는 UI Button 으로 */ }

        private void Update()
        {
            if (!_running) return;
            float now = Time.realtimeSinceStartup;

            // 수명 만료된 개구리 숨기기
            int activeCount = 0;
            for (int i = 0; i < MaxFrogs; i++)
            {
                if (_frogActive[i])
                {
                    if (now - _frogBornAt[i] > _lifetimeSec)
                    {
                        _frogActive[i] = false;
                        if (_frogRts[i] != null) _frogRts[i].gameObject.SetActive(false);
                    }
                    else
                    {
                        activeCount++;
                    }
                }
            }

            // 새 개구리 스폰
            if (now >= _nextSpawnAt && activeCount < MaxFrogs)
            {
                SpawnFrog();
                _nextSpawnAt = now + Random.Range(_minInterval, _maxInterval);
            }
        }

        private void SpawnFrog()
        {
            for (int i = 0; i < MaxFrogs; i++)
            {
                if (!_frogActive[i] && _frogRts[i] != null)
                {
                    float x = Random.Range(0.05f, 0.80f);
                    float y = Random.Range(0.12f, 0.72f);
                    _frogRts[i].anchorMin = new Vector2(x, y);
                    _frogRts[i].anchorMax = new Vector2(x + 0.15f, y + 0.12f);
                    _frogRts[i].offsetMin = Vector2.zero;
                    _frogRts[i].offsetMax = Vector2.zero;
                    _frogRts[i].gameObject.SetActive(true);
                    _frogActive[i] = true;
                    _frogBornAt[i] = Time.realtimeSinceStartup;
                    break;
                }
            }
        }

        private void OnFrogCaught(int index)
        {
            if (!_running || !_frogActive[index]) return;
            _frogActive[index] = false;
            if (_frogRts[index] != null) _frogRts[index].gameObject.SetActive(false);
            _score = _score + CatchScore;
            if (_score.Value > MaxScore) _score.Value = MaxScore;
            UpdateLabel();
        }

        private void OnMiss()
        {
            if (!_running) return;
            _score = _score + MissPenalty;
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (_label != null)
                _label.text = $"개구리 잡아라!\n점수: {Mathf.Max(0, _score.Value)}";
        }

        private void BuildUI()
        {
            _root = new GameObject("FrogCatchUI");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            // 배경 (탭 = 미스)
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(_root.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.28f, 0.10f); // 다크 그린 연못 (fallback)
            var bgSprite = ShortGeta.Core.UI.GameSpriteLoader.LoadBg(GameId);
            if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }
            var bgBtn = bgGo.AddComponent<Button>();
            bgBtn.targetGraphic = bgImg;
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(OnMiss);

            // 레이블 (상단)
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_root.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.05f, 0.85f);
            lrt.anchorMax = new Vector2(0.95f, 0.97f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 44;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;

            // 개구리 슬롯 생성
            _frogRts = new RectTransform[MaxFrogs];
            _frogBornAt = new float[MaxFrogs];
            _frogActive = new bool[MaxFrogs];
            var frogSprite = ShortGeta.Core.UI.GameSpriteLoader.LoadByGameId(GameId, "frog_idle");

            for (int i = 0; i < MaxFrogs; i++)
            {
                int idx = i;
                var frogGo = new GameObject($"Frog{i}");
                frogGo.transform.SetParent(_root.transform, false);
                var rt = frogGo.AddComponent<RectTransform>();
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var frogImg = frogGo.AddComponent<Image>();
                if (frogSprite != null)
                {
                    frogImg.sprite = frogSprite;
                    frogImg.color = Color.white;
                }
                else
                {
                    frogImg.color = new Color(0.20f, 0.75f, 0.20f);
                    frogImg.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetCircle();

                    var emojiGo = new GameObject("Emoji");
                    emojiGo.transform.SetParent(frogGo.transform, false);
                    var ert = emojiGo.AddComponent<RectTransform>();
                    ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one;
                    ert.offsetMin = Vector2.zero; ert.offsetMax = Vector2.zero;
                    var et = emojiGo.AddComponent<TextMeshProUGUI>();
                    et.text = "🐸";
                    et.fontSize = 80;
                    et.alignment = TextAlignmentOptions.Center;
                }

                var btn = frogGo.AddComponent<Button>();
                btn.targetGraphic = frogImg;
                btn.onClick.AddListener(() => OnFrogCaught(idx));

                frogGo.SetActive(false);
                _frogRts[i] = rt;
            }
        }
    }
}
