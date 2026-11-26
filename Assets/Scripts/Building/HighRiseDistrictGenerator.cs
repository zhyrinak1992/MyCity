using UnityEngine;

namespace MyCity.Building
{
    /// <summary>
    /// Procedural visual prototype for an original modern high-rise business cluster.
    /// Decorative only: it does not reserve plots or alter economy, zoning, saves or navigation.
    /// </summary>
    public static class HighRiseDistrictGenerator
    {
        public static GameObject Create(Vector3 center)
        {
            GameObject root = new GameObject("District | Modern High-Rise Cluster");
            Material glassBlue = MakeMaterial(new Color(0.16f, 0.34f, 0.46f), 0.42f, 0.82f);
            Material glassSilver = MakeMaterial(new Color(0.48f, 0.57f, 0.62f), 0.32f, 0.78f);
            Material darkMetal = MakeMaterial(new Color(0.10f, 0.14f, 0.18f), 0.72f, 0.68f);
            Material warmGlass = MakeMaterial(new Color(0.42f, 0.36f, 0.27f), 0.28f, 0.72f);
            Material podium = MakeMaterial(new Color(0.31f, 0.35f, 0.37f), 0.35f, 0.58f);
            Material accent = MakeMaterial(new Color(0.76f, 0.66f, 0.43f), 0.55f, 0.62f);

            // Four distinct silhouettes; the cluster uses a neutral in-game identity.
            CreateTower(root.transform, "North Tower", center + new Vector3(-10f, 0f, 8f), new Vector2(7f, 7f), 34f, glassBlue, darkMetal, podium, accent, 0);
            CreateTower(root.transform, "East Tower", center + new Vector3(8f, 0f, 7f), new Vector2(6f, 8f), 27f, glassSilver, darkMetal, podium, accent, 1);
            CreateTower(root.transform, "South Tower", center + new Vector3(7f, 0f, -9f), new Vector2(8f, 6f), 42f, warmGlass, darkMetal, podium, accent, 2);
            CreateTower(root.transform, "West Tower", center + new Vector3(-9f, 0f, -8f), new Vector2(6f, 6f), 23f, glassBlue, darkMetal, podium, accent, 3);
            CreatePublicPlaza(root.transform, center, podium, accent);
            return root;
        }

        private static void CreateTower(Transform parent, string name, Vector3 ground, Vector2 footprint,
            float height, Material facade, Material metal, Material podium, Material accent, int style)
        {
            float width = footprint.x;
            float depth = footprint.y;
            float podiumHeight = 3.2f;
            Cube(name + " | podium", ground + Vector3.up * (podiumHeight * 0.5f),
                new Vector3(width + 2.4f, podiumHeight, depth + 2.4f), podium, parent);

            float towerHeight = Mathf.Max(12f, height - podiumHeight);
            Vector3 towerCenter = ground + Vector3.up * (podiumHeight + towerHeight * 0.5f);
            if (style == 1)
            {
                // Slightly tapered profile.
                Cube(name + " | lower shaft", ground + Vector3.up * (podiumHeight + towerHeight * 0.36f),
                    new Vector3(width, towerHeight * 0.72f, depth), facade, parent);
                Cube(name + " | upper shaft", ground + Vector3.up * (podiumHeight + towerHeight * 0.86f),
                    new Vector3(width * 0.78f, towerHeight * 0.28f, depth * 0.78f), facade, parent);
            }
            else if (style == 2)
            {
                // Stepped crown profile.
                Cube(name + " | main shaft", towerCenter - Vector3.up * 1.2f,
                    new Vector3(width, towerHeight - 2.4f, depth), facade, parent);
                Cube(name + " | crown step", ground + Vector3.up * (height - 1.2f),
                    new Vector3(width * 0.72f, 2.4f, depth * 0.72f), metal, parent);
            }
            else
            {
                Cube(name + " | shaft", towerCenter, new Vector3(width, towerHeight, depth), facade, parent);
            }

            // Horizontal floor bands and corner fins create readable scale without hundreds of window objects.
            int floors = Mathf.Max(5, Mathf.RoundToInt(towerHeight / 2.8f));
            for (int floor = 1; floor < floors; floor++)
            {
                float y = podiumHeight + towerHeight * floor / floors;
                Cube(name + " | floor band " + floor, ground + Vector3.up * y,
                    new Vector3(width + 0.08f, 0.09f, depth + 0.08f), metal, parent);
            }

            float finWidth = 0.16f;
            for (int side = -1; side <= 1; side += 2)
            {
                Cube(name + " | facade fin X " + side,
                    ground + new Vector3(side * (width * 0.5f - 0.22f), podiumHeight + towerHeight * 0.5f, -depth * 0.5f - 0.06f),
                    new Vector3(finWidth, towerHeight, 0.12f), accent, parent);
                Cube(name + " | facade fin Z " + side,
                    ground + new Vector3(-width * 0.5f - 0.06f, podiumHeight + towerHeight * 0.5f, side * (depth * 0.5f - 0.22f)),
                    new Vector3(0.12f, towerHeight, finWidth), accent, parent);
            }

            // Roof plant and a restrained illuminated crown marker.
            Cube(name + " | roof plant", ground + Vector3.up * (height + 0.55f),
                new Vector3(width * 0.34f, 1.1f, depth * 0.34f), metal, parent);
            Cube(name + " | crown light", ground + Vector3.up * (height + 1.2f),
                new Vector3(width * 0.42f, 0.12f, 0.12f), accent, parent);
        }

        private static void CreatePublicPlaza(Transform parent, Vector3 center, Material paving, Material accent)
        {
            Cube("High-rise plaza | paving", center + new Vector3(0f, 0.045f, 0f),
                new Vector3(13f, 0.09f, 13f), paving, parent);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Vector3 p = center + new Vector3(Mathf.Cos(angle) * 5.1f, 0.35f, Mathf.Sin(angle) * 5.1f);
                GameObject bollard = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bollard.name = "High-rise plaza | light bollard " + (i + 1);
                bollard.transform.SetParent(parent, false);
                bollard.transform.position = p;
                bollard.transform.localScale = new Vector3(0.22f, 0.35f, 0.22f);
                bollard.GetComponent<Renderer>().sharedMaterial = accent;
            }
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static Material MakeMaterial(Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }
    }
}
