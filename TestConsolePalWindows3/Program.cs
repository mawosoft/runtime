// Copyright (c) 2024 Matthias Wolf, Mawosoft.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
[assembly: SupportedOSPlatform("windows")]

namespace TestConsolePalWindows3;

internal sealed partial class Program
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FreeConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AllocConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr SetStdHandle(int nStdHandle, IntPtr hHandle);

    public const int STD_INPUT_HANDLE = -10;
    public const int STD_OUTPUT_HANDLE = -11;
    public const int STD_ERROR_HANDLE = -12;

    public const int INPUT_KEYBOARD = 1; // struct INPUT has KEYBDINPUT

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int type;
        public INPUTUNION union;
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mouseInput;
        [FieldOffset(0)] public KEYBDINPUT keyboardInput;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    };

    // KEYBDINPUT.dwFlags
    public const int KEYEVENTF_EXTENDEDKEY = 1;
    public const int KEYEVENTF_KEYUP = 2;
    public const int KEYEVENTF_UNICODE = 4;
    public const int KEYEVENTF_SCANCODE = 8;

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    };

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial int SendInput(int cInputs, ReadOnlySpan<INPUT> pInputs, int cbSize);


    internal static void Main()
    {
        // Allocate a new console to allow simulating console input on CI runner, but keep output redirections.
        // This must be done **before** calling the Console API because System.Console only checks redirection during init.
        IntPtr hout = GetStdHandle(STD_OUTPUT_HANDLE);
        IntPtr herr = GetStdHandle(STD_ERROR_HANDLE);
        bool bfree = FreeConsole();
        bool balloc = AllocConsole();
        SetStdHandle(STD_OUTPUT_HANDLE, hout);
        SetStdHandle(STD_ERROR_HANDLE, herr);

        Console.WriteLine($"Redirected in/out/err: {Console.IsInputRedirected}/{Console.IsOutputRedirected}/{Console.IsErrorRedirected}");
        Console.WriteLine($"Free/AllocConsole: {bfree} {balloc}");

        // Send console input
        INPUT[] inputs = new INPUT[4];
        inputs[0].type = INPUT_KEYBOARD;
        // ctrl    keydown
        inputs[0].union.keyboardInput.wVk = 0x11;
        inputs[0].union.keyboardInput.wScan = 0x1d;
        inputs[0].union.keyboardInput.dwFlags = 0;
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].union.keyboardInput.wVk = (ushort)ConsoleKey.V;
        inputs[1].union.keyboardInput.wScan = 0x2f;
        inputs[1].union.keyboardInput.dwFlags = 0;
        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].union.keyboardInput.wVk = (ushort)ConsoleKey.V;
        inputs[2].union.keyboardInput.wScan = 0x2f;
        inputs[2].union.keyboardInput.dwFlags = KEYEVENTF_KEYUP;
        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].union.keyboardInput.wVk = 0x11; 
        inputs[3].union.keyboardInput.wScan = 0x1d;
        inputs[3].union.keyboardInput.dwFlags = KEYEVENTF_KEYUP;
        // Unless there is a arguments error, SendInput always returns success.
        // Need to always check the last PInvoke error.
        int sent = SendInput(inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        int error = Marshal.GetLastPInvokeError();
        Console.WriteLine($"SendInput: sent: {sent}, last error: 0x{error:X}");

        // Wait for available console input and read it, with timeout against unexpected hijinks.
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (Console.KeyAvailable) break;
        }
        sw.Stop();
        if (sw.Elapsed < timeout)
        {
            var cki = Console.ReadKey(intercept: true);
            Console.WriteLine($"ReadKey: key: {cki.Key}, mod: {cki.Modifiers}, char: 0x{(int)cki.KeyChar:X}");

        }
        else
        {
            Console.WriteLine("KeyAvailable time out.");
        }

        // Ensure new console is visible if run locally w/o redirections.
        if (!Console.IsOutputRedirected)
        {
            Console.WriteLine("App started without redirected std handles. Press <ENTER> to close this window.");
            Console.ReadLine();
        }
    }
}
