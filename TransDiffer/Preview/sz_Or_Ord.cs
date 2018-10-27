namespace TransDiffer.Preview
{
    class sz_Or_Ord
    {
        internal ushort Ordinal { get; private set; }
        internal string String { get; private set; }

        internal sz_Or_Ord(string str)
        {
            Ordinal = 0;
            String = str;
        }

        internal sz_Or_Ord(ushort ord)
        {
            Ordinal = ord;
            String = null;
        }
    }
}
