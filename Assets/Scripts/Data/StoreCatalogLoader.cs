using System.IO;
using UnityEngine;

namespace MyCity.Data
{
    /// <summary>Loads editable store/product definitions from StreamingAssets. Built-in catalog remains the safe fallback.</summary>
    public static class StoreCatalogLoader
    {
        public static bool TryLoadExternalCatalog()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "MyCity", "store_catalog.json");
            if (!File.Exists(path)) return false;
            try
            {
                StoreCatalogFile file = JsonUtility.FromJson<StoreCatalogFile>(File.ReadAllText(path));
                if (file == null || file.Version < 1 || file.Stores == null) return false;
                return StoreCatalog.ReplaceCatalog(file.Stores);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("My City: не удалось загрузить внешний каталог магазинов; используются встроенные данные. " + exception.Message);
                return false;
            }
        }
    }
}
