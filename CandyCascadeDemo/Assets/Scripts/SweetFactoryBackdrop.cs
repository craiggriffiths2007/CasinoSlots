using UnityEngine;
using UnityEngine.UI;

namespace CandyCascade
{
    public static class SweetFactoryBackdrop
    {
        public static void Create(RectTransform parent)
        {
            var texture = Resources.Load<Texture2D>("Art/SweetFactory");
            if (texture == null)
            {
                Debug.LogError("Sweet factory background is missing from Resources/Art/SweetFactory.");
                return;
            }
            var container = new GameObject("Sweet factory background", typeof(RectTransform));
            var rect = container.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            Stretch(rect);
            var art = new GameObject("Factory artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
            art.transform.SetParent(rect, false);
            var image = art.GetComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
            var fitter = art.GetComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)texture.width / texture.height;

            // A continuous soft shade keeps text readable without rectangular UI panels.
            var shade = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            shade.name = "Factory readability gradient"; shade.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float u = x / 127f, v = y / 127f;
                float sides = Mathf.Pow(Mathf.Abs(u - .5f) * 2, .75f);
                float top = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1, v));
                float bottom = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .24f, v));
                float alpha = Mathf.Clamp01(.20f + .38f * sides + .21f * top + .30f * bottom);
                shade.SetPixel(x, y, new Color(.025f, .01f, .045f, alpha));
            }
            shade.Apply(false, true);
            var veil = new GameObject("Soft readability shade", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            veil.transform.SetParent(rect, false); Stretch(veil.GetComponent<RectTransform>());
            var overlay = veil.GetComponent<RawImage>(); overlay.texture = shade; overlay.raycastTarget = false;
        }
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
