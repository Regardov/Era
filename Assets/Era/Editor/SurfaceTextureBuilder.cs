using System.IO;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class SurfaceTextureBuilder
    {
        const int Size=512;
        public static void Prepare()
        {
            const string root="Assets/Era/Resources/Textures";Directory.CreateDirectory(root);
            if(File.Exists(root+"/Realm-surfaces-v9.txt"))return;
            foreach(string name in new[]{"MeadowDetail","StoneDetail","WoodDetail","LeafDetail"})
            {
                var pixels=new Color[Size*Size];var height=new float[Size*Size];
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
                {
                    float u=x/(float)Size,v=y/(float)Size;
                    float broad=Tile(u,v,5,17),fine=Tile(u,v,27,61),grain=Tile(u,v,73,131),value;
                    if(name=="WoodDetail")
                    {
                        float ridge=Mathf.Sin((u*14+Tile(u,v,6,37)*.45f)*Mathf.PI*2);
                        float cracks=Mathf.Pow(Mathf.Clamp01(ridge),14);
                        float knot=Tile(u,v,3,43);
                        value=.62f+broad*.18f+fine*.10f+grain*.06f-cracks*.29f;
                        value+=Mathf.Sin((v*45+u*3)*Mathf.PI*2)*.015f;
                    }
                    else if(name=="StoneDetail")
                    {
                        float seam=StoneSeam(u,v),fracture=Mathf.Exp(-seam*32);
                        value=.62f+broad*.22f+fine*.12f+grain*.06f-fracture*.25f;
                    }
                    else if(name=="LeafDetail")
                    {
                        float vein=Mathf.Abs(Mathf.Sin((u*11+v*6)*Mathf.PI*2));
                        value=.52f+broad*.18f+fine*.18f+vein*.10f;
                    }
                    else value=.59f+broad*.20f+fine*.12f+grain*.09f;
                    value=Mathf.Clamp01(value);height[y*Size+x]=value;
                    pixels[y*Size+x]=name=="StoneDetail"?new Color(value*.98f,value,value*.97f):new Color(value,value,value);
                }
                Write(root+"/"+name+".png",pixels,false);
                if(name=="WoodDetail"||name=="StoneDetail")
                {
                    var normals=new Color[pixels.Length];
                    for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
                    {
                        float dx=height[y*Size+(x+1)%Size]-height[y*Size+(x+Size-1)%Size];
                        float dy=height[((y+1)%Size)*Size+x]-height[((y+Size-1)%Size)*Size+x];
                        var n=new Vector3(-dx*2,-dy*2,1).normalized;normals[y*Size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
                    }
                    Write(root+"/"+(name=="WoodDetail"?"WoodNormal":"StoneNormal")+".png",normals,true);
                }
            }
            Grass(root);File.WriteAllText(root+"/Realm-surfaces-v9.txt","Original seamless surfaces, normal maps and curved grass blade cutout.");
        }
        static void Grass(string root)
        {
            var pixels=new Color[Size*Size];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                float u=x/(float)Size,v=y/(float)Size,alpha=0,shade=.8f;
                for(int blade=0;blade<11;blade++)
                {
                    float length=.62f+Hash(blade,91)*.37f,t=v/length;if(t>1)continue;
                    float foot=.18f+Hash(blade,12)*.64f,lean=(Hash(blade,42)-.5f)*.45f;
                    float center=foot+lean*Mathf.Pow(t,1.65f),width=(.012f+Hash(blade,63)*.009f)*Mathf.Pow(1-t,.75f);
                    float coverage=Mathf.Clamp01((width-Mathf.Abs(u-center))*Size+.5f);
                    if(coverage>alpha){alpha=coverage;shade=.65f+v*.25f+Hash(blade,12)*.1f;}
                }
                pixels[y*Size+x]=new Color(shade,shade,shade,alpha);
            }
            string file=root+"/GrassClump.png";var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(file,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(file);importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.SaveAndReimport();
        }
        static void Write(string file,Color[] pixels,bool normal)
        {
            var t=new Texture2D(Size,Size,TextureFormat.RGB24,false);t.SetPixels(pixels);t.Apply();File.WriteAllBytes(file,t.EncodeToPNG());Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(file);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.anisoLevel=4;importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        static float Hash(int x,int y){unchecked{int n=x*374761393+y*668265263;n=(n^(n>>13))*1274126177;return (n&0x7fffffff)/2147483647f;}}
        static float StoneSeam(float u,float v)
        {
            const int cells=7;float x=u*cells,y=v*cells;int cx=Mathf.FloorToInt(x),cy=Mathf.FloorToInt(y);float first=9,second=9;
            for(int a=-1;a<=1;a++)for(int b=-1;b<=1;b++)
            {
                int hx=(cx+a+cells)%cells,hy=(cy+b+cells)%cells;float px=cx+a+.2f+Hash(hx,hy)*.6f,py=cy+b+.2f+Hash(hx+19,hy+37)*.6f;
                float d=(px-x)*(px-x)+(py-y)*(py-y);if(d<first){second=first;first=d;}else if(d<second)second=d;
            }
            return Mathf.Sqrt(second)-Mathf.Sqrt(first);
        }
        static float Tile(float u,float v,float f,float o)=>Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(u*f+o,v*f+o),Mathf.PerlinNoise((u-1)*f+o,v*f+o),u),Mathf.Lerp(Mathf.PerlinNoise(u*f+o,(v-1)*f+o),Mathf.PerlinNoise((u-1)*f+o,(v-1)*f+o),u),v);
    }
}
