using DTech.Parley.Editor.Sessions;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor.UI
{
    internal sealed class AgentsGraphWindow : EditorWindow
    {
        private const string Title = "Parley Agents";
        private const float MinWidth = 520f;
        private const float MinHeight = 320f;

        [SerializeField] private ParleyWindow _owner;

        private AgentsPanel _panel;
        private ChatSession _session;

        public static void Open(ParleyWindow owner, ChatSession session)
        {
            AgentsGraphWindow window = Find(owner);
            if (window == null)
            {
                window = CreateWindow<AgentsGraphWindow>();
                window.minSize = new Vector2(MinWidth, MinHeight);
            }

            window._owner = owner;
            window.Show();
            window.Focus();
            window.Bind(session);
        }

        public static void Rebind(ParleyWindow owner, ChatSession session)
        {
            Find(owner)?.Bind(session);
        }

        private static AgentsGraphWindow Find(ParleyWindow owner)
        {
            foreach (AgentsGraphWindow window in Resources.FindObjectsOfTypeAll<AgentsGraphWindow>())
            {
                if (window._owner == owner)
                {
                    return window;
                }
            }

            return null;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(Title, "Subagents of a Parley chat");
        }

        private void CreateGUI()
        {
            ParleyStyles.Apply(rootVisualElement);
            _panel = new AgentsPanel(false, null);
            rootVisualElement.Add(_panel);
            _panel.Bind(_session);
        }

        private void Bind(ChatSession session)
        {
            _session = session;
            _panel?.Bind(session);
        }
    }
}
