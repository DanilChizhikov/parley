using System;
using System.Collections.Generic;
using System.Text;

namespace DTech.Parley.Editor
{
    internal sealed class SkillConfiguration
    {
        public HashSet<string> LibraryNames { get; } = new (StringComparer.Ordinal);
        public HashSet<string> Disabled { get; } = new (StringComparer.Ordinal);
        public string LibraryRoot { get; set; }
        public string LibraryFolder { get; set; }

        public bool HasEnabledLibrarySkill
        {
            get
            {
                foreach (string name in LibraryNames)
                {
                    if (!Disabled.Contains(name))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsEnabled(string name)
        {
            return !Disabled.Contains(name);
        }

        public string Fingerprint()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(LibraryRoot).Append('|');
            AppendSorted(builder, LibraryNames);
            builder.Append('|');
            AppendSorted(builder, Disabled);
            return builder.ToString();
        }

        private static void AppendSorted(StringBuilder builder, HashSet<string> names)
        {
            List<string> sorted = new (names);
            sorted.Sort(StringComparer.Ordinal);
            foreach (string name in sorted)
            {
                builder.Append(name).Append(';');
            }
        }
    }
}
