using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    // Lives beside WorldEnvironment: that class owns and destroys its procedural meshes.
    public sealed class WorldTreeView:MonoBehaviour
    {
        public const int WoodLayer=28, FoliageLayer=29;
        public int PresentedStage{get;private set;}=-1;
        public Bounds ModelBounds{get;private set;}
        public LODGroup Levels{get;private set;}
        sealed class RootSkin{public MeshFilter filter;public Mesh original,copy;public Vector3[] source;}
        readonly List<RootSkin> roots=new List<RootSkin>();
        GameObject model;
        public static bool IsFoliage(string name)=>name.Contains("Foliage")||name.Contains("LeafSilhouette")||name.Contains("PendantLeaves")||name.Contains("RootMoss");
        public void Present(int stage,bool visible)
        {
            if(model==null)
            {
                var prefab=Resources.Load<GameObject>("Environment/Yggdrasil");
                if(prefab==null){Debug.LogError("Missing Yggdrasil runtime prefab");enabled=false;return;}
                model=Instantiate(prefab,transform,false);model.name="Yggdrasil / centre landmark";
                Levels=model.GetComponent<LODGroup>();
                foreach(var f in model.GetComponentsInChildren<MeshFilter>(true))
                    if(f.name.StartsWith("YGG_Root_")&&f.sharedMesh!=null)
                    {
                        var copy=Instantiate(f.sharedMesh);copy.name=f.sharedMesh.name+" / terrain fitted instance";
                        roots.Add(new RootSkin{filter=f,original=f.sharedMesh,copy=copy,source=f.sharedMesh.vertices});f.sharedMesh=copy;
                    }
            }
            model.SetActive(visible);
            if(PresentedStage==stage)return;
            PresentedStage=stage;transform.position=WorldMap.OnGround(0,0);
            foreach(var root in roots)
            {
                var vertices=new Vector3[root.source.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=root.filter.transform.TransformPoint(root.source[i]);
                    float r=new Vector2(p.x,p.z).magnitude;
                    // Outside the solid trunk, roots become low, ground-hugging ridges.
                    float compress=Mathf.Lerp(1,.20f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(WorldTreeLayout.TrunkRadius*.873f,WorldTreeLayout.TrunkRadius*1.636f,r)));
                    p.y=transform.position.y+(p.y-transform.position.y)*compress+WorldMap.Ground(p)-transform.position.y-.035f;
                    vertices[i]=root.filter.transform.InverseTransformPoint(p);
                }
                root.copy.vertices=vertices;root.copy.RecalculateNormals();root.copy.RecalculateBounds();
            }
            Levels.RecalculateBounds();
            bool first=true;var bounds=new Bounds();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
            ModelBounds=bounds;
        }
        void OnDestroy(){foreach(var root in roots)if(root.copy!=null)Destroy(root.copy);}
    }
}
