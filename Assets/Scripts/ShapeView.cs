using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Caches the built-in primitive meshes and resolves a material that works with the
/// active render pipeline. A pink object means the shader is not supported by the
/// pipeline (or was stripped from the build), so the template material is chosen in
/// this order:
///   1. A material assigned by the user in the inspector (MatchGameController.materialOverride)
///   2. The default material of the active render pipeline (URP Lit in a URP project)
///   3. The primitive's default material (Built-in pipeline)
/// </summary>
public static class ShapeLibrary
{
    static Mesh[] meshes;
    static Material primitiveMaterial;
    static Material overrideMaterial;
    static Material template;

    public static void Configure(Material materialOverride)
    {
        overrideMaterial = materialOverride;
        template = null; // force the template to be resolved again
    }

    static void Init()
    {
        if (meshes == null || meshes[0] == null)
        {
            meshes = new Mesh[3];
            PrimitiveType[] types = { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder };

            for (int i = 0; i < types.Length; i++)
            {
                GameObject go = GameObject.CreatePrimitive(types[i]);
                meshes[i] = go.GetComponent<MeshFilter>().sharedMesh;
                if (i == 0) primitiveMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
                Object.Destroy(go);
            }
        }

        if (template == null) template = ResolveTemplate();
    }

    static Material ResolveTemplate()
    {
        if (overrideMaterial != null) return overrideMaterial;

        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline != null && pipeline.defaultMaterial != null) return pipeline.defaultMaterial;

        return primitiveMaterial;
    }

    public static Mesh GetMesh(ShapeType shape)
    {
        Init();
        return meshes[(int)shape];
    }

    public static Material CreateMaterialInstance()
    {
        Init();
        return new Material(template);
    }
}

/// <summary>
/// Displays an ObjectSpec as a 3D object and smoothly animates scale and rotation.
/// </summary>
public class ShapeView : MonoBehaviour
{
    const float SmoothSpeed = 12f;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Material material;

    Vector3 desiredScale = Vector3.one * 0.25f;
    Quaternion desiredRotation = Quaternion.identity;

    public static ShapeView Create(string objectName, Transform parent, Vector3 localPosition)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.AddComponent<ShapeView>();
    }

    void Awake()
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        material = ShapeLibrary.CreateMaterialInstance();
        meshRenderer.sharedMaterial = material;
    }

    public void Apply(ObjectSpec spec, bool instant = false)
    {
        meshFilter.sharedMesh = ShapeLibrary.GetMesh(spec.shape);
        SetMaterialColor(spec.UnityColor);
        desiredScale = spec.VisualScale;
        desiredRotation = spec.Rotation;

        if (instant)
        {
            transform.localScale = desiredScale;
            transform.rotation = desiredRotation;
        }
    }

    // URP uses _BaseColor, the Built-in pipeline uses _Color. Set whichever exists.
    void SetMaterialColor(Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    void Update()
    {
        float t = 1f - Mathf.Exp(-SmoothSpeed * Time.deltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, desiredScale, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
