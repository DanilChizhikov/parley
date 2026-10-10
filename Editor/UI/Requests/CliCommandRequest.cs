using System;
using System.Collections.Generic;

namespace DTech.Parley.Editor.UI
{
    internal readonly struct CliCommandRequest
    {
        public ParleyProfile Profile { get; }
        public List<string> Arguments { get; }
        public Action<string> OnLine { get; }
        public Action<int> OnExit { get; }

        public CliCommandRequest(
            ParleyProfile profile,
            List<string> arguments,
            Action<string> onLine = null,
            Action<int> onExit = null)
        {
            Profile = profile;
            Arguments = arguments;
            OnLine = onLine;
            OnExit = onExit;
        }
    }
}