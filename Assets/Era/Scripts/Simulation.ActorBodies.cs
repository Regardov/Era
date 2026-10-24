using UnityEngine;
namespace Era
{
    public sealed partial class Simulation
    {
        Vector3 MovePlayerBody(PlayerState p,Vector3 delta)=>ActorBodies.Move(State,p.position,delta,ActorBodies.Player(p));
        Vector3 MoveEnemyBody(EnemyState e,Vector3 delta)=>ActorBodies.Move(State,e.position,delta,ActorBodies.Enemy(e),e.id,false);
        void SettleBodies(float dt)
        {
            // Resolve spawn/pull contacts even for stationary, recovering and frozen creatures.
            // Enemies absorb crowd corrections; they never accumulate force on the player.
            for(int pass=0;pass<2;pass++)foreach(var e in State.enemies)
            {
                e.position=ActorBodies.Recover(State,e.position,ActorBodies.Enemy(e),e.id,true,dt*2.5f);
                float hover=1.8f+Mathf.Sin(State.elapsed*3+e.id)*.2f;
                // A diving creature stays low at impact and returns smoothly during its
                // existing recovery window; crowd correction must not cancel that pose.
                float altitude=e.attackStyle==4&&e.recovery>0?Mathf.Lerp(.7f,hover,1-e.recovery/.42f):hover;
                e.position.y=WorldMap.Ground(e.position)+(EnemyCatalog.Flying(e.kind)?altitude:0);
            }
            foreach(var p in State.players)if(!p.downed&&!p.eliminated)
                p.position=ActorBodies.Recover(State,p.position,ActorBodies.Player(p),-1,false,dt*.8f);
        }
    }
}
