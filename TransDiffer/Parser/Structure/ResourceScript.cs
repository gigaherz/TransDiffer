using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransDiffer.Parser.Structure
{
    public class ResourceScript
    {
        public List<ResourceStatement> Definition { get; } = new List<ResourceStatement>();
    }
}
