using System.Collections.Generic;
using System.Diagnostics;

namespace TransDiffer.Parser
{
    public static class ClassNames
    {
        static readonly Dictionary<string, string> lookup = new Dictionary<string, string>
        {
            { "PROGRESS_CLASSA", "msctls_progress32" },
            { "PROGRESS_CLASSW", "msctls_progress32" },
            { "PROGRESS_CLASS", "msctls_progress32" },

            { "UPDOWN_CLASSA", "msctls_updown32" },
            { "UPDOWN_CLASSW", "msctls_updown32" },
            { "UPDOWN_CLASS", "msctls_updown32" },

            { "ANIMATE_CLASSA", "SysAnimate32" },
            { "ANIMATE_CLASSW", "SysAnimate32" },
            { "ANIMATE_CLASS", "SysAnimate32" },

            { "WC_TREEVIEWA", "SysTreeView32" },
            { "WC_TREEVIEWW", "SysTreeView32" },
            { "WC_TREEVIEW", "SysTreeView32" },
        };

        public static string Translate(string ident)
        {
            if (lookup.TryGetValue(ident, out var result))
                return result;

            Debug.WriteLine($"Unknown identifier: {ident}");
            return "STATIC";
        }
    }
}
