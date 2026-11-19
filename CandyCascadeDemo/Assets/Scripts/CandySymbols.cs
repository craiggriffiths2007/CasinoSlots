using UnityEngine;

namespace CandyCascade
{
    public static class CandySymbols
    {
        // Atlas coordinates measured from the top-left; tight sprite rectangles
        // keep transparent atlas padding out of the visible symbol size.
        static readonly Rect[] Regions = {
            new Rect(70, 45, 310, 370), new Rect(485, 72, 360, 320),
            new Rect(898, 50, 421, 345), new Rect(1355, 67, 377, 336),
            new Rect(44, 480, 350, 350), new Rect(486, 475, 355, 348),
            new Rect(917, 465, 387, 373)
        };
        public static Sprite[] Load()
        {
            var texture = Resources.Load<Texture2D>("Art/SweetSymbols");
            if (!texture) throw new System.InvalidOperationException("Missing sweet symbol atlas.");
            var sprites = new Sprite[7];
            float sx = texture.width / 1774f, sy = texture.height / 887f;
            for (int i = 0; i < sprites.Length; i++)
            {
                Rect r = Regions[i];
                var crop = new Rect(r.x * sx, texture.height - (r.y + r.height) * sy, r.width * sx, r.height * sy);
                sprites[i] = Sprite.Create(texture, crop, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                sprites[i].name = "Sweet " + i;
            }
            return sprites;
        }
    }
}
