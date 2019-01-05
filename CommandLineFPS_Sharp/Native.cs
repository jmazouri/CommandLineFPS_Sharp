using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public enum Keys : int
{
    W = 0x57,
    A = 0x41,
    S = 0x53,
    D = 0x44
}

public static class Native
{
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(Keys vKey);

    /// <summary>
    /// A wrapper around GetAsyncKeyState that simply returns true instead of a weird short
    /// </summary>
    /// <param name="vKey"></param>
    /// <returns></returns>
    public static bool ManagedGetKeyState(Keys vKey)
    {
        var x = GetAsyncKeyState(vKey);
        return (x == 1) || (x == short.MinValue);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool WriteConsoleOutputCharacter(IntPtr hConsoleOutput, char[] lpCharacter, uint nLength, COORD dwWriteCoord, out uint lpNumberOfCharsWritten);

    [DllImport("kernel32.dll")]
    public static extern bool SetConsoleActiveScreenBuffer(IntPtr hConsoleOutput);

    [DllImport("kernel32.dll")]
    public static extern IntPtr CreateConsoleScreenBuffer(UInt32 dwDesiredAccess, UInt32 dwShareMode, IntPtr secutiryAttributes, UInt32 flags, IntPtr screenBufferData);

    [StructLayout(LayoutKind.Sequential)]
    public struct COORD
    {
        public short X;
        public short Y;

        public COORD(short X, short Y)
        {
            this.X = X;
            this.Y = Y;
        }
    };
}