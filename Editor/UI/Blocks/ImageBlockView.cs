using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ImageBlockView : BlockView
    {
        private const float MaxImageSize = 220.0f;

        private Texture2D _texture;

        public ImageBlockView(TranscriptBlock block) : base(block)
        {
            AddToClassList("pl-block--image");
            Image image = CreateImage(block.Data);
            if (image != null)
            {
                Add(image);
            }

            Add(ParleyStyles.Text(block.Text, ParleyStyles.Muted));
            RegisterCallback<DetachFromPanelEvent>(DetachedHandler);
        }

        public override void Refresh()
        {
        }

        private Image CreateImage(string base64)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64 ?? string.Empty);
            }
            catch (FormatException exception)
            {
                Debug.LogWarning("[Parley] Could not decode an image attachment: " + exception.Message);
                return null;
            }

            _texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            if (!_texture.LoadImage(bytes))
            {
                return null;
            }

            Image image = new Image { image = _texture, scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("pl-image");
            float aspect = _texture.height > 0 ? (float)_texture.width / _texture.height : 1.0f;
            float height = Mathf.Min(MaxImageSize, _texture.height);
            image.style.height = height;
            image.style.width = height * aspect;
            return image;
        }

        private void DetachedHandler(DetachFromPanelEvent evt)
        {
            if (_texture == null)
            {
                return;
            }

            Object.DestroyImmediate(_texture);
            _texture = null;
        }
    }
}