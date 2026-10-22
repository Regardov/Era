using System.IO;
using UnityEditor;
using UnityEngine;

namespace Era.Editor
{
    public static class ActorArtBuilder
    {
        const string Marker="Assets/Era/Generated/Actors-v10.txt";
        public static void Prepare()
        {
            if(File.Exists(Marker))return;
            Directory.CreateDirectory("Assets/Era/Resources/Bosses");
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,AssetDatabase.GenerateUniqueAssetPath("Assets/Era/Generated/RealmActors-v10.asset"));
            var visual=new VisualFactory();
            foreach(Hero hero in System.Enum.GetValues(typeof(Hero)))
            {
                string path="Assets/Era/Resources/Characters/"+hero+".prefab";
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(hero==Hero.Lumen?"Assets/Era/Resources/Characters/Echo.prefab":path);
                var instance=Object.Instantiate(source);instance.name=hero.ToString();
                try
                {
                    var skeleton=instance.GetComponent<SkeletalActor>();instance.GetComponent<HeroMotion>().BindImported(hero,skeleton);
                    Paint(instance,library,GameData.HeroColor(hero),hero==Hero.Lumen);
                    if(hero==Hero.Lumen)
                    {
                        var hat=skeleton.Bone("Mage_Hat");if(hat!=null)hat.gameObject.SetActive(false);
                        var head=skeleton.Bone("head");var gold=new Color(.89f,.72f,.32f);
                        Vector3 crown=instance.transform.InverseTransformPoint(head.position)+Vector3.up*.90f;
                        var halo=visual.Ring("Lumen golden halo",instance.transform,.40f,gold,.035f);halo.transform.localPosition=crown;
                        for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;WorldVfx.Crystal(visual,instance.transform,"Halo jewel",crown+new Vector3(Mathf.Cos(a)*.4f,0,Mathf.Sin(a)*.4f),.10f,GameData.AuraColors[i]);}
                        for(int side=-1;side<=1;side+=2)visual.Rock("Ivory shoulder mantle",skeleton.spine,new Vector3(side*.39f,.28f,0),new Vector3(.25f,.19f,.28f),new Color(.86f,.82f,.70f),side+31);
                    }
                    SaveGenerated(instance,library);PrefabUtility.SaveAsPrefabAsset(instance,path);
                }finally{Object.DestroyImmediate(instance);}
            }
            for(int stage=1;stage<=CampaignData.StageCount;stage++)
            {
                EnemyKind kind=stage==2?EnemyKind.Stalker:stage==1||stage==4||stage==6?EnemyKind.Brute:EnemyKind.Warden;
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Era/Resources/Creatures/"+kind+".prefab");var instance=Object.Instantiate(source);instance.name="Boss / "+CampaignData.BossName(stage);
                try
                {
                    var creature=instance.GetComponent<CreatureMotion>();creature.kind=EnemyKind.Warden;
                    instance.transform.localScale=Vector3.one*(stage==4?2.8f:stage==2?2.15f:2.4f);
                    Paint(instance,library,CampaignData.Color(stage),false);BossArt.Decorate(visual,instance,stage);SaveGenerated(instance,library);
                    PrefabUtility.SaveAsPrefabAsset(instance,"Assets/Era/Resources/Bosses/Stage"+stage+".prefab");
                }finally{Object.DestroyImmediate(instance);}
            }
            File.WriteAllText(Marker,"Three refined skeletal heroes, ivory Lumen, seven distinct boss silhouettes.");AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static void Paint(GameObject root,WorldAssetLibrary library,Color accent,bool lumen)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.sharedMaterial==null||renderer.sharedMaterial.mainTexture==null)continue;
                var material=new Material(Shader.Find("Era/Actor Satin")){name=root.name+" / satin atlas",mainTexture=renderer.sharedMaterial.mainTexture};material.color=Color.white;
                material.SetColor("_Accent",accent);material.SetFloat("_Lumen",!lumen||renderer.name.Contains("Head")?0:renderer.name.Contains("Cape")?2:1);AssetDatabase.AddObjectToAsset(material,library);renderer.sharedMaterial=material;
                material.SetFloat("_Boss",root.name.StartsWith("Boss /")?1:0);
                if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=false;
            }
        }
        static void SaveGenerated(GameObject root,WorldAssetLibrary library)
        {
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh))AssetDatabase.AddObjectToAsset(f.sharedMesh,library);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(m!=null&&!AssetDatabase.Contains(m))AssetDatabase.AddObjectToAsset(m,library);
        }
    }
}
