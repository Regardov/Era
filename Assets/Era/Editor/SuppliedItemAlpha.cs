using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    // Final texture import: remove invisible matte residue and provide a zero-alpha safety border.
    public sealed class SuppliedItemAlpha:AssetPostprocessor
    {
        public override uint GetVersion()=>1;
        void OnPostprocessTexture(Texture2D texture)
        {
            if(!assetPath.StartsWith("Assets/Era/Resources/Items/v029/"))return;
            var pixels=texture.GetPixels32();int w=texture.width,h=texture.height;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int i=y*w+x;
                if(pixels[i].a<=4||x<3||y<3||x>=w-3||y>=h-3)pixels[i]=new Color32(0,0,0,0);
            }
            texture.SetPixels32(pixels);texture.Apply(false,false);
        }
    }
}
