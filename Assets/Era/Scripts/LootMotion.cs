using UnityEngine;

namespace Era
{
    public struct LootPose
    {
        public Vector3 position;
        public float scale,roll,glow;
        public int rank,variant;
    }
    // The trajectory is derived from replicated age, ID and chest position, never local randomness.
    public static class LootMotion
    {
        public static int Rank(ItemRarity rarity)=>rarity==ItemRarity.Uncommon?0:rarity==ItemRarity.Rare?1:rarity==ItemRarity.Epic?2:rarity==ItemRarity.Mythic?3:4;
        public static ItemRarity Rarity(LootState drop)=>drop.kind==RewardKind.Item?GameData.Items[Mathf.Clamp(drop.content,0,ItemCatalog.Count-1)].rarity:ItemRarity.Rare;
        public static float Duration(ItemRarity rarity)=>.68f+.16f*Rank(rarity);
        public static LootPose Evaluate(LootState drop,float age=-1)
        {
            int rank=Rank(Rarity(drop)),variant=(drop.id%5+drop.content%5+10)%5;
            float time=Mathf.Max(0,age<0?drop.age:age),t=Mathf.Clamp01(time/Duration(Rarity(drop)));
            var pose=new LootPose{position=drop.position,scale=1,roll=Mathf.Sin(time*1.9f+drop.id)*3,glow=.78f+Mathf.Sin(time*2.6f+drop.id)*.12f,rank=rank,variant=variant};
            if(drop.collecting)
            {float collect=Mathf.Clamp01(drop.collectTime/.35f);pose.scale=Mathf.Lerp(1,.16f,collect*collect);pose.roll=collect*130;pose.glow=1-collect;return pose;}
            if(!drop.fromChest||t>=1)return pose;
            float eased=t*t*(3-2*t);Vector3 delta=drop.position-drop.launchOrigin;delta.y=0;
            Vector3 side=delta.sqrMagnitude>.001f?Vector3.Cross(delta.normalized,Vector3.up):Vector3.right;
            float arc=Mathf.Sin(t*Mathf.PI),height=.7f+rank*.22f;
            pose.position=Vector3.Lerp(drop.launchOrigin,drop.position,eased)+Vector3.up*arc*height;
            switch(variant)
            {
                case 0:pose.roll=Mathf.Sin(t*Mathf.PI)*-38;pose.position+=Vector3.up*Mathf.Abs(Mathf.Sin(t*Mathf.PI*2))*(1-t)*.18f;break;
                case 1:pose.position+=side*Mathf.Sin(t*Mathf.PI*2)*arc*.42f;pose.roll=Mathf.Sin(t*Mathf.PI*2)*32;break;
                case 2:float rise=Mathf.Clamp01((t-.2f)/.8f);pose.position=Vector3.Lerp(drop.launchOrigin,drop.position,rise*rise*(3-2*rise))+Vector3.up*arc*(height+.35f);pose.roll=Mathf.Sin(t*Mathf.PI)*18;break;
                case 3:pose.position+=side*Mathf.Sin(t*Mathf.PI)*.36f;pose.roll=(1-t)*-170;break;
                case 4:pose.position+=side*Mathf.Sin(t*Mathf.PI*3)*arc*.27f;pose.roll=Mathf.Sin(t*Mathf.PI*2)*-28;break;
            }
            pose.scale=Mathf.Lerp(.24f,1,1-Mathf.Pow(1-Mathf.Clamp01(t*3.4f),3));
            pose.glow=1.1f+arc*(.25f+rank*.12f);return pose;
        }
    }
}
