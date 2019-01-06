using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using TransDiffer.Parser.Structure;

namespace TransDiffer.Preview
{
    // DLGTEMPLATEEX
    class DialogTemplateEx
    {
        internal WindowStylesEx exStyle;
        internal WindowStyles style;
        internal sz_Or_Ord menu = null;
        internal sz_Or_Ord windowClass = null;
        internal string title;

        internal Font mFont;

        internal short x, y, cx, cy;

        internal List<DialogItemTemplateEx> controls = new List<DialogItemTemplateEx>();

        internal byte[] CreateTemplate()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write((ushort)1);        // dlgVer
            bw.Write((ushort)0xffff);   // signature
            bw.Write((uint)0);          // helpID;
            bw.Write((uint)exStyle);
            bw.Write((uint)style);
            bw.Write((ushort)controls.Count);  // cDlgItems
            bw.Write(x);
            bw.Write(y);
            bw.Write(cx);
            bw.Write(cy);
            Debug.Assert(menu == null);
            Write(bw, menu);            // menu, 0x0000 = no menu, 0xffff means followed by ord, otherwise, string
            Debug.Assert(windowClass == null);
            Write(bw, windowClass);           // windowClass, 0x0000 = Default class, 0xffff means followed by ord, otherwise, string
            WriteString(bw, title);

            if ((style & WindowStyles.DS_SETFONT) != 0)
            {
                if (mFont == null)
                {
                    mFont = new Font() { Name = "Segoe UI", Size = 12 };
                }
                bw.Write((ushort)mFont.Size);
                bw.Write((ushort)0);        // weight
                bw.Write((byte)0);          // italic
                bw.Write((byte)1);          // charset = DEFAULT_CHARSET
                WriteString(bw, (mFont.Name)); // typeface
            }
            DWordAlign(bw);

            foreach (var control in controls)
            {
                control.WriteToStream(bw);
            }

            return ms.ToArray();
        }

        internal static void Write(BinaryWriter bw, sz_Or_Ord sz_or_ord)
        {
            if (sz_or_ord == null)
            {
                bw.Write((ushort)0);
                return;
            }

            if (sz_or_ord.String != null)
            {
                WriteString(bw, sz_or_ord.String);
            }
            else
            {
                bw.Write((ushort)0xffff);
                bw.Write(sz_or_ord.Ordinal);
            }
        }

        internal static void DWordAlign(BinaryWriter ms)
        {
            long pos = ms.BaseStream.Position;
            long advance = pos % 4;
            if (advance == 0)
                return;

            byte[] dum = new byte[4];
            ms.Write(dum, 0, 4 - (int)advance);
        }

        internal static void WriteString(BinaryWriter bw, string str)
        {
            if (str != null)
            {
                byte[] data = Encoding.Unicode.GetBytes(str);
                bw.Write(data);
            }
            bw.Write((ushort)0);    // null terminator or empty string
        }
    }
}
