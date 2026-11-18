using System;
using System.Collections.Generic;
using System.IO;

namespace DTech.Parley.Editor.Skills
{
    internal static class SkillScanner
    {
        private static readonly string[] SkillFolders = { ".claude/skills", ".agents/skills" };

        public static List<SkillDocument> ScanDocuments(string folder, List<string> diagnostics)
        {
            List<SkillDocument> documents = new();
            if (string.IsNullOrWhiteSpace(folder)) return documents;
            try
            {
                if (!Directory.Exists(folder))
                {
                    diagnostics.Add(folder + ": directory does not exist.");
                    return documents;
                }
                ScanDirectory(folder, documents, diagnostics);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            {
                diagnostics.Add(folder + ": " + exception.Message);
            }
            return documents;
        }

        private static void ScanDirectory(string folder, List<SkillDocument> documents, List<string> diagnostics)
        {
            string[] files = Directory.GetFiles(folder, "*.md");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                try
                {
                    SkillDocument document = SkillMarkdown.Parse(File.ReadAllText(file));
                    document.FolderPath = Path.GetDirectoryName(file);
                    documents.Add(document);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    diagnostics.Add(file + ": " + exception.Message);
                }
            }
            string[] directories = Directory.GetDirectories(folder);
            Array.Sort(directories, StringComparer.Ordinal);
            foreach (string directory in directories)
            {
                if (Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal)
                    || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                try { ScanDirectory(directory, documents, diagnostics); }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    diagnostics.Add(directory + ": " + exception.Message);
                }
            }
        }

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
