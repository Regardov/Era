using System.IO;
using UnityEditor;
using UnityEngine;
namespace Era.Editor
{
    public static class FeedbackPrefabBuilder
    {
        const string Folder="Assets/Era/Resources/Feedback",Stamp="Assets/Era/Generated/CombatFeedback-v21.txt";
        public static void Prepare()
        {
            if(!File.Exists(Folder+"/Flame.mat")){Directory.CreateDirectory(Folder);AssetDatabase.CreateAsset(new Material(Shader.Find("Era/Living Flame")){name="Living flame"},Folder+"/Flame.mat");}
            if(File.Exists(Stamp)&&File.Exists(Folder+"/CombatParticle.prefab")&&File.Exists(Folder+"/SmokeParticle.prefab"))
            {
                foreach(string name in new[]{"CombatParticle","SmokeParticle"})
                {var particle=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".prefab").GetComponent<ParticleSystem>();var main=particle.main;main.startSize3D=name=="CombatParticle";main.startRotation3D=name=="CombatParticle";main.maxParticles=10;PresentationFx.ConfigureCurves(particle,name=="SmokeParticle");EditorUtility.SetDirty(particle);}AssetDatabase.SaveAssets();return;
            }
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var library=ScriptableObject.CreateInstance<WorldAssetLibrary>();AssetDatabase.CreateAsset(library,"Assets/Era/Generated/CombatFeedback-v21.asset");
            foreach(var mesh in new[]{FeedbackGeometry.Shard,FeedbackGeometry.Cross,FeedbackGeometry.Orb,FeedbackGeometry.Shield,FeedbackGeometry.Saw})if(!AssetDatabase.Contains(mesh))AssetDatabase.AddObjectToAsset(mesh,library);
            var smoke=FeedbackGeometry.Mist;if(smoke.mainTexture!=null&&!AssetDatabase.Contains(smoke.mainTexture))AssetDatabase.AddObjectToAsset(smoke.mainTexture,library);
            var particles=new Material(FeedbackGeometry.Solid){name="Smooth low-poly feedback"};var mist=new Material(smoke){name="Soft smoke particles"};var shield=new Material(FeedbackGeometry.Barrier){name="Reactive protective field"};
            AssetDatabase.CreateAsset(particles,Folder+"/Particles.mat");AssetDatabase.CreateAsset(mist,Folder+"/Smoke.mat");AssetDatabase.CreateAsset(shield,Folder+"/Shield.mat");
            var temp=new GameObject("Feedback prefab authoring");
            try
            {
                for(int i=0;i<2;i++)
                {
                    bool isSmoke=i==1;var ps=PresentationFx.CreateParticleTemplate(temp.transform,isSmoke);ps.GetComponent<Renderer>().sharedMaterial=isSmoke?mist:particles;
                    PrefabUtility.SaveAsPrefabAsset(ps.gameObject,Folder+(isSmoke?"/SmokeParticle.prefab":"/CombatParticle.prefab"));
                }
                AssetDatabase.SaveAssets();File.WriteAllText(Stamp,"v21 / shared low-poly meshes, 18+10 pooled particles, soft smoke and reactive barrier\n");AssetDatabase.ImportAsset(Stamp);Debug.Log("ERA_FEEDBACK_PREFABS_READY");
            }
            finally{Object.DestroyImmediate(temp);}
        }
    }
}
