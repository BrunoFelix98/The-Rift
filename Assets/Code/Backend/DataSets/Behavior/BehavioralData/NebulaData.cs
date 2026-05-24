using UnityEngine;

public class NebulaData : MonoBehaviour
{
    public NebulaSO data;
    private MeshRenderer nebRenderer;

    public void Initialize(NebulaSO template, CelestialHierarchyNode node)
    {
        data = template;
        if (nebRenderer == null) nebRenderer = GetComponent<MeshRenderer>();

#if UNITY_EDITOR
        string matPath = "Assets/Prefabs/Celestial Prefabs/Materials/DefaultNebula.mat";
        Material nebMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (nebMat != null) nebRenderer.material = nebMat;
#endif
    }
}
