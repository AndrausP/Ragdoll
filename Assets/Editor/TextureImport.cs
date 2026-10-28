using UnityEditor;

/// Texturas PBR baixadas (ambientCG, Resources/Textures): *_NormalGL vira normal map, *_Roughness fica linear.
public class TextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("Resources/Textures/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 1024;
        ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
        ti.anisoLevel = 8;
        if (assetPath.Contains("_NormalGL")) ti.textureType = TextureImporterType.NormalMap;
        else if (assetPath.Contains("_Roughness")) ti.sRGBTexture = false;
    }
}
