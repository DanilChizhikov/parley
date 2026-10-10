using System;
using System.Collections.Generic;
using System.IO;

namespace DTech.Parley.Editor.Skills
{
    internal static class SkillScanner
    {
        private static readonly string[] SkillFolders = { ".claude/skills", ".agents/skills" };

        public static List<SkillInfo> Scan(SkillConfiguration configuration)
        {
            List<SkillInfo> skills = new ();
            if (!string.IsNullOrEmpty(configuration.LibraryFolder))
            {
                AddFolder(skills, configuration.LibraryFolder, true);
            }

            foreach (string folder in SkillFolders)
            {
                AddFolder(skills, Path.Combine(ProjectPaths.Root, folder), false);
            }

            foreach (string folder in SkillFolders)
            {
                AddFolder(skills, Path.Combine(ProjectPaths.HomeFolder, folder), false);
            }

            skills.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return skills;
        }

        public static string ReadBody(SkillInfo skill)
        {
            return SkillMarkdown.Parse(File.ReadAllText(skill.Path)).Body;
        }

        private static bool Contains(List<SkillInfo> skills, string name)
        {
            foreach (SkillInfo skill in skills)
            {
                if (skill.Name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddFolder(List<SkillInfo> skills, string folder, bool isLibrary)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(folder);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return;
            }

            foreach (string directory in directories)
            {
                string name = Path.GetFileName(directory);
                string file = Path.Combine(directory, SkillLibrary.FileName);
                if (!File.Exists(file) || Contains(skills, name))
                {
                    continue;
                }

                SkillDocument document;
                try
                {
                    document = SkillMarkdown.Parse(File.ReadAllText(file));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    continue;
                }

                skills.Add(new SkillInfo
                {
                    Name = name,
                    Description = document.Description,
                    ArgumentHint = document.ArgumentHint,
                    Path = file,
                    IsLibrary = isLibrary,
                });
            }
        }
    }
}
