using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    public struct WorldProp
    {
        public Vector3 position;
        public float size, angle;
        public int kind;
    }

    // Shared terrain and collision definitions. No client-only decorative physics affects movement.
    public static class WorldMap
    {
        public static int Stage {get;private set;}=1;
        static List<WorldProp> props;
        public static List<WorldProp> Props=>props??(props=CreateProps());
        public static readonly Vector3[] Landmarks = { new Vector3(0,0,0), new Vector3(-95,0,70), new Vector3(105,0,80), new Vector3(60,0,-110) };
        public static void SetStage(int stage)
        {stage=Mathf.Clamp(stage,1,CampaignData.StageCount);if(Stage==stage)return;Stage=stage;props=null;}
        public static float Height(float x,float z)
        {
            float height=NaturalHeight(x,z);
            for(int river=0;river<2;river++)
            {
                float width=RiverWidth(z,river),distance=Mathf.Abs(x-RiverCenter(z,river));
                if(distance>=width+3)continue;
                float water=RiverWaterHeight(z,river);
                if(distance<width)height=water-.65f*(1-distance*distance/(width*width));
                else height=Mathf.Lerp(water,height,Mathf.SmoothStep(0,1,(distance-width)/3));
            }
            return height;
        }
        static float NaturalHeight(float x,float z)
        {
            float r=Mathf.Sqrt(x*x+z*z),blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(16,39,r));
            foreach(var landmark in Landmarks)if(landmark.x!=0||landmark.z!=0)
                blend*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,17,Vector2.Distance(new Vector2(x,z),new Vector2(landmark.x,landmark.z))));
            float broad=(Mathf.PerlinNoise(x*.012f+Stage*7.31f,z*.012f+91)-.45f)*(10+Stage*1.7f);
            float ridge=Mathf.Sin(x*.022f+Stage*.8f)*Mathf.Cos(z*.019f-Stage*.37f)*(2.2f+Stage*.35f);
            float detail=(Mathf.PerlinNoise(x*.046f+19,z*.046f+Stage*2)-.5f)*1.2f;
            float knolls=Mathf.Sin(x*.052f+Stage)*Mathf.Sin(z*.041f)*1.5f;
            return (broad+ridge+detail+knolls)*blend;
        }
        public static float RiverCenter(float z,int river)=>river==0?48+Mathf.Sin(z*.019f+Stage*.3f)*16+Mathf.Sin(z*.043f)*4:-96+Mathf.Sin(z*.016f-Stage*.5f)*21;
        public static float RiverWidth(float z,int river)=>(river==0?3.2f:2.1f)+Mathf.Sin(z*.027f+Stage)*.35f;
        public static float RiverWaterHeight(float z,int river)=>NaturalHeight(RiverCenter(z,river),z)-.12f;
        public static bool InWater(float x,float z,float margin=0)
        {for(int river=0;river<2;river++)if(Mathf.Abs(x-RiverCenter(z,river))<RiverWidth(z,river)+margin)return true;return false;}
        public static float Ground(Vector3 position) => Height(position.x, position.z);
        public static Vector3 OnGround(float x, float z) => new Vector3(x, Height(x,z), z);
        public static bool ClearLocation(Vector3 point,float clearance)
        {
            if(new Vector2(point.x,point.z).magnitude>GameData.ArenaRadius-clearance-8||InWater(point.x,point.z,clearance))return false;
            foreach(var prop in Props)
                if(prop.kind!=2&&Vector2.Distance(new Vector2(point.x,point.z),new Vector2(prop.position.x,prop.position.z))<(prop.kind==0?.48f:.85f)*prop.size+clearance)return false;
            for(int i=0;i<8;i++)
            {float a=i*Mathf.PI/4;if(Mathf.Abs(Height(point.x+Mathf.Cos(a)*clearance,point.z+Mathf.Sin(a)*clearance)-point.y)>clearance*.22f)return false;}
            return true;
        }
        public static Vector3 RandomClearLocation(System.Random random,float clearance,Vector3 avoid,float separation)
        {
            for(int attempt=0;attempt<1024;attempt++)
            {
                float a=(float)random.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt((float)random.NextDouble())*(GameData.ArenaRadius-32);
                var point=OnGround(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
                if(Vector3.Distance(point,avoid)>=separation&&ClearLocation(point,clearance))return point;
            }
            // The sanctuary is authored flat and clear; retain a safe fallback for future terrain.
            foreach(var point in Landmarks)if(Vector3.Distance(point,avoid)>=separation&&ClearLocation(OnGround(point.x,point.z),clearance))return OnGround(point.x,point.z);
            return OnGround(0,0);
        }
        public static string Region(Vector3 p)
        {
            if (p.sqrMagnitude < 38*38) return "THE WAKING SANCTUARY";
            if (p.x < -45) return "LAVENDER GROVE";
            if (p.z < -50) return "AMBERWILDS";
            if (p.x > 50) return "THE FALLEN GARDENS";
            return "MOSS LIGHT MEADOWS";
        }
        static List<WorldProp> CreateProps()
        {
            var result = new List<WorldProp>(); var random = new System.Random(91247+Stage*513);
            for (int i = 0; i < 560; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2;
                float r = 24 + Mathf.Sqrt((float)random.NextDouble()) * (GameData.ArenaRadius-40);
                var p = OnGround(Mathf.Sin(a)*r, Mathf.Cos(a)*r);
                if(InWater(p.x,p.z,1.3f))continue;
                int kind = i < 240 ? 0 : i<350?1:2;
                float size = kind == 0 ? .85f+(float)random.NextDouble()*1.05f : kind==1?.7f+(float)random.NextDouble()*1.8f:.65f+(float)random.NextDouble()*.8f;
                if(kind==0&&i%9==0)size*=1.65f;
                result.Add(new WorldProp { position = p, size = size, kind = kind, angle = (float)random.NextDouble()*360 });
            }
            return result;
        }
        public static Vector3 Resolve(Vector3 from, Vector3 delta, float radius)
        {
            Vector3 next = from + delta;
            var planar = Vector2.ClampMagnitude(new Vector2(next.x,next.z), GameData.ArenaRadius-radius-6);
            next.x=planar.x; next.z=planar.y;
            for (int pass=0; pass<2; pass++)
            foreach (var prop in Props)
            {
                if(prop.kind==2)continue; // Low shrubs bend visually and do not trap movement.
                float extent = (prop.kind == 0 ? 0.48f : 0.85f)*prop.size + radius;
                float dx=next.x-prop.position.x, dz=next.z-prop.position.z;
                float sq=dx*dx+dz*dz;
                if (sq>=extent*extent) continue;
                if (sq<0.0001f) { dx=1; dz=0; sq=1; }
                float scale=extent/Mathf.Sqrt(sq); next.x=prop.position.x+dx*scale; next.z=prop.position.z+dz*scale;
            }
            var inside=Vector2.ClampMagnitude(new Vector2(next.x,next.z),GameData.ArenaRadius-radius-6);next.x=inside.x;next.z=inside.y;
            return next;
        }
        public static void PopulateChests(WorldState state)
        {
            var positions = new List<Vector2> { new Vector2(6,2), new Vector2(-11,10), new Vector2(16,-12) };
            for(int i=0;i<GameData.ChestCount-3;i++) { float a=i*2.399963f; float r=35+Mathf.Sqrt(i/(float)(GameData.ChestCount-4))*(GameData.ArenaRadius-60); positions.Add(new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r)); }
            for(int i=0;i<positions.Count;i++)
            {
                var v=positions[i];if(i>2){float a=(state.stage-1)*.63f;v=new Vector2(v.x*Mathf.Cos(a)-v.y*Mathf.Sin(a),v.x*Mathf.Sin(a)+v.y*Mathf.Cos(a));} var p=Resolve(OnGround(v.x,v.y),Vector3.zero,1);
                p.y=Ground(p); int tier=i>4 && i%4==0 ? 1 : 0;
                int baseCost=Mathf.RoundToInt((tier==1?30:10+(i%5)*2)*(1+.10f*(state.stage-1)));
                state.chests.Add(new ChestState { id=state.nextId++,position=p,baseCost=baseCost,cost=GameData.ChestPrice(baseCost,state.openedChests),tier=tier });
            }
        }
    }
}
