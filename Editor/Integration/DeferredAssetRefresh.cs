using System;

namespace DTech.Parley.Editor
{
    internal sealed class DeferredAssetRefresh
    {
        private readonly Func<bool> _canRefresh;
        private readonly Action _refresh;
        private bool _pending;

        public DeferredAssetRefresh(Func<bool> canRefresh, Action refresh)
        {
            _canRefresh = canRefresh;
            _refresh = refresh;
        }

        public void Request()
        {
            _pending = true;
        }

        public void Tick()
        {
            if (!_pending || !_canRefresh())
            {
                return;
            }

            _pending = false;
            _refresh();
        }
    }
}
