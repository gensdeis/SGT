using UnityEngine;

namespace ShortGeta.Core
{
    public enum InputEventType
    {
        Down,
        Up,
        Move,
        KeyDown,
        KeyUp,
    }

    // 터치/마우스/키보드 입력 추상화 단일 구조체.
    // MinigameLauncher 가 수집해서 IMinigame.OnInput() 에 전달.
    public readonly struct InputEvent
    {
        public readonly InputEventType Type;
        public readonly Vector2 ScreenPosition; // KeyDown/KeyUp 일 때는 zero
        public readonly KeyCode KeyCode;        // 터치/마우스 일 때는 None
        public readonly float TimestampSec;     // Time.realtimeSinceStartup

        public InputEvent(InputEventType type, Vector2 pos, KeyCode key, float ts)
        {
            Type = type;
            ScreenPosition = pos;
            KeyCode = key;
            TimestampSec = ts;
        }

        public static InputEvent Touch(InputEventType type, Vector2 pos)
            => new InputEvent(type, pos, KeyCode.None, Time.realtimeSinceStartup);

        public static InputEvent Key(InputEventType type, KeyCode key)
            => new InputEvent(type, Vector2.zero, key, Time.realtimeSinceStartup);
    }
}
