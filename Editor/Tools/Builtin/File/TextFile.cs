using System.IO;
using System.Text;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal static class TextFile
    {
        private const int BomLength = 3;

        private static readonly UTF8Encoding StrictUtf8 = new (false, true);

        public static bool TryRead(string path, out TextFileContent content)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bool hasBom = bytes.Length >= BomLength && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            int offset = hasBom ? BomLength : 0;
            try
            {
                content = new TextFileContent(StrictUtf8.GetString(bytes, offset, bytes.Length - offset), hasBom);
                return true;
            }
            catch (DecoderFallbackException)
            {
                content = default;
                return false;
            }
        }

        public static void Write(string path, string text, bool hasBom)
        {
            File.WriteAllText(path, text, new UTF8Encoding(hasBom));
        }
    }
}
