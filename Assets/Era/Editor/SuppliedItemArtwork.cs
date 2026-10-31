using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class SuppliedItemArtwork
    {
        public static void Prepare()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var id in IconLibrary.SuppliedObjects)
            {
                string path="Assets/Era/Resources/"+IconLibrary.ItemResourcePath((int)id)+".png";
                if(!File.Exists(path))throw new Exception("Missing supplied item: "+id);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer==null)throw new Exception("Missing PNG importer: "+id);
                if(importer.alphaIsTransparency&&importer.isReadable&&!importer.mipmapEnabled&&importer.maxTextureSize==256&&importer.textureCompression==TextureImporterCompression.Uncompressed&&importer.npotScale==TextureImporterNPOTScale.None)continue;
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency=true;importer.isReadable=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=256;importer.SaveAndReimport();
            }
            Directory.CreateDirectory("ArtSource/Items/v0.29/Runtime");
            foreach(var id in IconLibrary.SuppliedObjects)
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Era/Resources/"+IconLibrary.ItemResourcePath((int)id)+".png");
                File.WriteAllBytes("ArtSource/Items/v0.29/Runtime/"+id+".png",texture.EncodeToPNG());
            }
        }
    }
}
