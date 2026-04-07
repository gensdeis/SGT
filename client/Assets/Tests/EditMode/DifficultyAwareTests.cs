using NUnit.Framework;
using ShortGeta.Core;
using ShortGeta.Minigames.DarkSouls;
using ShortGeta.Minigames.FrogCatch;
using ShortGeta.Minigames.KakaoUnread;
using ShortGeta.Minigames.MathGenius;
using ShortGeta.Minigames.NoodleBoil;
using ShortGeta.Minigames.PokerFace;
using UnityEngine;

namespace ShortGeta.Tests.EditMode
{
    // 6개 미니게임이 IDifficultyAware 를 구현하고, ±1 + 클램프가 예외 없이 적용되는지 검증.
    public class DifficultyAwareTests
    {
        private static T NewGame<T>() where T : MonoBehaviour
        {
            var go = new GameObject("test_" + typeof(T).Name);
            return go.AddComponent<T>();
        }

        private static void TearDown(MonoBehaviour mb)
        {
            if (mb != null) Object.DestroyImmediate(mb.gameObject);
        }

        private static void Verify<T>() where T : MonoBehaviour
        {
            var g = NewGame<T>();
            try
            {
                Assert.IsInstanceOf<IDifficultyAware>(g, $"{typeof(T).Name} must implement IDifficultyAware");
                var d = (IDifficultyAware)g;
                Assert.DoesNotThrow(() => d.SetDifficulty(-1));
                Assert.DoesNotThrow(() => d.SetDifficulty(0));
                Assert.DoesNotThrow(() => d.SetDifficulty(1));
                // 클램프 검증 (±5 → ±1)
                Assert.DoesNotThrow(() => d.SetDifficulty(5));
                Assert.DoesNotThrow(() => d.SetDifficulty(-5));
            }
            finally
            {
                TearDown(g);
            }
        }

        [Test] public void FrogCatch_Implements() => Verify<FrogCatchGame>();
        [Test] public void NoodleBoil_Implements() => Verify<NoodleBoilGame>();
        [Test] public void PokerFace_Implements() => Verify<PokerFaceGame>();
        [Test] public void DarkSouls_Implements() => Verify<DarkSoulsGame>();
        [Test] public void KakaoUnread_Implements() => Verify<KakaoUnreadGame>();
        [Test] public void MathGenius_Implements() => Verify<MathGeniusGame>();
    }
}
