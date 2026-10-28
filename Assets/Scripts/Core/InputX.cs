using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Ações de entrada do jogo (mesmo mapa do Godot: WASD, Shift, Espaço, Ctrl/C, E, F, R, K, Tab, X, T, H, Q, O, 0-9, Esc, mouse).
/// Usa o Input System novo quando ativo e o Input Manager antigo caso contrário.
public static class InputX
{
    public enum A { Forward, Back, Left, Right, Jump, Sprint, Crouch, Grab, Spawn, Reset, Clean, Panel, XRay, Slowmo, Help, Escape, Reload, Options, Trap, Inventory }

#if ENABLE_INPUT_SYSTEM
    static Key[][] keys =
    {
        new[] { Key.W, Key.UpArrow }, new[] { Key.S, Key.DownArrow }, new[] { Key.A, Key.LeftArrow }, new[] { Key.D, Key.RightArrow },
        new[] { Key.Space }, new[] { Key.LeftShift, Key.RightShift }, new[] { Key.LeftCtrl, Key.C }, new[] { Key.E },
        new[] { Key.F }, new[] { Key.R }, new[] { Key.K }, new[] { Key.Tab }, new[] { Key.X }, new[] { Key.T }, new[] { Key.H },
        new[] { Key.Escape }, new[] { Key.Q }, new[] { Key.O }, new[] { Key.G }, new[] { Key.I },
    };
    public static bool Held(A a)
    {
        var kb = Keyboard.current; if (kb == null) return false;
        foreach (var k in keys[(int)a]) if (kb[k].isPressed) return true;
        return false;
    }
    public static bool Down(A a)
    {
        var kb = Keyboard.current; if (kb == null) return false;
        foreach (var k in keys[(int)a]) if (kb[k].wasPressedThisFrame) return true;
        return false;
    }
    public static bool SlotDown(int n)   // 0..9
    {
        var kb = Keyboard.current; if (kb == null) return false;
        Key k = n == 0 ? Key.Digit0 : Key.Digit1 + (n - 1);
        return kb[k].wasPressedThisFrame;
    }
    public static bool Fire => Mouse.current != null && Mouse.current.leftButton.isPressed;
    public static bool FireDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    public static bool Alt => Mouse.current != null && Mouse.current.rightButton.isPressed;
    public static bool AltDown => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    public static bool AnyMouseDown => Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);
    /// deslocamento do mouse em pixels (como event.relative do Godot)
    public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    /// rodinha: +1 para cima, -1 para baixo
    public static int Wheel
    {
        get
        {
            if (Mouse.current == null) return 0;
            float y = Mouse.current.scroll.ReadValue().y;
            return y > 0.1f ? 1 : (y < -0.1f ? -1 : 0);
        }
    }
#else
    static KeyCode[][] keys =
    {
        new[] { KeyCode.W, KeyCode.UpArrow }, new[] { KeyCode.S, KeyCode.DownArrow }, new[] { KeyCode.A, KeyCode.LeftArrow }, new[] { KeyCode.D, KeyCode.RightArrow },
        new[] { KeyCode.Space }, new[] { KeyCode.LeftShift, KeyCode.RightShift }, new[] { KeyCode.LeftControl, KeyCode.C }, new[] { KeyCode.E },
        new[] { KeyCode.F }, new[] { KeyCode.R }, new[] { KeyCode.K }, new[] { KeyCode.Tab }, new[] { KeyCode.X }, new[] { KeyCode.T }, new[] { KeyCode.H },
        new[] { KeyCode.Escape }, new[] { KeyCode.Q }, new[] { KeyCode.O }, new[] { KeyCode.G }, new[] { KeyCode.I },
    };
    public static bool Held(A a) { foreach (var k in keys[(int)a]) if (Input.GetKey(k)) return true; return false; }
    public static bool Down(A a) { foreach (var k in keys[(int)a]) if (Input.GetKeyDown(k)) return true; return false; }
    public static bool SlotDown(int n) => Input.GetKeyDown(KeyCode.Alpha0 + n);
    public static bool Fire => Input.GetMouseButton(0);
    public static bool FireDown => Input.GetMouseButtonDown(0);
    public static bool Alt => Input.GetMouseButton(1);
    public static bool AltDown => Input.GetMouseButtonDown(1);
    public static bool AnyMouseDown => Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
    public static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
    public static int Wheel { get { float y = Input.mouseScrollDelta.y; return y > 0.1f ? 1 : (y < -0.1f ? -1 : 0); } }
#endif

    public static bool Captured => Cursor.lockState == CursorLockMode.Locked;
    public static void Capture(bool on)
    {
        Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !on;
    }
}
