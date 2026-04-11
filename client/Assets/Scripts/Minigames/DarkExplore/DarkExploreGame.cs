using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.DarkExplore
{
    // 어두운 탐험 — 터치로 이동, 원형 시야, 아이템 수집. 45초.
    public class DarkExploreGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "dark_explore_v1";
        public string Title => "어두운 탐험";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 45f;
        public string[] Tags => new[] { GameTags.Focus, GameTags.Fantasy };

        private const int MaxScore = 300;
        private const int ItemScore = 30;
        private const int ItemCount = 10;
        private SafeInt _score;
        private bool _running;
        private int _difficulty;
        private float _playerX = 0.5f, _playerY = 0.5f;

        private GameObject _root;
        private TextMeshProUGUI _label;
        private RectTransform _playerRt;
        private RectTransform[] _itemRts;
        private bool[] _collected;

        public void SetDifficulty(int i) => _difficulty = Mathf.Clamp(i, -1, 1);

        public void OnGameStart()
        {
            _score = SafeInt.From(0); _running = true;
            BuildUI();
            SpawnItems();
        }

        public void OnGameEnd()
        {
            _running = false; _score.Value = Mathf.Clamp(_score.Value, 0, MaxScore);
            if (_root != null) Destroy(_root);
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input)
        {
            if (!_running || input.Type != InputEventType.Down) return;
            _playerX = input.ScreenPosition.x / Screen.width;
            _playerY = input.ScreenPosition.y / Screen.height;
        }

        private void Update()
        {
            if (!_running) return;
            if (_playerRt != null)
            {
                _playerRt.anchorMin = new Vector2(_playerX - 0.04f, _playerY - 0.03f);
                _playerRt.anchorMax = new Vector2(_playerX + 0.04f, _playerY + 0.03f);
            }

            // 아이템 수집 판정
            if (_itemRts != null)
            {
                for (int i = 0; i < ItemCount; i++)
                {
                    if (_collected[i] || _itemRts[i] == null) continue;
                    float ix = (_itemRts[i].anchorMin.x + _itemRts[i].anchorMax.x) / 2f;
                    float iy = (_itemRts[i].anchorMin.y + _itemRts[i].anchorMax.y) / 2f;
                    float dist = Mathf.Sqrt((_playerX - ix) * (_playerX - ix) + (_playerY - iy) * (_playerY - iy));
                    if (dist < 0.08f)
                    {
                        _collected[i] = true;
                        _itemRts[i].gameObject.SetActive(false);
                        _score = _score + ItemScore;
                    }
                }
            }

            if (_label != null) _label.text = $"어두운 탐험\n아이템: {_score.Value / ItemScore}/{ItemCount}\n\n터치하면 이동!";
        }

        private void SpawnItems()
        {
            _itemRts = new RectTransform[ItemCount];
            _collected = new bool[ItemCount];
            for (int i = 0; i < ItemCount; i++)
            {
                var item = new GameObject($"Item{i}");
                item.transform.SetParent(_root.transform, false);
                var rt = item.AddComponent<RectTransform>();
                float x = Random.Range(0.1f, 0.9f), y = Random.Range(0.15f, 0.8f);
                rt.anchorMin = new Vector2(x - 0.02f, y - 0.015f);
                rt.anchorMax = new Vector2(x + 0.02f, y + 0.015f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var img = item.AddComponent<Image>();
                img.color = new Color(1f, 0.9f, 0.2f); // 빛나는 노랑
                img.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetCircle();
                _itemRts[i] = rt;
            }
        }

        private void BuildUI()
        {
            _root = new GameObject("DarkExploreUI"); _root.transform.SetParent(transform, false);
            var c = _root.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 100;
            var s = _root.AddComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(720, 1280); s.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            // 어두운 배경
            var bg = new GameObject("Bg"); bg.transform.SetParent(_root.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.05f); // 거의 검정

            // 플레이어 (밝은 원)
            var player = new GameObject("Player"); player.transform.SetParent(_root.transform, false);
            _playerRt = player.AddComponent<RectTransform>();
            _playerRt.offsetMin = Vector2.zero; _playerRt.offsetMax = Vector2.zero;
            var pImg = player.AddComponent<Image>();
            pImg.color = new Color(0.5f, 0.8f, 1f, 0.7f); // 랜턴 빛
            pImg.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetCircle();

            var lg = new GameObject("Label"); lg.transform.SetParent(_root.transform, false);
            var lr = lg.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.05f, 0.88f); lr.anchorMax = new Vector2(0.95f, 0.98f);
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            _label = lg.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 36; _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(0.7f, 0.7f, 0.8f);
        }
    }
}
