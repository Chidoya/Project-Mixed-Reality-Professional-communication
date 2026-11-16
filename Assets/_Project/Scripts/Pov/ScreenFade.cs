using System.Collections;
using UnityEngine;

namespace ProCommVR.Pov
{
    /// <summary>
    /// Fades the view to black and back, with a small black quad just in front of the camera (works in VR, one eye or
    /// both). The renderer is only on while the fade is not fully transparent. Put it on a quad that is a child of the
    /// camera, with a transparent unlit material.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class ScreenFade : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer _renderer;
        private MaterialPropertyBlock _block;

        /// <summary>
        /// 0 = clear, 1 = black.
        /// </summary>
        public float Alpha { get; private set; }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            SetAlpha(0f);
        }

        public IEnumerator FadeTo(float alpha, float seconds)
        {
            var from = Alpha;
            for (var t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, alpha, t / seconds));
                yield return null;
            }
            SetAlpha(alpha);
        }

        public void SetAlpha(float alpha)
        {
            if (_renderer == null)
                Awake();
            Alpha = Mathf.Clamp01(alpha);
            _block.SetColor(BaseColorId, new Color(0f, 0f, 0f, Alpha));
            _renderer.SetPropertyBlock(_block);
            _renderer.enabled = Alpha > 0.001f;
        }
    }
}
