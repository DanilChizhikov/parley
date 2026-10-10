using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditorInternal;

namespace DTech.Parley.Editor.Skills
{
    internal static class SkillLibrary
    {
        public const string FileName = "SKILL.md";

        private const int MaxNameLength = 64;

        private static readonly UTF8Encoding Utf8 = new (false);

        public static string Root => Path.Combine(InternalEditorUtility.unityPreferencesFolder, "DTech", "Parley", "Skills");

        public static string Folder => Path.Combine(Root, ".claude", "skills");

        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength || name[0] == '-')
            {
                return false;
            }

            foreach (char character in name)
            {
                if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
                {
                    return false;
                }
            }

            return true;
        }

        public static List<SkillDocument> List()
        {
            List<SkillDocument> documents = new ();
            if (!Directory.Exists(Folder))
            {
                return documents;
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(Folder);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return documents;
            }

            foreach (string directory in directories)
            {
                SkillDocument document = IsValidName(Path.GetFileName(directory)) ? Read(directory) : null;
                if (document != null)
                {
                    documents.Add(document);
                }
            }

            documents.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return documents;
        }

        public static List<string> Names()
        {
            List<string> names = new ();
            foreach (SkillDocument document in List())
            {
                names.Add(document.Name);
            }

            return names;
        }

        public static SkillDocument Find(string name)
        {
            return IsValidName(name) ? Read(Path.Combine(Folder, name)) : null;
        }

        public static void Save(SkillDocument document, string previousName)
        {
            if (!IsValidName(document.Name))
            {
                throw new ArgumentException("Invalid skill name: " + document.Name);
            }

            string target = Path.Combine(Folder, document.Name);
            if (!string.IsNullOrEmpty(previousName) && previousName != document.Name)
            {
                string source = Path.Combine(Folder, previousName);
                if (Directory.Exists(target))
                {
                    throw new IOException("A skill named '" + document.Name + "' already exists.");
                }

                if (Directory.Exists(source))
                {
                    Directory.Move(source, target);
                }
            }

            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, FileName), SkillMarkdown.Format(document), Utf8);
            document.FolderPath = target;
        }

        public static void Delete(string name)
        {
            string directory = IsValidName(name) ? Path.Combine(Folder, name) : null;
            if (directory != null && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        public static string Import(string sourceFolder, out string error)
        {
            error = null;
            string source = string.IsNullOrEmpty(sourceFolder) ? null : Path.GetFullPath(sourceFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string file = source == null ? null : Path.Combine(source, FileName);
            if (file == null || !File.Exists(file))
            {
                error = "The folder has no " + FileName + ".";
                return null;
            }

            if (ProjectPaths.IsInside(source, Folder) || ProjectPaths.IsInside(Folder, source))
            {
                error = "The folder is inside or contains the Parley skill library.";
                return null;
            }

            SkillDocument document = SkillMarkdown.Parse(File.ReadAllText(file));
            string name = IsValidName(document.Name) ? document.Name : Path.GetFileName(source).ToLowerInvariant();
            if (!IsValidName(name))
            {
                error = "'" + name + "' is not a valid skill name: use lowercase letters, digits and hyphens.";
                return null;
            }

            string target = Path.Combine(Folder, name);
            if (Directory.Exists(target))
            {
                error = "A skill named '" + name + "' already exists in the library.";
                return null;
            }

            string staging = Path.Combine(Folder, ".import-" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyDirectory(source, staging);
                if (document.Name != name)
                {
                    document.Name = name;
                    File.WriteAllText(Path.Combine(staging, FileName), SkillMarkdown.Format(document), Utf8);
                }

                Directory.Move(staging, target);
            }
            catch (Exception)
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, true);
                }

                throw;
            }

            return name;
        }

        private static SkillDocument Read(string directory)
        {
            string file = Path.Combine(directory, FileName);
            if (!File.Exists(file))
            {
                return null;
            }

            SkillDocument document;
            try
            {
                document = SkillMarkdown.Parse(File.ReadAllText(file));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return null;
            }

            document.Name = Path.GetFileName(directory);
            document.FolderPath = directory;
            return document;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }

            foreach (string directory in Directory.GetDirectories(source))
            {
                string name = Path.GetFileName(directory);
                if (name != ".git" && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                {
                    CopyDirectory(directory, Path.Combine(target, name));
                }
            }
        }
    }
}
