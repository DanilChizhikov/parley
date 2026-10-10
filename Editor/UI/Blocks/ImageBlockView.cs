using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ImageBlockView : BlockView
    {
        private const float MaxImageSize = 220.0f;

        private readonly Image _image;

        private Texture2D _texture;

        public ImageBlockView(TranscriptBlock block) : base(block)
        {
            AddToClassList("pl-block--image");
            _image = new Image { scaleMode = ScaleMode.ScaleToFit };
            _image.AddToClassList("pl-image");
            Add(_image);
            Add(ParleyStyles.Text(block.Text, ParleyStyles.Muted));
            RegisterCallback<AttachToPanelEvent>(AttachedHandler);
            RegisterCallback<DetachFromPanelEvent>(DetachedHandler);
        }

        public override void Refresh()
        {
        }

        private void LoadTexture()
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(Block.Data ?? string.Empty);
            }
            catch (FormatException exception)
            {
                Debug.LogWarning("[Parley] Could not decode an image attachment: " + exception.Message);
                ParleyStyles.SetVisible(_image, false);
                return;
            }

            _texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            if (!_texture.LoadImage(bytes))
            {
                ParleyStyles.SetVisible(_image, false);
                return;
            }

            _image.image = _texture;
            float aspect = _texture.height > 0 ? (float)_texture.width / _texture.height : 1.0f;
            float height = Mathf.Min(MaxImageSize, _texture.height);
            _image.style.height = height;
            _image.style.width = height * aspect;
            ParleyStyles.SetVisible(_image, true);
        }

        private void AttachedHandler(AttachToPanelEvent evt)
        {
            if (_texture == null)
            {
                LoadTexture();
            }
        }

        private void DetachedHandler(DetachFromPanelEvent evt)
        {
            if (_texture == null)
            {
                return;
            }

            _image.image = null;
            Object.DestroyImmediate(_texture);
            _texture = null;
        }
    }
}