using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

/// <summary>
/// Interop con la API de Windows. Todo el P/Invoke del proyecto vive aca y nada
/// mas, asi el resto del codigo no tiene declaraciones nativas desparramadas.
/// </summary>
internal static class Win32
{
    // --- estilos de ventana ---
    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;

    internal const uint WS_POPUP = 0x80000000;
    internal const uint WS_VISIBLE = 0x10000000;

    internal const uint WS_EX_LAYERED = 0x00080000;
    internal const uint WS_EX_TRANSPARENT = 0x00000020; // el mouse pasa de largo
    internal const uint WS_EX_TOOLWINDOW = 0x00000080;  // no sale en alt-tab

    // --- SetWindowPos ---
    internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    internal static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_FRAMECHANGED = 0x0020;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    // --- appbar: la barra de tareas ---
    internal const uint ABM_GETSTATE = 0x0004;
    internal const uint ABM_GETTASKBARPOS = 0x0005;
    internal const int ABS_AUTOHIDE = 0x0001;

    internal const uint ABE_LEFT = 0;
    internal const uint ABE_TOP = 1;
    internal const uint ABE_RIGHT = 2;
    internal const uint ABE_BOTTOM = 3;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left, top, right, bottom;
        public int Width => right - left;
        public int Height => bottom - top;
    }

    // --- DWM: alpha por pixel ---
    internal const uint DWM_BB_ENABLE = 0x01;
    internal const uint DWM_BB_BLURREGION = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    internal struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fEnable;
        public IntPtr hRgnBlur;
        [MarshalAs(UnmanagedType.Bool)] public bool fTransitionOnMaximized;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    internal const uint LWA_COLORKEY = 0x00000001;
    internal const uint LWA_ALPHA = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey,
        byte bAlpha, uint dwFlags);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND blurBehind);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteObject(IntPtr hObject);

    [DllImport("shell32.dll", SetLastError = true)]
    internal static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    // --- cursor y mouse globales ---
    // Se leen por Win32 y no por el Input System a proposito: cuando la ventana
    // es click-through no recibe mensajes de mouse, asi que Unity deja de ver el
    // cursor justo cuando mas lo necesitamos — es la pescadilla que se muerde la
    // cola de este patron. GetCursorPos y GetAsyncKeyState son globales y andan
    // aunque la ventana no tenga foco ni reciba input.

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x, y;
    }

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    internal const int VK_LBUTTON = 0x01;

    /// <summary>Si el boton izquierdo del mouse esta apretado ahora mismo.</summary>
    internal static bool IsLeftMouseDown()
    {
        return (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
    }

    internal const int SM_CXSCREEN = 0;
    internal const int SM_CYSCREEN = 1;

    // --- encontrar nuestra propia ventana ---

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);

    /// <summary>
    /// Devuelve el handle de la ventana del player. Busca por process id y clase
    /// en vez de usar GetActiveWindow(): esa devuelve lo que este enfocado, que en
    /// el arranque no siempre somos nosotros.
    /// Devuelve IntPtr.Zero si no la encuentra (por ejemplo, corriendo en el Editor).
    /// </summary>
    internal static IntPtr FindPlayerWindow()
    {
        uint ownPid = (uint)Process.GetCurrentProcess().Id;
        IntPtr found = IntPtr.Zero;
        var className = new StringBuilder(128);

        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out uint pid);
            if (pid != ownPid) return true;

            className.Length = 0;
            GetClassName(handle, className, className.Capacity);
            if (className.ToString() != "UnityWndClass") return true;

            found = handle;
            return false; // encontrada, cortamos
        }, IntPtr.Zero);

        return found;
    }

    /// <summary>
    /// Rectangulo de la barra de tareas principal y sobre que borde de la pantalla
    /// esta apoyada. En false si el shell no contesta.
    /// Ojo: con varios monitores, ABM_GETTASKBARPOS devuelve la del monitor primario.
    /// </summary>
    internal static bool TryGetTaskbar(out RECT rect, out uint edge, out bool autoHide)
    {
        var data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)) };

        if (SHAppBarMessage(ABM_GETTASKBARPOS, ref data) == IntPtr.Zero)
        {
            rect = default;
            edge = ABE_BOTTOM;
            autoHide = false;
            return false;
        }

        rect = data.rc;
        edge = data.uEdge;

        var stateData = new APPBARDATA { cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)) };
        autoHide = (SHAppBarMessage(ABM_GETSTATE, ref stateData).ToInt64() & ABS_AUTOHIDE) != 0;
        return true;
    }

    // --- Gestión de Monitores ---
    public struct MonitorArea
    {
        public int Index;
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    internal static List<MonitorArea> GetMonitors()
    {
        List<MonitorArea> list = new List<MonitorArea>();
        int idx = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                list.Add(new MonitorArea
            {
                Index = idx++,
                X = mi.rcWork.left,
                Y = mi.rcWork.top,
                Width = mi.rcWork.right - mi.rcWork.left,
                Height = mi.rcWork.bottom - mi.rcWork.top
            });
            }
            return true;
        }, IntPtr.Zero);

        return list;
    }
}


