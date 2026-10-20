using UnityEngine;

namespace CityPack
{
    /// <summary>
    /// Tunes Standard-shader surfaces on this scene only.
    /// Source City Pack materials stay unchanged; values live in a property block.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CitySurfaceStyler : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int GlossId = Shader.PropertyToID("_Glossiness");

        [Range(0f, 0.25f)]
        public float variation = 0.08f;

        void OnEnable()
        {
            Apply();
        }

        public void Apply()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                var source = renderer.sharedMaterial;
                if (source == null || source.shader == null)
                    continue;
                if (source.shader.name.IndexOf("Skybox", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var category = Classify(source.name + " " + renderer.gameObject.name);
                var metal = 0f;
                var smooth = 0.18f;
                Values(category, ref metal, ref smooth);

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                if (source.HasProperty("_Metallic"))
                    block.SetFloat(MetallicId, metal);
                if (source.HasProperty("_Glossiness"))
                    block.SetFloat(GlossId, smooth);

                if (variation > 0f && source.HasProperty("_Color") && category != SurfaceCategory.Foliage && category != SurfaceCategory.Glass)
                {
                    var color = source.GetColor("_Color");
                    var hash = Hash01(renderer.gameObject.name.GetHashCode() ^ i * 17);
                    var dirt = Mathf.Lerp(0.94f, 1.04f, hash);
                    var tinted = new Color(color.r * dirt, color.g * dirt * Mathf.Lerp(0.98f, 1.02f, hash), color.b * dirt, color.a);
                    block.SetColor(ColorId, Color.Lerp(color, tinted, variation / 0.08f));
                }

                renderer.SetPropertyBlock(block);
            }
        }

        public static SurfaceCategory Classify(string raw)
        {
            var name = raw.ToLowerInvariant();
            if (name.Contains("glass"))
                return SurfaceCategory.Glass;
            if (name.Contains("wood") || name.Contains("bench") || name.Contains("fence"))
                return SurfaceCategory.Wood;
            if (name.Contains("metal") || name.Contains("lamp") || name.Contains("pole") || name.Contains("hydrant") || name.Contains("mailbox") || name.Contains("cart"))
                return SurfaceCategory.Metal;
            if (name.Contains("street") || name.Contains("asphalt") || name.Contains("road"))
                return SurfaceCategory.Asphalt;
            if (name.Contains("side") || name.Contains("walk") || name.Contains("concrete") || name.Contains("stone"))
                return SurfaceCategory.Concrete;
            if (name.Contains("grass") || name.Contains("leaf") || name.Contains("bush") || name.Contains("flower") || name.Contains("hedge") || name.Contains("tree") || name.Contains("plant"))
                return SurfaceCategory.Foliage;
            if (name.Contains("wall") || name.Contains("build") || name.Contains("shop") || name.Contains("brick"))
                return SurfaceCategory.Wall;
            return SurfaceCategory.Default;
        }

        public static void Values(SurfaceCategory category, ref float metallic, ref float smoothness)
        {
            switch (category)
            {
                case SurfaceCategory.Asphalt:
                    metallic = 0f;
                    smoothness = 0.18f;
                    break;
                case SurfaceCategory.Concrete:
                    metallic = 0f;
                    smoothness = 0.22f;
                    break;
                case SurfaceCategory.Wall:
                    metallic = 0.02f;
                    smoothness = 0.16f;
                    break;
                case SurfaceCategory.Metal:
                    metallic = 0.72f;
                    smoothness = 0.48f;
                    break;
                case SurfaceCategory.Glass:
                    metallic = 0.04f;
                    smoothness = 0.9f;
                    break;
                case SurfaceCategory.Wood:
                    metallic = 0f;
                    smoothness = 0.28f;
                    break;
                case SurfaceCategory.Foliage:
                    metallic = 0f;
                    smoothness = 0.08f;
                    break;
                default:
                    metallic = 0.02f;
                    smoothness = 0.2f;
                    break;
            }
        }

        static float Hash01(int seed)
        {
            var x = Mathf.Sin(seed * 12.9898f) * 43758.55f;
            return x - Mathf.Floor(x);
        }
    }

    public enum SurfaceCategory
    {
        Default,
        Asphalt,
        Concrete,
        Wall,
        Metal,
        Glass,
        Wood,
        Foliage
    }
}
