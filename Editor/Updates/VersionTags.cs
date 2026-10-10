using System;

namespace DTech.Parley.Editor.Updates
{
    internal static class VersionTags
    {
        public static Version Parse(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            string value = tag.Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(1);
            }

            if (!Version.TryParse(value, out Version version))
            {
                return null;
            }

            return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        }

        public static Version Max(Version current, Version candidate)
        {
            if (candidate == null)
            {
                return current;
            }

            return current == null || candidate > current ? candidate : current;
        }
    }
}
