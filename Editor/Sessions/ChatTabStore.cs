using System;
using System.IO;
using Newtonsoft.Json;

namespace DTech.Parley.Editor.Sessions
{
    internal sealed class ChatTabStore : IChatTabStore
    {
        private readonly string _path;

        public ChatTabStore(string path)
        {
            _path = path;
        }

        public ChatTabState Load()
        {
            try
            {
                return File.Exists(_path) ? JsonConvert.DeserializeObject<ChatTabState>(File.ReadAllText(_path)) : null;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[Parley] Could not restore open tabs: " + exception.Message);
                return null;
            }
        }

        public void Save(ChatTabState state)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string temporary = _path + ".tmp";
                File.WriteAllText(temporary, JsonConvert.SerializeObject(state));
                if (File.Exists(_path))
                {
                    File.Replace(temporary, _path, null);
                }
                else
                {
                    File.Move(temporary, _path);
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[Parley] Could not save open tabs: " + exception.Message);
            }
        }
    }
}
