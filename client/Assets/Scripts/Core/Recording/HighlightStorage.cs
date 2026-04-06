using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShortGeta.Core.Recording
{
    // 디스크 저장 헬퍼. EditMode 테스트 가능하도록 baseDir 주입.
    public class HighlightStorage
    {
        private readonly string _baseDir;

        // 기본: Application.persistentDataPath/highlights
        // 테스트: Path.GetTempPath()/.../highlights
        public HighlightStorage(string baseDir)
        {
            _baseDir = baseDir;
            Directory.CreateDirectory(_baseDir);
        }

        public string BaseDir => _baseDir;

        // 새 session 디렉토리 생성. 형식: {timestamp}_{tag}/
        public string CreateSessionDir(string tag)
        {
            string safeTag = SanitizeTag(tag);
            string ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string dir = Path.Combine(_baseDir, $"{ts}_{safeTag}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // 프레임을 frame_NN.png 로 저장. 동기 호출 (별도 thread 권장).
        public void SavePngSequence(string dir, IList<Texture2D> frames)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] == null) continue;
                byte[] png = frames[i].EncodeToPNG();
                string filename = $"frame_{i:D3}.png";
                File.WriteAllBytes(Path.Combine(dir, filename), png);
            }
        }

        // 가장 최근 session 디렉토리 (없으면 null)
        public string GetLatestSessionDir()
        {
            if (!Directory.Exists(_baseDir)) return null;
            var dirs = Directory.GetDirectories(_baseDir);
            if (dirs.Length == 0) return null;
            Array.Sort(dirs);
            return dirs[dirs.Length - 1];
        }

        // 모든 session 디렉토리 (정렬됨)
        public List<string> ListSessionDirs()
        {
            var result = new List<string>();
            if (!Directory.Exists(_baseDir)) return result;
            var dirs = Directory.GetDirectories(_baseDir);
            Array.Sort(dirs);
            result.AddRange(dirs);
            return result;
        }

        private static string SanitizeTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "untagged";
            var chars = tag.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }
    }
}
