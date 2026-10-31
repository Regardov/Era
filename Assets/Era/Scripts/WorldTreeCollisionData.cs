using System;
using UnityEngine;
namespace Era
{
    // Small baked 2D convex pieces of the actual trunk and tall roots. Shared by host and clients.
    public sealed class WorldTreeCollisionData:ScriptableObject
    {
        [Serializable]public sealed class Hull{public string source;public Vector2[] points;public Vector2 min,max;}
        public Hull[] hulls=Array.Empty<Hull>();
        public float height,crown;
        public static bool Near(Hull h,Vector2 p,float r)=>p.x>=h.min.x-r&&p.x<=h.max.x+r&&p.y>=h.min.y-r&&p.y<=h.max.y+r;
        static Vector2 Nearest(Hull h,Vector2 p,out bool inside,out Vector2 outward)
        {
            inside=true;float best=float.PositiveInfinity;Vector2 nearest=default;outward=default;
            for(int i=0;i<h.points.Length;i++)
            {
                var a=h.points[i];var b=h.points[(i+1)%h.points.Length];var e=b-a;
                if(e.x*(p.y-a.y)-e.y*(p.x-a.x)<0)inside=false;
                var q=a+e*Mathf.Clamp01(Vector2.Dot(p-a,e)/Mathf.Max(.000001f,e.sqrMagnitude));float d=(p-q).sqrMagnitude;
                if(d<best){best=d;nearest=q;outward=new Vector2(e.y,-e.x).normalized;}
            }
            return nearest;
        }
        public bool Contains(Vector2 p,float radius)
        {
            foreach(var h in hulls)if(Near(h,p,radius))
            {var q=Nearest(h,p,out bool inside,out _);if(inside||(p-q).sqrMagnitude<radius*radius)return true;}
            return false;
        }
        public Vector2 Resolve(Vector2 p,float radius)
        {
            const float skin=.008f;
            if(!Contains(p,radius))return p;
            var result=p;
            for(int pass=0;pass<12;pass++)
            {
                foreach(var h in hulls)if(Near(h,result,radius))
                {
                    var q=Nearest(h,result,out bool inside,out var normal);var delta=result-q;
                    if(inside||delta.sqrMagnitude<radius*radius)
                    {if(!inside&&delta.sqrMagnitude>.000001f)normal=delta.normalized;result=q+normal*(radius+skin);}
                }
                if(!Contains(result,radius))return result;
            }
            // Rare overlap recovery: select a valid outside edge, not an oscillating projection.
            float best=float.PositiveInfinity;Vector2 valid=result;
            foreach(var h in hulls)for(int i=0;i<h.points.Length;i++)
            {
                var a=h.points[i];var e=h.points[(i+1)%h.points.Length]-a;
                var q=a+e*Mathf.Clamp01(Vector2.Dot(p-a,e)/Mathf.Max(.000001f,e.sqrMagnitude));
                var candidate=q+new Vector2(e.y,-e.x).normalized*(radius+skin);float d=(candidate-p).sqrMagnitude;
                if(d<best&&!Contains(candidate,radius)){best=d;valid=candidate;}
            }
            return valid;
        }
    }
}
