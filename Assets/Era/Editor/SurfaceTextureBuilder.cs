using System.IO;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class SurfaceTextureBuilder
    {
        public static void Prepare()
        {
            const string path="Assets/Era/Resources/Textures";Directory.CreateDirectory(path);bool refresh=!File.Exists(path+"/Subtle-v2.txt");
            foreach(string name in new[]{"MeadowDetail","StoneDetail","WoodDetail"})
            {
                string file=path+"/"+name+".png";if(File.Exists(file)&&!refresh)continue;var texture=new Texture2D(128,128,TextureFormat.RGB24,false);var pixels=new Color[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {
                    float u=x/128f,v=y/128f,noise=Tile(u,v,5,17),fine=Tile(u,v,15,61);
                    float value=name=="WoodDetail"?.83f+Tile(u*1.0f,v,13,37)*.12f:name=="StoneDetail"?.90f+noise*.065f+fine*.025f:.96f+noise*.025f+fine*.015f;
                    pixels[y*128+x]=new Color(value,value,value);
                }
                texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(file,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(file);importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.SaveAndReimport();
            }
            File.WriteAllText(path+"/Subtle-v2.txt","Seamless subtle surface noise v2");
        }
        static float Tile(float u,float v,float f,float o)=>Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(u*f+o,v*f+o),Mathf.PerlinNoise((u-1)*f+o,v*f+o),u),Mathf.Lerp(Mathf.PerlinNoise(u*f+o,(v-1)*f+o),Mathf.PerlinNoise((u-1)*f+o,(v-1)*f+o),u),v);
    }
}
