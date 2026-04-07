#if UNITY_EDITOR
using System.IO;
using ShortGeta.Minigames.FrogCatch;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ShortGeta.Editor.Bundles
{
    // Editor-only 자동 셋업.
    // - import 시 1회 (InitializeOnLoadMethod) 자동 실행
    // - 메뉴 ShortGeta → Bundles → Setup FrogCatch Prefab 수동 재실행
    //
    // 동작:
    //   1. Assets/Minigames/Prefabs/ 디렉토리 준비
    //   2. FrogCatch.prefab 이 없으면 새로 생성:
    //      Root("FrogCatchRoot") + FrogCatchGame
    //        ├─ FrogSpawner (with serialized frogPrefab)
    //        └─ Frog template (SetActive=false)
    //   3. AddressableAssetSettings 에 entry 추가, address = "minigame/frog_catch_v1"
    public static class SetupFrogCatchPrefab
    {
        private const string PrefabDir = "Assets/Minigames/Prefabs";
        private const string PrefabPath = PrefabDir + "/FrogCatch.prefab";
        private const string Address = "minigame/frog_catch_v1";

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            // Defer: AssetDatabase / Addressables 가 import 중일 수 있어 다음 frame 으로 미룸
            EditorApplication.delayCall += () =>
            {
                try { TryAutoSetup(); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SetupFrogCatchPrefab] auto-setup skipped: {e.Message}");
                }
            };
        }

        [MenuItem("ShortGeta/Bundles/Setup FrogCatch Prefab")]
        public static void RunFromMenu()
        {
            TryAutoSetup(force: true);
        }

        private static void TryAutoSetup(bool force = false)
        {
            // Addressables 미설치 시 settings == null
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                if (force)
                {
                    Debug.LogWarning("[SetupFrogCatchPrefab] AddressableAssetSettings 가 없음. " +
                        "Window → Asset Management → Addressables → Groups 에서 'Create Addressables Settings'.");
                }
                return;
            }

            // 1. 디렉토리
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Minigames"))
                {
                    AssetDatabase.CreateFolder("Assets", "Minigames");
                }
                AssetDatabase.CreateFolder("Assets/Minigames", "Prefabs");
            }

            // 2. prefab 존재 확인
            bool exists = File.Exists(PrefabPath);
            if (exists && !force)
            {
                EnsureAddressable(settings);
                return;
            }

            if (exists && force)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            // 3. prefab 생성
            var root = new GameObject("FrogCatchRoot");
            try
            {
                // FrogCatchGame component
                var game = root.AddComponent<FrogCatchGame>();

                // FrogSpawner child
                var spawnerGo = new GameObject("FrogSpawner");
                spawnerGo.transform.SetParent(root.transform, false);
                var spawner = spawnerGo.AddComponent<FrogSpawner>();

                // Frog template child (sphere primitive 대신 빈 GO + SpriteRenderer 사용)
                var frogGo = new GameObject("FrogTemplate");
                frogGo.transform.SetParent(root.transform, false);
                frogGo.AddComponent<SpriteRenderer>();
                var frog = frogGo.AddComponent<Frog>();
                frogGo.SetActive(false);

                // FrogSpawner.frogPrefab 직렬화 할당
                var so = new SerializedObject(spawner);
                var frogPrefabProp = so.FindProperty("frogPrefab");
                if (frogPrefabProp != null)
                {
                    frogPrefabProp.objectReferenceValue = frog;
                    so.ApplyModifiedProperties();
                }
                else
                {
                    Debug.LogWarning("[SetupFrogCatchPrefab] FrogSpawner.frogPrefab field not found via SerializedProperty");
                }

                // FrogCatchGame.spawner 직렬화 할당
                var gameSo = new SerializedObject(game);
                var spawnerProp = gameSo.FindProperty("spawner");
                if (spawnerProp != null)
                {
                    spawnerProp.objectReferenceValue = spawner;
                    gameSo.ApplyModifiedProperties();
                }

                // prefab 저장
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null)
                {
                    Debug.LogError("[SetupFrogCatchPrefab] SaveAsPrefabAsset returned null");
                    return;
                }
                Debug.Log($"[SetupFrogCatchPrefab] created {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            EnsureAddressable(settings);
        }

        private static void EnsureAddressable(AddressableAssetSettings settings)
        {
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"[SetupFrogCatchPrefab] no GUID for {PrefabPath}");
                return;
            }

            var group = settings.DefaultGroup;
            if (group == null)
            {
                Debug.LogWarning("[SetupFrogCatchPrefab] no DefaultGroup; skipping");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            if (entry == null)
            {
                Debug.LogWarning("[SetupFrogCatchPrefab] CreateOrMoveEntry returned null");
                return;
            }
            entry.address = Address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupFrogCatchPrefab] addressable entry: {Address} → {PrefabPath}");
        }
    }
}
#endif
