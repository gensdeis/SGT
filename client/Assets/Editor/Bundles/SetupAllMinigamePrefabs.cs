#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ShortGeta.Minigames.DarkSouls;
using ShortGeta.Minigames.FrogCatch;
using ShortGeta.Minigames.KakaoUnread;
using ShortGeta.Minigames.MathGenius;
using ShortGeta.Minigames.NoodleBoil;
using ShortGeta.Minigames.PokerFace;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ShortGeta.Editor.Bundles
{
    // 6개 미니게임 모두에 대해 prefab + Addressable 자동 셋업.
    // Iter 2C' 의 SetupFrogCatchPrefab 패턴 일반화.
    //
    // 동작:
    //   - 각 게임 type 별로 GameObject 조립 → SaveAsPrefabAsset → CreateOrMoveEntry
    //   - address: minigame/{game_id}
    //   - FrogCatch 는 spawner + frog template child 추가, 나머지는 single component
    public static class SetupAllMinigamePrefabs
    {
        private const string PrefabDir = "Assets/Minigames/Prefabs";

        // (gameId, prefabName, MonoBehaviour type, customAssemble?)
        private static readonly List<(string Id, string Name, Type CompType)> Games = new()
        {
            ("frog_catch_v1",   "FrogCatch",   typeof(FrogCatchGame)),
            ("noodle_boil_v1",  "NoodleBoil",  typeof(NoodleBoilGame)),
            ("poker_face_v1",   "PokerFace",   typeof(PokerFaceGame)),
            ("dark_souls_v1",   "DarkSouls",   typeof(DarkSoulsGame)),
            ("kakao_unread_v1", "KakaoUnread", typeof(KakaoUnreadGame)),
            ("math_genius_v1",  "MathGenius",  typeof(MathGeniusGame)),
        };

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                try { TryAutoSetupAll(); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SetupAllMinigamePrefabs] auto-setup skipped: {e.Message}");
                }
            };
        }

        [MenuItem("ShortGeta/Bundles/Setup All Minigame Prefabs")]
        public static void RunFromMenu()
        {
            TryAutoSetupAll(force: true);
        }

        private static void TryAutoSetupAll(bool force = false)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                if (force)
                {
                    Debug.LogWarning("[SetupAllMinigamePrefabs] AddressableAssetSettings 가 없음. " +
                        "Window → Asset Management → Addressables → Groups → Create.");
                }
                return;
            }

            EnsureDirectory();

            foreach (var g in Games)
            {
                string prefabPath = $"{PrefabDir}/{g.Name}.prefab";
                bool exists = File.Exists(prefabPath);
                if (exists && !force) continue;
                if (exists && force) AssetDatabase.DeleteAsset(prefabPath);

                if (g.CompType == typeof(FrogCatchGame))
                {
                    BuildFrogCatchPrefab(prefabPath);
                }
                else
                {
                    BuildSimplePrefab(prefabPath, g.Name, g.CompType);
                }

                EnsureAddressable(settings, prefabPath, $"minigame/{g.Id}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupAllMinigamePrefabs] {Games.Count} games processed");
        }

        private static void EnsureDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Minigames"))
            {
                AssetDatabase.CreateFolder("Assets", "Minigames");
            }
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                AssetDatabase.CreateFolder("Assets/Minigames", "Prefabs");
            }
        }

        private static void BuildSimplePrefab(string path, string rootName, Type compType)
        {
            var root = new GameObject(rootName);
            try
            {
                root.AddComponent(compType);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[SetupAllMinigamePrefabs] created {path}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildFrogCatchPrefab(string path)
        {
            var root = new GameObject("FrogCatchRoot");
            try
            {
                var game = root.AddComponent<FrogCatchGame>();

                var spawnerGo = new GameObject("FrogSpawner");
                spawnerGo.transform.SetParent(root.transform, false);
                var spawner = spawnerGo.AddComponent<FrogSpawner>();

                var frogGo = new GameObject("FrogTemplate");
                frogGo.transform.SetParent(root.transform, false);
                frogGo.AddComponent<SpriteRenderer>();
                var frog = frogGo.AddComponent<Frog>();
                frogGo.SetActive(false);

                var so = new SerializedObject(spawner);
                var frogProp = so.FindProperty("frogPrefab");
                if (frogProp != null)
                {
                    frogProp.objectReferenceValue = frog;
                    so.ApplyModifiedProperties();
                }

                var gameSo = new SerializedObject(game);
                var spawnerProp = gameSo.FindProperty("spawner");
                if (spawnerProp != null)
                {
                    spawnerProp.objectReferenceValue = spawner;
                    gameSo.ApplyModifiedProperties();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[SetupAllMinigamePrefabs] created {path}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsureAddressable(AddressableAssetSettings settings, string path, string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return;
            var group = settings.DefaultGroup;
            if (group == null) return;
            var entry = settings.CreateOrMoveEntry(guid, group);
            if (entry == null) return;
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
        }
    }
}
#endif
