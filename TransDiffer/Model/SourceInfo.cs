using System.Collections.Generic;
using System.IO;

namespace TransDiffer.Model
{
    class SourceInfo
    {
        public FileInfo File { get; set; }
        public int Line { get; set; }
        public HashSet<TranslationStringReference> Strings { get; } = new HashSet<TranslationStringReference>();
    }
}
