using System.IO;
using UnityEditor;

namespace DTech.Parley.Editor.Sessions
{
    [InitializeOnLoad]
    internal static class ChatTabInstaller
    {
        private static ChatTabManager _manager;

        public static bool HasActiveWork => _manager?.HasActiveWork == true;

        public static IChatTabManager Manager
        {
            get
            {
                if (_manager == null)
                {
                    _manager = new ChatTabManager(new SessionFactory(),
                        new ChatTabStore(Path.Combine(ProjectPaths.LibraryFolder, "open-tabs.json")));
                    _manager.OnChanged += WorkChangedHandler;
                    ParleyUserSettings settings = ParleyUserSettings.instance;
                    _manager.Restore(settings.Find, settings.ActiveProfile, SessionStore.Load(settings.LastSessionId));
                }

                return _manager;
            }
        }

        static ChatTabInstaller()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ShutdownHandler;
            EditorApplication.quitting += ShutdownHandler;
            EditorApplication.playModeStateChanged += PlayModeChangedHandler;
            EditorApplication.update += UpdateHandler;
        }

        public static ChatTabManager CreatePreview()
        {
            return new ChatTabManager(new SessionFactory(true), new PreviewStore());
        }

        private static void UpdateHandler()
        {
            if (_manager != null)
            {
                foreach (ChatSession session in _manager.Tabs)
                {
                    session.RefreshExecutionState();
                }
            }

            WorkChangedHandler();
        }

        private static void WorkChangedHandler()
        {
            if (_manager?.HasActiveWork == true)
            {
                ReloadGuard.Acquire(_manager);
            }
            else if (_manager != null)
            {
                ReloadGuard.Release(_manager);
            }
        }

        private static void ShutdownHandler()
        {
            _manager?.Dispose();
            _manager = null;
        }

        private static void PlayModeChangedHandler(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && _manager?.HasActiveWork == true)
            {
                EditorApplication.isPlaying = false;
                UnityEngine.Debug.LogWarning("[Parley] Finish or stop all active chats before entering Play Mode.");
            }
        }

        private sealed class SessionFactory : IChatSessionFactory
        {
            private readonly bool _preview;

            public SessionFactory(bool preview = false)
            {
                _preview = preview;
            }

            public ChatSession Create(ParleyProfile profile, SessionRecord record)
            {
                return new ChatSession(profile, record, _preview);
            }
        }

        private sealed class PreviewStore : IChatTabStore
        {
            public ChatTabState Load() => null;
            public void Save(ChatTabState state) { }
        }
    }
}
