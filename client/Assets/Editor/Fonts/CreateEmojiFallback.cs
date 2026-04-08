#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ShortGeta.EditorTools
{
    // 이모지 fallback SDF 를 자동 생성하여 malgun SDF 의 Fallback List 에 추가.
    //
    // 사용법:
    //   1. C:\Windows\Fonts\seguiemj.ttf (Segoe UI Emoji) 를 Assets/Fonts/ 로 복사
    //   2. 메뉴: ShortGeta → Fonts → Create Emoji Fallback (seguiemj)
    //   3. 끝. Editor Play 시 이모지 □ 가 실제 이모지로 렌더됨
    //
    // 동작:
    //   - seguiemj.ttf 를 Dynamic SDF Font Asset 으로 변환
    //   - malgun SDF 의 FallbackFontAssetTable 에 추가 (중복 방지)
    //
    // 주의: Segoe UI Emoji 는 color font 라 SDF 로는 단색 실루엣으로 표시될 수 있음.
    //        원색 이모지가 필요하면 이 스킬 밖 작업 (EmojiOne/Twemoji 스프라이트 atlas).
    public static class CreateEmojiFallback
    {
        private const string EmojiTtfPath = "Assets/Fonts/seguiemj.ttf";
        private const string MalgunSdfPath = "Assets/Fonts/malgun SDF.asset";
        private const string OutputSdfPath = "Assets/Fonts/seguiemj SDF.asset";

        [MenuItem("ShortGeta/Fonts/Create Emoji Fallback (seguiemj)")]
        public static void Create()
        {
            // 1) seguiemj.ttf 확인
            var emojiFont = AssetDatabase.LoadAssetAtPath<Font>(EmojiTtfPath);
            if (emojiFont == null)
            {
                if (EditorUtility.DisplayDialog(
                    "seguiemj.ttf 가 없어요",
                    $"{EmojiTtfPath} 에 파일이 없습니다.\n\n" +
                    "C:\\Windows\\Fonts\\seguiemj.ttf 를 Assets/Fonts/ 로 복사 후 다시 시도해주세요.\n\n" +
                    "(Windows 탐색기에서 Ctrl+C → Unity Project 창에 Ctrl+V)",
                    "탐색기 열기", "취소"))
                {
                    EditorUtility.RevealInFinder(@"C:\Windows\Fonts\seguiemj.ttf");
                }
                return;
            }

            // 2) malgun SDF 확인
            var malgun = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MalgunSdfPath);
            if (malgun == null)
            {
                EditorUtility.DisplayDialog("malgun SDF 없음",
                    $"{MalgunSdfPath} 가 없습니다. 먼저 malgun SDF 를 생성해주세요.",
                    "OK");
                return;
            }

            // 3) emoji SDF 이미 있으면 재사용, 없으면 생성
            var emojiSdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputSdfPath);
            if (emojiSdf == null)
            {
                // Dynamic SDF 생성 (샘플링 90, 패딩 9, 아틀라스 2048)
                emojiSdf = TMP_FontAsset.CreateFontAsset(
                    emojiFont,
                    samplingPointSize: 90,
                    atlasPadding: 9,
                    renderMode: UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                    atlasWidth: 2048,
                    atlasHeight: 2048,
                    atlasPopulationMode: AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);

                // 저장
                Directory.CreateDirectory(Path.GetDirectoryName(OutputSdfPath));
                AssetDatabase.CreateAsset(emojiSdf, OutputSdfPath);
                // atlas texture / material 은 subasset 으로 저장됨
                foreach (var tex in emojiSdf.atlasTextures)
                {
                    if (tex != null && !AssetDatabase.Contains(tex))
                        AssetDatabase.AddObjectToAsset(tex, emojiSdf);
                }
                if (emojiSdf.material != null && !AssetDatabase.Contains(emojiSdf.material))
                    AssetDatabase.AddObjectToAsset(emojiSdf.material, emojiSdf);

                AssetDatabase.SaveAssets();
                Debug.Log($"[EmojiFallback] created {OutputSdfPath}");
            }

            // 4) malgun SDF 의 FallbackFontAssetTable 에 추가 (중복 방지)
            if (malgun.fallbackFontAssetTable == null)
                malgun.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();

            if (!malgun.fallbackFontAssetTable.Contains(emojiSdf))
            {
                malgun.fallbackFontAssetTable.Add(emojiSdf);
                EditorUtility.SetDirty(malgun);
                AssetDatabase.SaveAssets();
                Debug.Log("[EmojiFallback] added to malgun SDF fallback list");
            }
            else
            {
                Debug.Log("[EmojiFallback] already in fallback list");
            }

            EditorUtility.DisplayDialog(
                "완료",
                "이모지 fallback 이 추가됐어요.\n\n" +
                "▶ Play 를 다시 누르면 이모지가 제대로 표시됩니다.\n\n" +
                "주의: Segoe UI Emoji 는 SDF 변환 시 단색 실루엣으로 나올 수 있어요.",
                "OK");
        }
    }
}
#endif
