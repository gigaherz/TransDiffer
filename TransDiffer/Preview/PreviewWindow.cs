using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using TransDiffer.Parser;
using TransDiffer.Parser.Structure;

namespace TransDiffer.Preview
{
    internal class PreviewWindow
    {
        public event EventHandler Closed;
        private readonly DialogTemplateEx mTemplate;
        private IntPtr mWnd;
        private readonly Win32.DialogProcDelegate mDelegate;

        private static string Cleanup(string input)
        {
            input = input ?? string.Empty;
            if (input.StartsWith("\"") && input.EndsWith("\""))
                return input.Substring(1, input.Length - 2);
            return input;
        }

        //https://stackoverflow.com/a/16736914/4928207
        private static class EnumConverter<TEnum> where TEnum : struct, IConvertible
        {
            public static readonly Func<uint, TEnum> ConvertToEnum = GenerateConverter1();
            public static readonly Func<TEnum, uint> ConvertFromEnum = GenerateConverter2();

            private static Func<uint, TEnum> GenerateConverter1()
            {
                var parameter = Expression.Parameter(typeof(uint));
                var dynamicMethod = Expression.Lambda<Func<uint, TEnum>>(
                    Expression.Convert(parameter, typeof(TEnum)),
                    parameter);
                return dynamicMethod.Compile();
            }

            private static Func<TEnum, uint> GenerateConverter2()
            {
                var parameter = Expression.Parameter(typeof(TEnum));
                var dynamicMethod = Expression.Lambda<Func<TEnum, uint>>(
                    Expression.Convert(parameter, typeof(uint)),
                    parameter);
                return dynamicMethod.Compile();
            }
        }

        private static uint ReadValue<T>(Token tok) where T : struct, IConvertible
        {
            if (tok.Name == Tokens.Ident)
            {
                T style;
                if (Enum.TryParse(tok.Text, out style))
                {
                    return EnumConverter<T>.ConvertFromEnum(style);
                }

                Debug.WriteLine($"Unknown style: {tok.Text}");
                return 0;
            }

            if (tok.Name == Tokens.Integer)
            {
                uint style;
                if (uint.TryParse(tok.Text, out style))
                {
                    return style;
                }
                Debug.WriteLine($"Unable to parse: {tok.Text}");
                return 0;
            }

            Debug.Assert(false);
            return 0;
        }

        private static void ToStyle<T>(ExpressionValue expression, ref T controlStyle) where T : struct, IConvertible
        {
            if (expression == null)
                return;
            var tokens = expression.Tokens.Where(tok => tok.Name != Tokens.Pipe).ToArray();
            Debug.Assert(tokens.All(tok => tok.Name == Tokens.Ident || tok.Name == Tokens.Integer || tok.Name == Tokens.Not));
            uint value = EnumConverter<T>.ConvertFromEnum(controlStyle);

            for (uint n = 0; n < tokens.Length;)
            {
                var tok = tokens[n];
                if (tok.Name == Tokens.Not)
                {
                    Debug.Assert(n + 1 < tokens.Length);
                    uint val = ReadValue<T>(tokens[n + 1]);
                    if (val != 0)
                    {
                        value &= ~val;
                    }
                    n += 2;
                }
                else
                {
                    value |= ReadValue<T>(tokens[n]);
                    n++;
                }
            }

            controlStyle = EnumConverter<T>.ConvertToEnum(value);
        }

        static readonly sz_Or_Ord g_Button = new sz_Or_Ord(0x80);
        static readonly sz_Or_Ord g_Edit = new sz_Or_Ord(0x81);
        static readonly sz_Or_Ord g_Static = new sz_Or_Ord(0x82);
        static readonly sz_Or_Ord g_Listbox = new sz_Or_Ord(0x83);
        static readonly sz_Or_Ord g_ScrollBar = new sz_Or_Ord(0x84);
        static readonly sz_Or_Ord g_ComboBox = new sz_Or_Ord(0x85);

        private static sz_Or_Ord ControlTypeToClass(Tokens type, string typeName, ref WindowStyles style)
        {
            switch (type)
            {
                case Tokens.DefPushButton:
                case Tokens.PushButton:
                    return g_Button;
                case Tokens.EditText:
                    return g_Edit;
                //case Tokens.Static:
                //    return g_Static;
                case Tokens.ListBox:
                    return g_Listbox;
                //case Tokens.ScrollBar:
                //    return g_ScrollBar;
                case Tokens.ComboBox:
                    return g_ComboBox;
                case Tokens.Control:
                    return new sz_Or_Ord(typeName);

                case Tokens.LText:
                case Tokens.RText:
                case Tokens.CText:
                    style |= WindowStyles.WS_GROUP;
                    if (type == Tokens.LText)
                        style |= WindowStyles.SS_LEFT;
                    else if (type == Tokens.RText)
                        style |= WindowStyles.SS_RIGHT;
                    else if (type == Tokens.CText)
                        style |= WindowStyles.SS_CENTER;
                    return g_Static;

                case Tokens.Icon:
                    style |= WindowStyles.SS_ICON;
                    return g_Static;

                case Tokens.GroupBox:
                    style |= WindowStyles.BS_GROUPBOX;
                    return g_Static;

                case Tokens.CheckBox:
                    style |= WindowStyles.WS_TABSTOP | WindowStyles.BS_CHECKBOX;
                    return g_Button;
                case Tokens.AutoCheckBox:
                    style |= WindowStyles.WS_TABSTOP | WindowStyles.BS_AUTOCHECKBOX;
                    return g_Button;

                case Tokens.RadioButton:
                    style |= WindowStyles.WS_TABSTOP | WindowStyles.BS_RADIOBUTTON;
                    return g_Button;
                case Tokens.AutoRadioButton:
                    style |= WindowStyles.WS_TABSTOP | WindowStyles.BS_AUTORADIOBUTTON;
                    return g_Button;

                default:
                    Debugger.Break();
                    return null;
            }
        }

        public PreviewWindow(DialogDefinition dlg)
        {
            mDelegate = DialogProc;

            mTemplate = new DialogTemplateEx();
            ToStyle(dlg.ExStyle, ref mTemplate.exStyle);
            ToStyle(dlg.Style, ref mTemplate.style);
            // Ensure that we still show child windows, and do not center them
            mTemplate.style &= ~(WindowStyles.WS_CHILD | WindowStyles.DS_CENTER | WindowStyles.WS_DISABLED);
            // Enforce visibility
            mTemplate.style |= WindowStyles.WS_VISIBLE;
            mTemplate.style |= WindowStyles.DS_NOFAILCREATE;    // HACK :)

            mTemplate.x = (short)dlg.Dimensions.Left;
            mTemplate.y = (short)dlg.Dimensions.Top;
            mTemplate.cx = (short)dlg.Dimensions.Width;
            mTemplate.cy = (short)dlg.Dimensions.Height;
            mTemplate.menu = null;
            mTemplate.windowClass = null;
            mTemplate.title = Cleanup(dlg.TextValue.Text);
            if (dlg.Font != null)
            {
                mTemplate.mFont = new Font { Name = Cleanup(dlg.Font.Name), Size = dlg.Font.Size };
            }

            uint id = 1;

            foreach (var dlgCtrl in dlg.Entries)
            {
                var ctrl = new DialogItemTemplateEx
                {
                    exStyle = 0,
                    style = 0,
                    x = (short) dlgCtrl.Dimensions.Left,
                    y = (short) dlgCtrl.Dimensions.Top,
                    cx = (short) dlgCtrl.Dimensions.Width,
                    cy = (short) dlgCtrl.Dimensions.Height,
                    id = id++
                };
                ctrl.windowClass = ControlTypeToClass(dlgCtrl.EntryType.Name, Cleanup(dlgCtrl.GenericControlType), ref ctrl.style);

                ToStyle(dlgCtrl.Style, ref ctrl.style);
                ctrl.style |= WindowStyles.WS_CHILD | WindowStyles.WS_VISIBLE;
                ctrl.style &= ~WindowStyles.WS_POPUP;

                if (dlgCtrl.TextValue != null)
                {
                    // FIXME: Ordinal for icon etc!
                    ctrl.title = new sz_Or_Ord(Cleanup(dlgCtrl.TextValue?.Text));
                }
                else
                {
                    ctrl.title = null;
                }

                mTemplate.controls.Add(ctrl);
            }
        }

        public void Show()
        {
            if (mWnd != IntPtr.Zero)
                return;

            byte[] data = mTemplate.CreateTemplate();
            IntPtr hInstance = Win32.GetModuleHandle(null);
            IntPtr wnd = Win32.CreateDialogIndirectParamW(hInstance, data, IntPtr.Zero, mDelegate, IntPtr.Zero);
            int error = Marshal.GetLastWin32Error();
            if (wnd != IntPtr.Zero)
            {
                mWnd = wnd;
            }
            else
            {
                string errorMessage = new System.ComponentModel.Win32Exception(error).Message;
                Debug.WriteLine($"Window creation failed: {error}: {errorMessage}");
            }
        }

        public void Close()
        {
            if (mWnd != IntPtr.Zero)
            {
                Win32.DestroyWindow(mWnd);
            }
        }

        private IntPtr DialogProc(IntPtr hwndDlg, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            if (uMsg == Win32.WM_DESTROY)
            {
                mWnd = IntPtr.Zero;
                Closed?.Invoke(this, new EventArgs());
            }
            else if (uMsg == Win32.WM_INITDIALOG)
            {
                return new IntPtr(1);
            }
            return IntPtr.Zero;
        }
    }
}
