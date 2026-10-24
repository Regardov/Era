using UnityEngine;
namespace Era
{
    // The same capsule definition drives the host solver and Unity query colliders.
    public readonly struct ActorCapsule
    {
        public readonly float radius,height;
        public ActorCapsule(float radius,float height){this.radius=radius;this.height=Mathf.Max(height,radius*2);}
        public float Lower(Vector3 p)=>p.y+radius;
        public float Upper(Vector3 p)=>p.y+height-radius;
    }
    public static class ActorBodies
    {
        public const float Skin=.006f;
        public static ActorCapsule Player(PlayerState p)=>new ActorCapsule(.4f,p.crouching?1.88f:2.23f);
        public static ActorCapsule Enemy(EnemyState e)
        {
            float radius=EnemyCatalog.Radius(e.kind)*.82f;
            float height=e.kind==EnemyKind.Warden?4.6f:e.kind==EnemyKind.Ogre||e.kind==EnemyKind.Brute||e.kind==EnemyKind.OgreShaman?2.55f:e.kind==EnemyKind.StoneGolem||e.kind==EnemyKind.CrystalGolem?2.5f:e.kind==EnemyKind.Slime||e.kind==EnemyKind.AcidSlime?1.18f:e.kind==EnemyKind.Bat?.8f:e.kind==EnemyKind.Gargoyle?1.85f:2.22f;
            float scale=e.eliteShrine>=0?1.2f:1;if(e.kind==EnemyKind.Slime&&e.summonGeneration>0)scale*=.6f;
            return new ActorCapsule(radius*scale,height*scale);
        }
        public static float PlanarRadius(ActorCapsule a,Vector3 p,ActorCapsule b,Vector3 q)
        {
            float gap=Mathf.Max(0,Mathf.Max(a.Lower(p)-b.Upper(q),b.Lower(q)-a.Upper(p))),r=a.radius+b.radius;
            return gap>=r?0:Mathf.Sqrt(Mathf.Max(0,r*r-gap*gap));
        }
        public static bool Overlap(ActorCapsule a,Vector3 p,ActorCapsule b,Vector3 q,float tolerance=0)
        {float r=PlanarRadius(a,p,b,q)-tolerance;return r>0&&new Vector2(p.x-q.x,p.z-q.z).sqrMagnitude<r*r;}
        public static Vector3 Move(WorldState state,Vector3 from,Vector3 delta,ActorCapsule shape,int ignoreEnemy=-1,bool ignorePlayers=true)
        {
            delta.y=0;var position=from;int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.18f));Vector3 step=delta/steps;
            for(int n=0;n<steps;n++)
            {
                Vector3 remaining=WorldMap.Resolve(position,step,shape.radius)-position;remaining.y=0;
                for(int slide=0;slide<3&&remaining.sqrMagnitude>.0000001f;slide++)
                {
                    float earliest=1;Vector3 normal=Vector3.zero;
                    foreach(var e in state.enemies)if(e.id!=ignoreEnemy&&e.hp>0)Sweep(shape,position,remaining,Enemy(e),e.position,ref earliest,ref normal);
                    if(!ignorePlayers)foreach(var p in state.players)if(!p.eliminated&&!p.downed)Sweep(shape,position,remaining,Player(p),p.position,ref earliest,ref normal);
                    position+=remaining*(earliest<1?Mathf.Max(0,earliest-.0001f):1);
                    if(earliest>=1)break;
                    remaining*=1-earliest;float inward=Vector3.Dot(remaining,normal);if(inward<0)remaining-=normal*inward;
                }
                // A wall correction is also subject to body contacts on the next substep.
                var bounded=WorldMap.Resolve(position,Vector3.zero,shape.radius);bounded.y=position.y;position=bounded;
            }
            return position;
        }
        static void Sweep(ActorCapsule a,Vector3 p,Vector3 delta,ActorCapsule b,Vector3 q,ref float time,ref Vector3 normal)
        {
            float radius=PlanarRadius(a,p,b,q);if(radius<=0)return;radius+=Skin;
            var offset=new Vector2(p.x-q.x,p.z-q.z);var move=new Vector2(delta.x,delta.z);float inward=Vector2.Dot(offset,move);
            if(inward>=0)return;float length=move.sqrMagnitude;if(length<.00000001f)return;
            float c=offset.sqrMagnitude-radius*radius,t;
            if(c<=0)t=0;
            else {float discriminant=inward*inward-length*c;if(discriminant<0)return;t=(-inward-Mathf.Sqrt(discriminant))/length;}
            if(t<0||t>time)return;var contact=offset+move*t;if(contact.sqrMagnitude<.000001f)return;
            time=t;normal=new Vector3(contact.x,0,contact.y).normalized;
        }
        public static float Support(WorldState state,PlayerState player,Vector3 position,float previousFeet,float tolerance=.04f)
        {
            var a=Player(player);float ground=WorldMap.Ground(position);
            foreach(var e in state.enemies)if(e.hp>0)
            {
                var b=Enemy(e);float planar=new Vector2(position.x-e.position.x,position.z-e.position.z).sqrMagnitude,r=a.radius+b.radius;
                if(planar>=r*r)continue;float top=b.Upper(e.position)+Mathf.Sqrt(r*r-planar)-a.radius;
                if(previousFeet>=top-tolerance)ground=Mathf.Max(ground,top+Skin);
            }
            return ground;
        }
        public static float Ceiling(WorldState state,PlayerState player,float nextFeet)
        {
            var a=Player(player);float limit=nextFeet;
            foreach(var e in state.enemies)if(e.hp>0)
            {
                var b=Enemy(e);float sq=new Vector2(player.position.x-e.position.x,player.position.z-e.position.z).sqrMagnitude,r=a.radius+b.radius;
                if(sq>=r*r)continue;float bottom=b.Lower(e.position)-Mathf.Sqrt(r*r-sq)-a.height+a.radius-Skin;
                if(player.position.y<=bottom+.02f&&nextFeet>bottom)limit=Mathf.Min(limit,bottom);
            }
            return limit;
        }
        public static Vector3 Recover(WorldState state,Vector3 position,ActorCapsule shape,int id,bool enemy,float maxCorrection)
        {
            for(int pass=0;pass<4;pass++)
            {
                Vector3 correction=Vector3.zero;
                foreach(var e in state.enemies)if(e.id!=id&&e.hp>0)
                {
                    var d=position-e.position;d.y=0;float r=PlanarRadius(shape,position,Enemy(e),e.position)+Skin;
                    if(r<=Skin||d.sqrMagnitude>=r*r)continue;
                    if(d.sqrMagnitude<.000001f)d=Quaternion.Euler(0,(id*137+e.id*31)%360,0)*Vector3.forward*.001f;
                    correction+=d.normalized*(r-d.magnitude)*(enemy?.52f:1);
                }
                if(enemy)foreach(var p in state.players)if(!p.eliminated&&!p.downed)
                {
                    var d=position-p.position;d.y=0;float r=PlanarRadius(shape,position,Player(p),p.position)+Skin;
                    if(r<=Skin||d.sqrMagnitude>=r*r)continue;if(d.sqrMagnitude<.000001f)d=Quaternion.Euler(0,id*137,0)*Vector3.forward*.001f;
                    correction+=d.normalized*(r-d.magnitude);
                }
                if(correction.sqrMagnitude<.0000001f)break;
                var next=WorldMap.Resolve(position,Vector3.ClampMagnitude(correction,maxCorrection/4),shape.radius);next.y=position.y;position=next;
            }
            return position;
        }
    }
}
