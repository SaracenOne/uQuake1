using UnityEngine;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

public class GenerateMap : MonoBehaviour
{
    public string mapName;
    public bool lightMapsEnabled;
    public bool skipSky;
	public bool overrideMaterials;
	public bool overrideTextures;
    private BSP29map map;
    private Dictionary<string, Material> materialDictionary;

    void Start()
    {

    }

    void Update()
    {

    }

    void PopulateLevel()
    {
        map = new BSP29map(mapName, overrideTextures);
        GenerateMapObjects();
    }

    void GenerateMapObjects()
    {
        materialDictionary = new Dictionary<string, Material>();

        foreach (Dictionary<string, string> entity in map.entityLump.entityDictionary)
            GenerateEntity(entity);

        foreach (BSPFace face in map.facesLump.faces)
            GenerateFaceObject(face);

        foreach (string name in materialDictionary.Keys)
            ExportMaterial(name, materialDictionary[name]);

        AssetDatabase.SaveAssets();
    }


    #region Editor Widgets

#if UNITY_EDITOR
    [CustomEditor(typeof(GenerateMap))]
    class GenerateMapInEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GenerateMap script = (GenerateMap)target;
            if (GUILayout.Button("Generate")) {
                script.PopulateLevel();
#if UNITY_EDITOR
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
            }

            if (GUILayout.Button("Clear"))
            {
                if (script.gameObject.transform.childCount > 0)
                {
                    var children = new List<GameObject>();
                    foreach (Transform child in script.gameObject.transform) children.Add(child.gameObject);
                    children.ForEach(child => DestroyImmediate(child));

#if UNITY_EDITOR
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
                }
            }
        }
    }
#endif

    #endregion


    #region Face Object Generation

    void ExportMaterial(string materialName, Material material)
    {
        string matReadPath = "Assets/Resources/Materials/" + materialName + ".mat";
        if (overrideMaterials) {
            AssetDatabase.CreateAsset(material, matReadPath);
        } else {
            Material serialisedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matReadPath);
            if(!serialisedMaterial) {
                AssetDatabase.CreateAsset(material, matReadPath);
            }
        }
    }

    GameObject GenerateEntity(Dictionary<string, string> entity)
    {
        if (entity.ContainsKey("classname")) {
            string classname = entity["classname"];
            GameObject entityObject = new GameObject(classname);
            entityObject.transform.parent = gameObject.transform;

            if (entity.ContainsKey("origin")) {
                string originString = entity["origin"];
                string[] coordinates = originString.Split(' ');
                if(coordinates.Length == 3) {
                    float x = -float.Parse(coordinates[0]) * BSP29map.QUAKE_TO_UNITY_CONVERSION_SCALE;
                    float y = float.Parse(coordinates[2]) * BSP29map.QUAKE_TO_UNITY_CONVERSION_SCALE; ;
                    float z = -float.Parse(coordinates[1]) * BSP29map.QUAKE_TO_UNITY_CONVERSION_SCALE;

                    entityObject.transform.localPosition = new Vector3(x, y, z);
                }
            }

            if(classname == "light") {
                entityObject.AddComponent<Light>();
                entityObject.isStatic = true;
            }

            return entityObject;
        }

        return null;
    }

    GameObject GenerateFaceObject(BSPFace face)
    {
        GameObject faceObject = new GameObject("BSPface");
        faceObject.transform.parent = gameObject.transform;
        Mesh faceMesh = new Mesh();
        faceMesh.name = "BSPmesh";

        // grab our verts
        Vector3[] verts = new Vector3[face.num_ledges];
        int edgestep = face.ledge_index;
        for (int i = 0; i < face.num_ledges; i++)
        {
            if (map.edgeLump.ledges[face.ledge_index + i] < 0)
            {
                verts[i] = map.vertLump.verts[map.edgeLump.edges[Mathf.Abs(map.edgeLump.ledges[edgestep])].vert1];
            }
            else
            {
                verts[i] = map.vertLump.verts[map.edgeLump.edges[map.edgeLump.ledges[edgestep]].vert2];
            }
            edgestep++;
        }

        // whip up tris
        int[] tris = new int[(face.num_ledges - 2) * 3];
        int tristep = 1;
        for (int i = 1; i < verts.Length - 1; i++)
        {
            tris[tristep - 1] = 0;
            tris[tristep] = i;
            tris[tristep + 1] = i + 1;
            tristep += 3;
        }

        // whip up uvs
        float scales = map.miptexLump.textures[map.texinfoLump.texinfo[face.texinfo_id].miptex].width * BSP29map.QUAKE_TO_UNITY_CONVERSION_SCALE;
        float scalet = map.miptexLump.textures[map.texinfoLump.texinfo[face.texinfo_id].miptex].height * BSP29map.QUAKE_TO_UNITY_CONVERSION_SCALE;
        Vector2[] uvs = new Vector2[face.num_ledges];
        for (int i = 0; i < face.num_ledges; i++)
        {
            uvs[i] = new Vector2((Vector3.Dot(verts[i], map.texinfoLump.texinfo[face.texinfo_id].vec3s) + map.texinfoLump.texinfo[face.texinfo_id].offs) / scales, (Vector3.Dot(verts[i], map.texinfoLump.texinfo[face.texinfo_id].vec3t) + map.texinfoLump.texinfo[face.texinfo_id].offt) / scalet);
            uvs[i].y = (1.0f - uvs[i].y);
        }

        faceMesh.vertices = verts;
        faceMesh.triangles = tris;
        faceMesh.uv = uvs;
        faceMesh.RecalculateNormals();
        faceObject.AddComponent<MeshFilter>();
        faceObject.GetComponent<MeshFilter>().mesh = faceMesh;
        faceObject.AddComponent<MeshRenderer>();

        // We make a material and then use shared material to work around a leak in the editor
        Material mat = null;
        string textureName = map.miptexLump.textures[map.texinfoLump.texinfo[face.texinfo_id].miptex].name;

        if (lightMapsEnabled)
        {
            // Lightmap wankery
            mat = new Material(Shader.Find("Legacy Shaders/Lightmapped/Diffuse"));
            int pointer = face.lightmap;
            if (pointer != -1)
            {
                int size = 10;
                Texture2D lightmap = new Texture2D(size, size);

                Color[] colors = new Color[size * size];

                for (int i = 0; i < size * size; i++)
                {
                    if (i >= map.lightLump.RawMaps.Length)
                        break;
                    var temp = map.lightLump.RawMaps[pointer + i];
                    colors[i] = new Color32(temp, temp, temp, 255);
                }
                lightmap.SetPixels(colors);

                mat.SetTexture("_LightMap", lightmap);
            } 
        }
        else
        {
            if (materialDictionary.ContainsKey(textureName)) {
                mat = materialDictionary[textureName];
            } else {
                if (!overrideMaterials)  {
                    string matReadPath = "Assets/Resources/Materials/" + textureName + ".mat";
                    mat = AssetDatabase.LoadAssetAtPath<Material>(matReadPath);
                    if (mat) {
                        materialDictionary[textureName] = mat;
                    }
                }

                if (mat == null) {
                    mat = new Material(Shader.Find("Diffuse"));
                    materialDictionary[textureName] = mat;
                }
            }
        }

        // Set the texture we made above, after possible lightmapping circlejerk
        mat.mainTexture = map.miptexLump.textures[map.texinfoLump.texinfo[face.texinfo_id].miptex];

        // Set the material
        faceObject.GetComponent<Renderer>().sharedMaterial = mat;

        // Turn off the renderer if the face is part of a trigger brush
        string texName = map.miptexLump.textures[map.texinfoLump.texinfo[face.texinfo_id].miptex].name;
        if (texName == "trigger")
        {
            faceObject.GetComponent<Renderer>().enabled = false;
        }

        // Skip any textures that show a sky, if we want
        if (skipSky && texName.StartsWith("sky"))
            faceObject.GetComponent<Renderer>().enabled = false;

        faceObject.AddComponent<MeshCollider>();
        faceObject.isStatic = true;

        return faceObject;
    }
    #endregion
}