using System;
using System.Runtime.InteropServices;

namespace TransDiffer.Preview
{
    public static class Win32
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate IntPtr DialogProcDelegate(IntPtr hwndDlg, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateDialogIndirectParamW(IntPtr hInstance, byte[] lpTemplate, IntPtr hWndParent, DialogProcDelegate lpDialogFunc, IntPtr dwInitParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hwnd);

        public static uint WM_DESTROY = 2;
        public static uint WM_INITDIALOG = 272;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
