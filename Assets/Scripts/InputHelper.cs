using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum GameKey { Num1, Num2, Num3, Num4, Num5, Num6, Pause, NextWave, Cancel, Upgrade, Sell, Speed, Restart, Target }

/// <summary>
/// Capa de entrada que funciona tanto con el Input System nuevo como con el Input Manager clásico.
/// </summary>
public static class InputHelper
{
    public static Vector2 PointerPosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }

    public static bool PrimaryDown
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }

    public static bool SecondaryDown
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
#endif
        }
    }

    public static bool KeyDown(GameKey key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (key)
        {
            case GameKey.Num1: return kb.digit1Key.wasPressedThisFrame;
            case GameKey.Num2: return kb.digit2Key.wasPressedThisFrame;
            case GameKey.Num3: return kb.digit3Key.wasPressedThisFrame;
            case GameKey.Num4: return kb.digit4Key.wasPressedThisFrame;
            case GameKey.Num5: return kb.digit5Key.wasPressedThisFrame;
            case GameKey.Num6: return kb.digit6Key.wasPressedThisFrame;

            case GameKey.Pause: return kb.pKey.wasPressedThisFrame;
            case GameKey.NextWave: return kb.spaceKey.wasPressedThisFrame;
            case GameKey.Cancel: return kb.escapeKey.wasPressedThisFrame;
            case GameKey.Upgrade: return kb.uKey.wasPressedThisFrame;
            case GameKey.Sell: return kb.xKey.wasPressedThisFrame;
            case GameKey.Speed: return kb.fKey.wasPressedThisFrame;
            case GameKey.Restart: return kb.rKey.wasPressedThisFrame;
            case GameKey.Target: return kb.tKey.wasPressedThisFrame;
        }
        return false;
#else
        switch (key)
        {
            case GameKey.Num1: return Input.GetKeyDown(KeyCode.Alpha1);
            case GameKey.Num2: return Input.GetKeyDown(KeyCode.Alpha2);
            case GameKey.Num3: return Input.GetKeyDown(KeyCode.Alpha3);
            case GameKey.Num4: return Input.GetKeyDown(KeyCode.Alpha4);
            case GameKey.Num5: return Input.GetKeyDown(KeyCode.Alpha5);
            case GameKey.Num6: return Input.GetKeyDown(KeyCode.Alpha6);

            case GameKey.Pause: return Input.GetKeyDown(KeyCode.P);
            case GameKey.NextWave: return Input.GetKeyDown(KeyCode.Space);
            case GameKey.Cancel: return Input.GetKeyDown(KeyCode.Escape);
            case GameKey.Upgrade: return Input.GetKeyDown(KeyCode.U);
            case GameKey.Sell: return Input.GetKeyDown(KeyCode.X);
            case GameKey.Speed: return Input.GetKeyDown(KeyCode.F);
            case GameKey.Restart: return Input.GetKeyDown(KeyCode.R);
            case GameKey.Target: return Input.GetKeyDown(KeyCode.T);
        }
        return false;
#endif
    }
}
