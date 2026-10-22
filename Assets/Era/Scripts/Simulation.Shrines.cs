using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    public sealed partial class Simulation
    {
        void PopulateShrines()
        {
            var points=new List<Vector2>{new Vector2(-8,-7),new Vector2(21,16)};
            for(int i=0;i<10;i++){float a=i*2.399963f+.6f,r=55+Mathf.Sqrt(i/9f)*125;points.Add(new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r));}
            for(int i=0;i<points.Count;i++){var p=WorldMap.Resolve(WorldMap.OnGround(points[i].x,points[i].y),Vector3.zero,2);p.y=WorldMap.Ground(p);State.shrines.Add(new ShrineState{id=State.nextId++,style=i%3,position=p});}
        }
        void TickShrines(float dt,IDictionary<int,InputFrame> inputs)
        {
            foreach(var p in State.players)p.chargingShrine=-1;
            foreach(var shrine in State.shrines)
            {
                if(shrine.spent)continue;
                var owner=State.Player(shrine.owner);
                bool Channeling(PlayerState p)=>p!=null&&!p.downed&&!p.eliminated&&Vector3.Distance(p.position,shrine.position)<3&&inputs.TryGetValue(p.id,out var intent)&&intent.revive;
                if(!Channeling(owner)){shrine.charge=0;shrine.owner=-1;owner=null;}
                if(owner==null)
                    foreach(var p in State.players)if(p.chargingShrine<0&&Channeling(p)){owner=p;shrine.owner=p.id;break;}
                if(owner==null||owner.chargingShrine>=0)continue;
                owner.chargingShrine=shrine.id;var facing=shrine.position-owner.position;facing.y=0;if(facing.sqrMagnitude>.01f)owner.yaw=Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg;shrine.charge=Mathf.Min(5,shrine.charge+dt);
                if(shrine.charge<5)continue;
                var options=new List<int>();for(int n=0;n<8;n++)if(n!=0||owner.extraJumps<3)options.Add(n);
                shrine.buff=options[random.Next(options.Count)];shrine.spent=true;owner.chargingShrine=-1;
                ApplyBlessing(owner,shrine.buff);Effect(12,shrine.position,5);Reward(owner,RewardKind.Blessing,shrine.buff);
            }
            State.rewards.RemoveAll(n=>(n.life-=dt)<=0);
        }
        public void ApplyBlessing(PlayerState player,int kind)
        {
            if(kind<0||kind>=8)return;
            if(kind==0&&player.extraJumps>=3)return;
            player.blessings[kind]++;
            switch(kind)
            {
                case 0:player.extraJumps++;break;
                case 1:player.moveSpeed+=.15f;break;
                case 2:player.attackSpeed+=.2f;break;
                case 3:player.damage+=.25f;break;
                case 4:player.maxHp+=30;player.hp=Mathf.Min(player.maxHp,player.hp+50);break;
                case 5:player.shield=Mathf.Min(150,player.shield+50);break;
                case 6:player.cooldownRate+=.15f;break;
                case 7:player.pickupRange+=3;break;
            }
        }
        void Reward(PlayerState owner,RewardKind kind,int content,int quantity=1,int rarity=0)
        {
            State.rewards.Add(new RewardNotice{id=State.nextId++,owner=owner.id,kind=kind,content=content,quantity=quantity,rarity=rarity});
            if(State.rewards.Count>24)State.rewards.RemoveAt(0);
        }
    }
}
