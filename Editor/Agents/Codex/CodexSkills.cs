using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexSkills
    {
        public const string ListMethod = "skills/list";
        public const string ExtraRootsMethod = "skills/extraRoots/set";
        public const string ChangedNotification = "skills/changed";

        private const string ConfigKey = "skills.config";

        public static JObject ListParameters()
        {
            return new JObject { ["cwds"] = new JArray(ProjectPaths.Root), ["forceReload"] = true };
        }

        public static JObject ExtraRootsParameters(SkillConfiguration configuration)
        {
            return new JObject { ["extraRoots"] = new JArray(configuration.LibraryFolder) };
        }

        public static void AddConfig(JObject config, SkillConfiguration configuration)
        {
            if (configuration.Disabled.Count == 0)
            {
                return;
            }

            List<string> names = new (configuration.Disabled);
            names.Sort(StringComparer.Ordinal);
            JArray entries = new JArray();
            foreach (string name in names)
            {
                entries.Add(new JObject { ["name"] = name, ["enabled"] = false });
            }

            config[ConfigKey] = entries;
        }

        public static List<SkillInfo> Parse(JToken result, SkillConfiguration configuration)
        {
            List<SkillInfo> skills = new ();
            if (!(result?["data"] is JArray entries))
            {
                return skills;
            }

            HashSet<string> seen = new (StringComparer.Ordinal);
            foreach (JToken entry in entries)
            {
                if (!(entry["skills"] is JArray list))
                {
                    continue;
                }

                foreach (JToken skill in list)
                {
                    string name = (string)skill["name"];
                    if (string.IsNullOrEmpty(name) || (bool?)skill["enabled"] == false || !seen.Add(name))
                    {
                        continue;
                    }

                    string path = (string)skill["path"];
                    skills.Add(new SkillInfo
                    {
                        Name = name,
                        Description = (string)skill["shortDescription"] ?? (string)skill["description"],
                        Path = path,
                        IsLibrary = !string.IsNullOrEmpty(configuration.LibraryFolder) && !string.IsNullOrEmpty(path) && ProjectPaths.IsInside(path, configuration.LibraryFolder),
                    });
                }
            }

            skills.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return skills;
        }
    }
}
