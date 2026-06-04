using System.Runtime.InteropServices;

namespace InputBridge;

public sealed class InputSimulator
{
    private const uint INPUT_KEYBOARD = 1;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_V = 0x56;
    private const ushort VK_BACK = 0x08;
    private const ushort VK_RETURN = 0x0D;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private readonly SynchronizationContext _uiContext;
    private readonly object _sync = new();

    public bool IsTyping { get; private set; }

    public InputSimulator(SynchronizationContext uiContext)
    {
        _uiContext = uiContext;
    }

    public void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        lock (_sync)
        {
            IsTyping = true;
            try
            {
                if (!SendUnicodeText(text))
                {
                    PasteWithClipboardRestore(text);
                }
            }
            finally
            {
                IsTyping = false;
            }
        }
    }

    private static bool SendUnicodeText(string text)
    {
        foreach (var ch in text)
        {
            if (!SendUnicodeChar(ch, false) || !SendUnicodeChar(ch, true))
            {
                return false;
            }

            Thread.Sleep(1);
        }

        return true;
    }

    private static bool SendUnicodeChar(char ch, bool up)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = ch,
                    dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0)
                }
            }
        };

        return SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()) == 1;
    }

    private void PasteWithClipboardRestore(string text)
    {
        IDataObject? previous = null;

        InvokeOnUi(() =>
        {
            try { previous = Clipboard.GetDataObject(); }
            catch { previous = null; }

            Clipboard.SetText(text);
        });

        Thread.Sleep(60);
        if (!SendKey(VK_CONTROL, false)
            || !SendKey(VK_V, false)
            || !SendKey(VK_V, true)
            || !SendKey(VK_CONTROL, true))
        {
            InvokeOnUi(() => SendKeys.SendWait("^v"));
        }
        Thread.Sleep(160);

        InvokeOnUi(() =>
        {
            try
            {
                if (previous != null)
                {
                    Clipboard.SetDataObject(previous, true);
                }
            }
            catch
            {
            }
        });
    }

    public void SendBackspaces(int count, int limit)
    {
        var safeCount = Math.Min(Math.Max(count, 0), Math.Max(limit, 0));
        if (safeCount == 0)
        {
            return;
        }

        lock (_sync)
        {
            IsTyping = true;
            try
            {
                for (var i = 0; i < safeCount; i++)
                {
                    if (!SendKey(VK_BACK, false) || !SendKey(VK_BACK, true))
                    {
                        InvokeOnUi(() => SendKeys.SendWait("{BACKSPACE}"));
                    }
                    Thread.Sleep(5);
                }
            }
            finally
            {
                IsTyping = false;
            }
        }
    }

    public void SendEnters(int count)
    {
        if (count <= 0)
        {
            return;
        }

        lock (_sync)
        {
            IsTyping = true;
            try
            {
                for (var i = 0; i < count; i++)
                {
                    if (!SendKey(VK_RETURN, false) || !SendKey(VK_RETURN, true))
                    {
                        InvokeOnUi(() => SendKeys.SendWait("{ENTER}"));
                    }

                    Thread.Sleep(5);
                }
            }
            finally
            {
                IsTyping = false;
            }
        }
    }

    public void SendSoftEnters(int count)
    {
        if (count <= 0)
        {
            return;
        }

        lock (_sync)
        {
            IsTyping = true;
            try
            {
                for (var i = 0; i < count; i++)
                {
                    if (!SendKey(VK_SHIFT, false)
                        || !SendKey(VK_RETURN, false)
                        || !SendKey(VK_RETURN, true)
                        || !SendKey(VK_SHIFT, true))
                    {
                        InvokeOnUi(() => SendKeys.SendWait("+{ENTER}"));
                    }

                    Thread.Sleep(5);
                }
            }
            finally
            {
                IsTyping = false;
            }
        }
    }

    private void InvokeOnUi(Action action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        Exception? exception = null;
        using var done = new ManualResetEventSlim();
        _uiContext.Post(_ =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
            finally { done.Set(); }
        }, null);
        done.Wait();
        if (exception != null)
        {
            throw exception;
        }
    }

    private static bool SendKey(ushort key, bool up)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = key,
                    dwFlags = up ? KEYEVENTF_KEYUP : 0
                }
            }
        };

        return SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()) == 1;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}

