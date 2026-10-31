using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

namespace Era.Editor
{
    public static class ItemArtChecks
    {
        [Serializable]sealed class SuppliedRecord{public string item,sha256;}
        [Serializable]sealed class SuppliedManifest{public SuppliedRecord[] items;}
        static int count;
        static void Check(bool value,string label){count++;if(!value)throw new Exception("ERA item art check failed: "+label);}
        public static int Run()
        {
            count=0;var hashes=new HashSet<string>();int active=0;
            var supplied=JsonUtility.FromJson<SuppliedManifest>("{\"items\":"+File.ReadAllText("ArtSource/Items/v0.29/manifest.json")+"}");
            Check(supplied.items.Length==10&&IconLibrary.SuppliedObjects.Length==10,"exactly ten supplied replacement objects");
            using(var sha=SHA256.Create())for(int item=0;item<ItemCatalog.Count;item++)
            {
                string name=((ItemId)item).ToString(),png="Assets/Era/Resources/"+IconLibrary.ItemResourcePath(item)+".png",svg="ArtSource/Items/v0.25/"+name+".svg";
                var texture=IconLibrary.ItemTexture(item);var sprite=IconLibrary.ItemSprite(item);
                Check(File.Exists(png)&&File.Exists(svg)&&texture!=null&&sprite!=null,"individual PNG/SVG and runtime sprite: "+name);
                Check(texture.name==name&&texture.width==256&&texture.height==256&&sprite.texture==texture,"stable item mapping and full resolution: "+name);
                Check(IconLibrary.ItemUV(item)==new Rect(0,0,1,1),"no cropped card or atlas rectangle: "+name);
                Check(hashes.Add(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(png)))),"distinct rendered artwork: "+name);
                if(IconLibrary.HasSuppliedObject(item))
                {
                    var original=Array.Find(supplied.items,p=>p.item==name);
                    Check(original!=null&&BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(png))).Replace("-","").ToLowerInvariant()==original.sha256,"selected cutout PNG and item identity: "+name);
                }
                int visible=0,transparent=0,minX=256,minY=256,maxX=0,maxY=0;var pixels=texture.GetPixels32();bool border=true;
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)
                {
                    byte a=pixels[y*256+x].a;if(a==0)transparent++;
                    if(a>12){visible++;minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                    if(x<3||y<3||x>=253||y>=253)border&=a==0;
                }
                Check(visible>256*256*.05f&&transparent>256*256*.4f,"visible object with genuinely transparent surroundings: "+name);
                Check(border&&minX>2&&minY>2&&maxX<253&&maxY<253,"no clipped silhouette or filled background: "+name);
                Check(Mathf.Abs((minX+maxX)*.5f-127.5f)<26&&Mathf.Abs((minY+maxY)*.5f-127.5f)<26,"object is centered: "+name);
                if(ItemCatalog.IsAvailable(item))active++;
            }
            Check(active==116&&hashes.Count==129,"all active items and all reserved IDs have their own artwork");
            Trajectories();Rarities();return count;
        }
        static void Rarities()
        {
            var old=I18n.Language;I18n.Language=GameLanguage.Russian;
            string[] names={"РЕДКОЕ","СВЕРХРЕДКОЕ","ЭПИЧЕСКОЕ","МИФИЧЕСКОЕ","ЛЕГЕНДАРНОЕ"};
            for(int rank=0;rank<5;rank++){int rarity=ItemCatalog.RarityOrder[rank];Check(LootMotion.Rank((ItemRarity)rarity)==rank&&GameData.RarityName(rarity)==names[rank],"requested rarity naming/order: "+rank);}
            Color green=GameData.RarityColor(ItemRarity.Uncommon),blue=GameData.RarityColor(ItemRarity.Rare),gold=GameData.RarityColor(ItemRarity.Legendary);
            Check(green.g>green.r*3&&green.g>green.b*2&&blue.b>blue.r*3&&gold.r>gold.b*4,"rare green / super rare blue / legendary gold");I18n.Language=old;
        }
        static void Trajectories()
        {
            var ids=new[]{ItemId.SharpBlade,ItemId.DoubleShot,ItemId.Chronosphere,ItemId.BlackHole,ItemId.RealityCube};
            foreach(var item in ids)for(int variant=0;variant<5;variant++)
            {
                var drop=new LootState{id=100,content=(int)item,kind=RewardKind.Item,position=new Vector3(4,.55f,2),origin=new Vector3(4,0,2),launchOrigin=new Vector3(2,.9f,2),fromChest=true};
                while(LootMotion.Evaluate(drop).variant!=variant)drop.id++;
                float duration=LootMotion.Duration(GameData.Items[(int)item].rarity);
                Check(Vector3.Distance(LootMotion.Evaluate(drop,0).position,drop.launchOrigin)<.001f,"item really starts inside chest");
                float peak=0;for(int frame=0;frame<=90;frame++)
                {
                    float age=frame/90f*duration;var pose=LootMotion.Evaluate(drop,age);peak=Mathf.Max(peak,pose.position.y);
                    Check(float.IsFinite(pose.position.x)&&float.IsFinite(pose.position.y)&&float.IsFinite(pose.position.z)&&pose.scale>0&&pose.scale<=1.001f,"finite bounded launch pose: "+item+" / "+variant);
                }
                Check(peak>1.2f&&Vector3.Distance(LootMotion.Evaluate(drop,duration).position,drop.position)<.001f,"launch arc settles at authoritative loot position");
                var copy=JsonUtility.FromJson<LootState>(JsonUtility.ToJson(drop));
                Check(copy.fromChest&&copy.launchOrigin==drop.launchOrigin&&LootMotion.Evaluate(copy,.3f).position==LootMotion.Evaluate(drop,.3f).position,"clients reproduce identical trajectory from snapshot");
            }
            var sim=new Simulation(823);var p=sim.AddPlayer(0,"Collection",Hero.Ember);sim.Start();sim.State.spawnTimer=100000;sim.State.resourceNodes.Clear();sim.State.chests.Clear();p.weapons=new[]{-1,-1};
            var chest=new ChestState{id=sim.State.nextId++,position=p.position,baseCost=0,cost=0};sim.State.chests.Add(chest);Check(sim.TryOpenChest(p),"actual chest rolls an animated reward");
            var actual=sim.State.loot[0];actual.age=.16f;p.position=actual.position;Vector3 shown=LootMotion.Evaluate(actual).position;
            Check(Vector3.Dot(actual.origin-chest.position,Quaternion.Euler(0,p.yaw,0)*Vector3.back)>.8f,"drop lands toward opener instead of behind the opaque chest");
            Check(sim.TryCollectLoot(p)&&Vector3.Distance(actual.collectOrigin,shown)<.001f,"early collection starts at the visible flying object without snapping");
            for(int i=0;i<15;i++)sim.Step(1f/30,new Dictionary<int,InputFrame>());
            Check(sim.State.loot.Count==0&&p.items[chest.reward]==1,"animated reward still collects exactly once");
        }
    }
}
