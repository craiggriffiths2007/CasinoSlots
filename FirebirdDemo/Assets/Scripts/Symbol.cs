using System.Collections;
using UnityEngine;

public enum SymbolType
{
    Bell,
    BlueOrb,
    Cherry,
    Diamond,
    GreenOrb,
    Lemon,
    Phoenix,
    Phoenix2,
    Plum,
    RedOrb,
    Seven,
    YellowOrb
}

public class Symbol : MonoBehaviour
{
    [SerializeField]
    private SymbolType symbolType;

    [SerializeField]
    private SpriteRenderer spriteRenderer;

    [SerializeField]
    private float maxWidth = 1.88f;

    [SerializeField]
    private float maxHeight = 1.88f;

    public SymbolType SymbolType => symbolType;

    public SpriteRenderer Renderer => spriteRenderer;

    public Sprite CurrentSprite => spriteRenderer.sprite;

    public void SetSymbol(
        SymbolType type,
        Sprite sprite)
    {
        symbolType = type;
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;

        FitSprite();

        spriteRenderer.enabled = true;
        SetWildFire(spriteRenderer, type == SymbolType.Phoenix || type == SymbolType.RedOrb);
    }

    public void SetVisible(bool visible)
    {
        spriteRenderer.enabled = visible;
    }

    public void SetRowPosition(
        int row,
        int rowCount,
        float verticalSpacing)
    {
        float startY =
            ((rowCount - 1) * verticalSpacing) / 2f;

        transform.localPosition =
            new Vector3(
                0f,
                startY - row * verticalSpacing,
                0f);
    }

    public IEnumerator Pulse(float duration)
    {
        yield return PulseRenderer(spriteRenderer, transform, duration);
    }

    public static IEnumerator PulseRenderer(SpriteRenderer renderer, Transform target, float duration)
    {
        Vector3 normalScale = target.localScale;
        Material originalMaterial = renderer.sharedMaterial;
        Material pulseMaterial = new Material(originalMaterial);
        Color normalTint = pulseMaterial.GetColor("_Color");
        renderer.sharedMaterial = pulseMaterial;
        float attack = Mathf.Min(0.12f, duration * 0.15f);
        float elapsed = 0f;
        try
        {
            while (elapsed < duration)
            {
                // One quick rise, then return over half the remaining win display.
                float amount = elapsed < attack
                    ? Mathf.SmoothStep(0f, 1f, elapsed / attack)
                    : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - attack) / ((duration - attack) * 0.5f));
                target.localScale = normalScale * (1f + 0.06f * amount);
                float brightness = 1f + 0.12f * amount;
                pulseMaterial.SetColor("_Color", new Color(normalTint.r * brightness,
                    normalTint.g * brightness, normalTint.b * brightness, normalTint.a));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            if (target != null) target.localScale = normalScale;
            if (renderer != null) renderer.sharedMaterial = originalMaterial;
            Destroy(pulseMaterial);
        }
    }
    public const float BurnDuration = 0.74f;
    public const float EmberDuration = 0.22f;

    private static Texture2D fireTexture;
    private static Material fireMaterial;
    private static Material afterBurnMaterial;

    public IEnumerator Disappear(float duration = BurnDuration)
    {
        yield return BurnRenderer(spriteRenderer, duration, isWild: symbolType == SymbolType.Phoenix || symbolType == SymbolType.Phoenix2 || symbolType == SymbolType.RedOrb);
    }

    public static IEnumerator BurnRenderer(SpriteRenderer spriteRenderer, float duration = BurnDuration, bool reveal = false, bool isWild = false)
    {
        SetWildFire(spriteRenderer, false);
        Bounds bounds = spriteRenderer.bounds;
        Material originalMaterial = spriteRenderer.sharedMaterial;
        Shader burnShader = Resources.Load<Shader>("SymbolBurn");
        if (burnShader == null)
            throw new System.InvalidOperationException("Missing Resources/SymbolBurn shader.");
        Material burnMaterial = new Material(burnShader);
        burnMaterial.SetFloat("_BurnBottom", bounds.min.y);
        burnMaterial.SetFloat("_BurnHeight", Mathf.Max(bounds.size.y, 0.001f));
        burnMaterial.SetFloat("_BurnProgress", 0f);
        burnMaterial.SetFloat("_Reveal", reveal ? 1f : 0f);
        burnMaterial.SetFloat("_BurnLeft", bounds.min.x);
        burnMaterial.SetFloat("_BurnWidth", Mathf.Max(bounds.size.x, 0.001f));
        spriteRenderer.sharedMaterial = burnMaterial;

        ParticleSystem fire = CreateFire(spriteRenderer, bounds, duration);
        float emberAmount = isWild ? 0.2f : 1f;
        fire.GetComponent<BonfireEmbers>().Amount = emberAmount;
        var shape = fire.shape;
        shape.enabled = false;
        var main = fire.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.40f, 1.06f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(EmberDuration * .65f, EmberDuration);
        main.startRotation = new ParticleSystem.MinMaxCurve(-.25f, .25f);
        main.maxParticles = 240;
        var emission = fire.emission;
        emission.rateOverTime = 0f;
        var velocity = fire.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.85f, 0.85f);
        velocity.y = new ParticleSystem.MinMaxCurve(3.6f * 1.02f, 7.8f * 1.02f);
        fire.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        ParticleSystem embers = CreateAfterBurn(spriteRenderer, bounds, false);
        ParticleSystem smoke = CreateAfterBurn(spriteRenderer, bounds, true);
        float elapsed = 0f;
        float flameBudget = 0f, emberBudget = 0f, smokeBudget = 0f;
        try
        {
            while (elapsed < duration)
            {
                // Keep the burn aligned with the renderer bounds.
                bounds = spriteRenderer.bounds;
                burnMaterial.SetFloat("_BurnBottom", bounds.min.y);
                burnMaterial.SetFloat("_BurnHeight", Mathf.Max(bounds.size.y, 0.001f));
                burnMaterial.SetFloat("_BurnLeft", bounds.min.x);
                burnMaterial.SetFloat("_BurnWidth", Mathf.Max(bounds.size.x, 0.001f));
                float t = Mathf.Clamp01(elapsed / duration);
                // Brief ignition, then a fast accelerating flare with a short ember tail.
                float sweep = BurnSweep(t);
                burnMaterial.SetFloat("_BurnProgress", sweep);
                // Reduce new flames over the upper half, reaching zero at the top.
                float taper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, 1f, sweep));
                EmitOnBurnEdge(fire, spriteRenderer, bounds, sweep, 270f * taper, ref flameBudget);
                fire.transform.position = new Vector3(bounds.center.x,
                    Mathf.Lerp(bounds.min.y, bounds.max.y, sweep), bounds.center.z);
                UpdateBurnTrail(embers, spriteRenderer, bounds, t - 0.06f, 200f * emberAmount, ref emberBudget);
                UpdateBurnTrail(smoke, spriteRenderer, bounds, t - 0.12f, 28f, ref smokeBudget);
                elapsed += Time.deltaTime;
                yield return null;
            }

            burnMaterial.SetFloat("_BurnProgress", 1f);
            spriteRenderer.enabled = reveal;
            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(EmberDuration);
        }
        finally
        {
            // Restore the renderer before this symbol is reused by the next cascade.
            if (spriteRenderer != null)
                spriteRenderer.sharedMaterial = originalMaterial;
            Destroy(burnMaterial);
            if (fire != null)
            {
                ReleaseFireEmbers(fire);
                Destroy(fire.gameObject);
            }
            foreach (var trail in new[] { embers, smoke })
            {
                if (trail == null) continue;
                trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(trail.gameObject, 2.2f);
            }
        }
    }

    public static ParticleSystem CreateFire(SpriteRenderer spriteRenderer, Bounds bounds, float duration)
    {
        if (fireMaterial == null)
        {
            fireTexture = Resources.Load<Texture2D>("FlameFlipbook20");
            if (fireTexture == null)
                throw new System.InvalidOperationException("Missing Resources/FlameFlipbook20 flame atlas.");
            // UI/Default is already used by the scene's UI and supports vertex tint.
            fireMaterial = new Material(Shader.Find("UI/Default"));
            fireMaterial.name = "Win Fire Material";
            fireMaterial.mainTexture = fireTexture;
        }

        GameObject effect = new GameObject("Winning Symbol Fire");
        effect.transform.SetParent(spriteRenderer.transform.parent, true);
        effect.transform.position = bounds.center;
        ParticleSystem fire = effect.AddComponent<ParticleSystem>();
        fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = fire.main;
        main.loop = false;
        main.duration = duration;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, EmberDuration);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.55f);
        main.maxParticles = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = Color.white;
        var animation = fire.textureSheetAnimation;
        animation.enabled = true;
        animation.mode = ParticleSystemAnimationMode.Grid;
        animation.numTilesX = 5;
        animation.numTilesY = 4;
        animation.animation = ParticleSystemAnimationType.WholeSheet;
        animation.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
        animation.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        animation.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
        animation.cycleCount = 1;
        var emission = fire.emission;
        emission.rateOverTime = 100f;
        var shape = fire.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(bounds.size.x * 0.8f, bounds.size.y * 0.7f, 0.01f);
        var velocity = fire.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        velocity.y = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var color = fire.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.02f), 0.35f),
                    new GradientColorKey(new Color(0.8f, 0.08f, 0.01f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var sizeOverLife = fire.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));
        var renderer = fire.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = fireMaterial;
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + 5;
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        var sparks = CreateAfterBurn(spriteRenderer, bounds, false);
        sparks.transform.SetParent(fire.transform, true);
        var companion = fire.gameObject.AddComponent<BonfireEmbers>();
        companion.Particles = sparks;
        AddFireTurbulence(fire);
        fire.Play();
        return fire;
    }

    // Smooth spatial variation, independent of the game's outcome RNG.
    public static float FireVariation(Vector3 position, float phase = 0f)
    {
        float time = Time.time * .5f;
        return Mathf.PerlinNoise(position.x * .4f + time + phase + 37f,
            position.y * .4f - time * .6f + phase + 91f) * 2f - 1f;
    }

    public static void AddFireTurbulence(ParticleSystem particles, float strength = .18f)
    {
        var noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = strength;
        noise.strengthY = strength * .45f;
        noise.strengthZ = 0f;
        noise.frequency = .35f;
        noise.scrollSpeed = .30f;
        noise.damping = true;
        noise.octaveCount = 3;
        noise.octaveScale = 1.5f;
        noise.octaveMultiplier = .4f;
        noise.quality = ParticleSystemNoiseQuality.Medium;
    }

    private static float BurnSweep(float t) =>
        Mathf.Pow(Mathf.Clamp01((t - 0.10f) / (0.78f / (1.2f * 1.02f))), 0.65f);

    private static void EmitOnBurnEdge(ParticleSystem particles, SpriteRenderer source,
        Bounds bounds, float sweep, float rate, ref float budget)
    {
        budget += rate * (1f + .22f * FireVariation(bounds.center)) * Time.deltaTime;
        int count = Mathf.FloorToInt(budget);
        budget -= count;
        Sprite sprite = source.sprite;
        if (sprite == null || !sprite.texture.isReadable) return;
        Rect textureRect = sprite.textureRect;
        Vector2 offset = sprite.textureRectOffset;
        for (int i = 0; i < count; i++)
        {
            float x = Random.value;
            // Match the uneven rising edge in SymbolBurn.shader.
            float wave = Mathf.Sin(x * 15f + sweep * 12f) * 0.055f
                + Mathf.Sin(x * 37f - sweep * 19f) * 0.025f;
            float height = Mathf.Lerp(-0.15f, 1.18f, sweep) + wave + (x - 0.5f) * 0.16f
                + Random.Range(0f, 0.04f);
            if (height < 0f || height > 1f) continue;
            Vector3 world = new Vector3(bounds.min.x + x * bounds.size.x,
                bounds.min.y + height * bounds.size.y, bounds.center.z);
            Vector3 local = source.transform.InverseTransformPoint(world);
            if (source.flipX) local.x = -local.x;
            if (source.flipY) local.y = -local.y;
            Vector2 pixel = new Vector2(local.x, local.y) * sprite.pixelsPerUnit + sprite.pivot - offset;
            if (pixel.x < 0f || pixel.y < 0f || pixel.x >= textureRect.width || pixel.y >= textureRect.height)
                continue;
            float alpha = sprite.texture.GetPixelBilinear(
                (textureRect.x + pixel.x) / sprite.texture.width,
                (textureRect.y + pixel.y) / sprite.texture.height).a;
            // Transparent holes emit nothing; antialiased edges emit fewer particles.
            if (alpha <= 0.05f || Random.value > alpha) continue;
            particles.Emit(new ParticleSystem.EmitParams { position = world, applyShapeToPosition = false }, 1);
            EmitFireEmbers(particles, world);
        }
    }

    private static void UpdateBurnTrail(ParticleSystem trail, SpriteRenderer source, Bounds bounds,
        float delayedTime, float rate, ref float budget)
    {
        float sweep = BurnSweep(delayedTime);
        trail.transform.position = new Vector3(bounds.center.x,
            Mathf.Lerp(bounds.min.y, bounds.max.y, sweep), bounds.center.z);
        float taperedRate = delayedTime <= 0.10f ? 0f
            : rate * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, sweep)));
        EmitOnBurnEdge(trail, source, bounds, sweep, taperedRate, ref budget);
    }

    private static ParticleSystem CreateAfterBurn(SpriteRenderer source, Bounds bounds, bool smoke)
    {
        if (afterBurnMaterial == null)
        {
            // Soft radial particles: tiny glowing embers or larger translucent smoke wisps.
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "After-burn soft particle";
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            texture.Apply(false, true);
            afterBurnMaterial = new Material(Shader.Find("UI/Default"));
            afterBurnMaterial.name = "After-burn particles";
            afterBurnMaterial.mainTexture = texture;
        }

        var effect = new GameObject(smoke ? "Symbol smoke" : "Sparkling embers");
        effect.transform.SetParent(source.transform.parent, true);
        effect.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        var particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 0.85f;
        main.startLifetime = smoke ? new ParticleSystem.MinMaxCurve(0.65f, 0.85f) : new ParticleSystem.MinMaxCurve(0.35f, 2.05f);
        main.startSpeed = 0f;
        main.startSize = smoke ? new ParticleSystem.MinMaxCurve(0.45f, 0.8f)
            : new ParticleSystem.MinMaxCurve(0.045f, 0.11f);
        main.startColor = smoke ? new Color(0.45f, 0.42f, 0.39f, 0.22f)
            : new Color(1f, 0.78f, 0.3f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = smoke ? 80 : 600;
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(bounds.size.x * 0.8f, bounds.size.y * 0.06f, 0.01f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = smoke ? new ParticleSystem.MinMaxCurve(-0.22f, 0.22f) : new ParticleSystem.MinMaxCurve(-0.75f, 0.75f);
        velocity.y = smoke ? new ParticleSystem.MinMaxCurve(0.3f, 0.65f)
            : new ParticleSystem.MinMaxCurve(-0.45f, 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f),
                new GradientColorKey(smoke ? Color.white : new Color(1f, 0.35f, 0.06f), 1f) },
            smoke ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0f, 1f) }
            : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.25f, 0.2f),
                new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(0.3f, 0.55f),
                new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, smoke ? 1.8f : 0.2f));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = afterBurnMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + (smoke ? 3 : 6);
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        AddFireTurbulence(particles, smoke ? .20f : .425f);
        if (!smoke)
        {
            var force = particles.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.y = 0.7f;
            var noise = particles.noise;
            noise.frequency = .65f;
            noise.scrollSpeed = .65f;
            noise.strengthY = .325f;
            var mainSparks = particles.main;
            mainSparks.scalingMode = ParticleSystemScalingMode.Shape;
        }
        particles.Play();
        return particles;
    }

    public static void EmitFireEmbers(ParticleSystem fire, Vector3 world, float amount = 1f)
    {
        var companion = fire.GetComponent<BonfireEmbers>();
        if (companion != null) companion.Emit(world, amount);
    }

    public static void ReleaseFireEmbers(ParticleSystem fire)
    {
        var companion = fire.GetComponent<BonfireEmbers>();
        if (companion == null || companion.Particles == null) return;
        var sparks = companion.Particles;
        sparks.transform.SetParent(null, true);
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(sparks.gameObject, 2.2f);
        companion.Particles = null;
    }

    private void FitSprite()
    {
        if (spriteRenderer.sprite == null)
            return;

        transform.localScale =
            Vector3.one;

        Vector2 spriteSize =
            spriteRenderer.sprite.bounds.size;

        float widthScale =
            maxWidth / spriteSize.x;

        float heightScale =
            maxHeight / spriteSize.y;

        float scale =
            Mathf.Min(
                widthScale,
                heightScale);

        transform.localScale =
            new Vector3(
                scale,
                scale,
                1f);
    }

    public static void SetWildFire(SpriteRenderer source, bool active)
    {
        var edge = source.GetComponent<WildEdgeFire>();
        if (active && edge == null) edge = source.gameObject.AddComponent<WildEdgeFire>();
        if (edge != null) edge.enabled = active;
    }
}

// Ambient flames use their own random stream so they never affect spin outcomes.
public class WildEdgeFire : MonoBehaviour
{
    private readonly System.Random random = new System.Random();
    private readonly System.Collections.Generic.List<Vector3> outline = new();
    private SpriteRenderer source;
    private Sprite cachedSprite;
    private ParticleSystem flames;
    private float budget;
    private bool primeOutline = true;

    private void LateUpdate()
    {
        if (source == null) source = GetComponent<SpriteRenderer>();
        if (!source.enabled || source.sprite == null || source.color.a <= .01f)
        {
            if (flames != null) flames.Clear();
            budget = 0f;
            primeOutline = true;
            return;
        }
        if (cachedSprite != source.sprite) CacheOutline();
        if (outline.Count == 0) return;
        if (flames == null)
        {
            flames = Symbol.CreateFire(source, source.bounds, 1f);
            flames.name = "Continuous wild edge flames";
            flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            flames.transform.SetParent(source.transform, false);
            flames.transform.localPosition = Vector3.zero;
            var main = flames.main;
            main.loop = true;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            // Sizes below are in sprite-local units, so include the sprite's
            // parent scale instead of rendering those values as world sizes.
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = flames.emission;
            emission.enabled = false;
            var shape = flames.shape;
            shape.enabled = false;
            var velocity = flames.velocityOverLifetime;
            velocity.enabled = false;
            var size = flames.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, .2f), new Keyframe(.4f, 1f),
                new Keyframe(1f, .15f)));
            flames.Play();
        }
        var renderer = flames.GetComponent<ParticleSystemRenderer>();
        renderer.maskInteraction = source.maskInteraction;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + 1;
        float scale = Mathf.Max(.001f, Mathf.Abs(source.transform.lossyScale.y));
        float perimeter = 2f * (source.bounds.size.x + source.bounds.size.y);
        budget += Mathf.Clamp(perimeter * 30f, 80f, 300f) *
            (1f + .20f * Symbol.FireVariation(source.bounds.center)) * Time.deltaTime;
        int count = Mathf.Min(24, Mathf.FloorToInt(budget));
        budget -= Mathf.Floor(budget);
        if (primeOutline)
        {
            count = Mathf.Clamp(Mathf.CeilToInt(perimeter / .12f), 24, 100);
            primeOutline = false;
        }
        for (int i = 0; i < count; i++)
        {
            // Distribute every batch over the entire contour, including its
            // lower edge, instead of letting random clusters favour the top.
            int index = Mathf.Min(outline.Count - 1,
                (int)((i + random.NextDouble()) * outline.Count / count));
            Vector3 point = outline[index];
            if (source.flipX) point.x = -point.x;
            if (source.flipY) point.y = -point.y;
            Symbol.EmitFireEmbers(flames, source.transform.TransformPoint(point), 0.2f);
            float flicker = Symbol.FireVariation(source.transform.TransformPoint(point), 13f);
            flames.Emit(new ParticleSystem.EmitParams
            {
                position = point + Vector3.up * (.08f / scale),
                velocity = new Vector3(Range(-.025f, .025f) + flicker * .035f, Range(.03f, .09f), 0f) / scale,
                startSize = Range(.32f, .64f) * (1f + .15f * flicker) / scale,
                startLifetime = Range(.28f, .43f),
                startColor = new Color(1f, .85f, .55f, source.color.a * .85f),
                applyShapeToPosition = false
            }, 1);
        }
    }

    private float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

    private void CacheOutline()
    {
        cachedSprite = source.sprite;
        outline.Clear();
        if (!cachedSprite.texture.isReadable) return;
        Rect rect = cachedSprite.textureRect;
        Vector2 offset = cachedSprite.textureRectOffset;
        int step = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(rect.width, rect.height) / 128f));
        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < rect.width && y < rect.height &&
            cachedSprite.texture.GetPixel((int)rect.x + x, (int)rect.y + y).a > .5f;
        for (int y = 0; y < rect.height; y += step)
            for (int x = 0; x < rect.width; x += step)
                if (Solid(x, y) && (!Solid(x - step, y) || !Solid(x + step, y) ||
                    !Solid(x, y - step) || !Solid(x, y + step)))
                    outline.Add((new Vector2(x, y) + offset - cachedSprite.pivot) / cachedSprite.pixelsPerUnit);
    }

    private void OnDisable()
    {
        budget = 0f;
        primeOutline = true;
        if (flames != null) flames.Clear();
    }
}

public class BonfireEmbers : MonoBehaviour
{
    public ParticleSystem Particles;
    public float Amount = 1f;
    private readonly System.Random random = new System.Random();

    public void Emit(Vector3 world, float amount)
    {
        if (Particles == null || random.NextDouble() >= Mathf.Clamp01(Amount * amount)) return;
        if (!Particles.isPlaying) Particles.Play(false);
        var fireRenderer = GetComponent<ParticleSystemRenderer>();
        var emberRenderer = Particles.GetComponent<ParticleSystemRenderer>();
        emberRenderer.sortingLayerID = fireRenderer.sortingLayerID;
        emberRenderer.sortingOrder = fireRenderer.sortingOrder + 1;
        emberRenderer.maskInteraction = fireRenderer.maskInteraction;
        for (int i = 0; i < 1; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float speed = 0.25f + (float)random.NextDouble() * 0.65f;
            Particles.Emit(new ParticleSystem.EmitParams
            {
                position = world,
                velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed,
                applyShapeToPosition = false
            }, 1);
        }
    }
}
