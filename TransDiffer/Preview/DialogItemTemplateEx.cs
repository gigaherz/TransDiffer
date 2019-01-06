using System.IO;

namespace TransDiffer.Preview
{
    // DLGITEMTEMPLATEEX
    class DialogItemTemplateEx
    {
        internal WindowStylesEx exStyle;
        internal WindowStyles style;

        internal short x, y, cx, cy;

        internal uint id;
        internal sz_Or_Ord windowClass;
        internal sz_Or_Ord title;

        internal void WriteToStream(BinaryWriter bw)
        {
            DialogTemplateEx.DWordAlign(bw);

            bw.Write((uint)0);          // helpID;
            bw.Write((uint)exStyle);
            bw.Write((uint)style);
            bw.Write(x);
            bw.Write(y);
            bw.Write(cx);
            bw.Write(cy);
            bw.Write(id);
            DialogTemplateEx.Write(bw, windowClass);
            DialogTemplateEx.Write(bw, title);
            bw.Write((ushort)0);        // extraCount
        }
    }
}
