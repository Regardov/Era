using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    public static class SurfaceDetail
    {
        public static void Apply(Transform world)
        {
            var cache=new Dictionary<string,Material>();foreach(var renderer in world.GetComponentsInChildren<Renderer>())
            {
                // Imported atlases and the custom terrain/water/grass shaders own their textures.
                if(renderer.GetComponentInParent<NatureAsset>()!=null)continue;
                var baseMaterial=renderer.sharedMaterial;
                if(baseMaterial!=null&&(baseMaterial.shader.name=="Era/Meadow Grass"||baseMaterial.shader.name=="Era/Still Water"))continue;
                string name=renderer.name.ToLowerInvariant();string texture=name.Contains("trunk")||name=="branch"||name.Contains("wood")?"WoodDetail":name.Contains("stone")||name.Contains("rock")||name.Contains("pillar")||name.Contains("floor")?"StoneDetail":"MeadowDetail";
                bool foliage=name.Contains("canopy")||name.Contains("bloom")||name.Contains("leaf")||name.Contains("frond")||name.Contains("fern");var original=renderer.sharedMaterial;if(original==null||original.IsKeywordEnabled("_EMISSION"))continue;string key=original.GetInstanceID()+texture+foliage;
                if(!cache.TryGetValue(key,out var material))
                {
                    material=new Material(original){name=original.name+" / Surface detail"};if(foliage)material.shader=Shader.Find("Era/Foliage");
                    if(material.HasProperty("_MainTex")){material.mainTexture=Resources.Load<Texture2D>("Textures/"+texture);material.mainTextureScale=texture=="WoodDetail"?new Vector2(2,2):new Vector2(2,2);}
                    cache[key]=material;
                }
                renderer.sharedMaterial=material;
            }
        }
    }
}
