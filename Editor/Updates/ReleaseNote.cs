using System;

namespace DTech.Parley.Editor.Updates
{
    internal readonly struct ReleaseNote
    {
        public Version Version { get; }

        public string Title { get; }

        public string Body { get; }

        public string Url { get; }

        public ReleaseNote(Version version, string title, string body, string url)
        {
            Version = version;
            Title = title;
            Body = body;
            Url = url;
        }
    }
}
