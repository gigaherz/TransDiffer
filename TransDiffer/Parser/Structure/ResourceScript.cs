using System.Collections.Generic;

namespace TransDiffer.Parser.Structure
{
    public class ResourceScript
    {
        public List<ResourceStatement> Definition { get; } = new List<ResourceStatement>();
    }
}
