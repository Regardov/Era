using System;
using System.Linq;
using UnityEngine;

namespace Era.Editor
{
    public static class AccessoryChecks
    {
        public static int Run()
        {
            int count=0;
            void Check(bool condition,string message){count++;if(!condition)throw new Exception("ERA accessory check failed: "+message);}
            foreach(Hero hero in Enum.GetValues(typeof(Hero)))
            {
                var sim=new Simulation(371+(int)hero,true);var player=sim.AddPlayer(0,"Jewelry",hero);
                Check(player.accessorySlots.Length==4&&player.accessorySlots.All(s=>s==-1),"four initially empty slots / "+hero);
                int[] build={2,4,8,12};
                foreach(int stat in build)Check(sim.ApplyAccessory(player,stat,1),"initial accessory accepted / "+stat);
                Check(player.accessorySlots.SequenceEqual(build)&&player.accessoryLevels.All(l=>l==1),"acquisitions occupy slots in selection order");
                float critical=player.critChance;
                Check(sim.ApplyAccessory(player,2,3)&&Mathf.Approximately(player.critChance-critical,GameData.Attributes[2].values[3]),"existing ring improves its actual stat");
                Check(player.accessoryLevels[0]==2&&player.accessoryRarities[0]==3,"ring rank increases in the same slot");
                Check(sim.ApplyAccessory(player,2,1)&&player.accessoryRarities[0]==3,"lower-tier increment never downgrades equipped rarity");
                int rewards=sim.State.rewards.Count,magnet=player.attributes[17];
                Check(!sim.ApplyAccessory(player,17,3)&&player.attributes[17]==magnet&&sim.State.rewards.Count==rewards,"fifth type cannot grant stats or consume a reward");
                Check(!sim.ApplyAccessory(player,-1,0)&&!sim.ApplyAccessory(player,21,0),"invalid catalogue IDs are rejected");
                int before=player.accessoryLevels.Sum(),accessoryChoices=0,auraChoices=0;
                for(int level=0;level<120;level++)
                {
                    sim.GrantExperience(sim.State.xpRequired-sim.State.xp);
                    int guard=0;
                    while(player.pendingLevels>0&&guard++<4)
                    {
                        int selected=0;
                        if(player.offerKind==RewardKind.Accessory)
                        {
                            Check(player.offers.Length==3&&player.offers.Distinct().Count()==3&&player.offers.All(build.Contains),"only three distinct equipped types offered after capacity / "+level);
                            accessoryChoices++;
                        }
                        else if(player.offerKind==RewardKind.Aura)
                        {
                            selected=Mathf.Max(0,Array.FindIndex(player.offers,a=>player.auraLevels[a]==0));auraChoices++;
                            if(player.auraLevels.Count(a=>a>0)==3)Check(player.offers.All(a=>player.auraLevels[a]>0),"aura cap offers only upgrades");
                        }
                        Check(sim.ChooseItem(player,selected),"legitimate queued choice is accepted / "+level);
                    }
                    Check(player.pendingLevels==0&&player.accessorySlots.SequenceEqual(build),"level completion preserves all four slot identities / "+level);
                }
                Check(accessoryChoices==120&&player.accessoryLevels.Sum()==before+120,"every level awards one rank; weapon milestone does not consume it");
                Check(player.accessoryLevels.Max()>5,"accessory levels continue beyond weapon level cap");
                Check(hero!=Hero.Lumen||(auraChoices==120&&player.auraLevels.Count(a=>a>0)==3),"Lumen receives one aura decision each level and owns exactly three");
                var copy=JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(sim.State)).Player(0);
                Check(copy.accessorySlots.SequenceEqual(player.accessorySlots)&&copy.accessoryLevels.SequenceEqual(player.accessoryLevels)&&copy.accessoryRarities.SequenceEqual(player.accessoryRarities),"network snapshot retains identities, ranks and rarity");
                var fresh=new Simulation(371,true).AddPlayer(0,"Next run",hero);
                Check(fresh.accessorySlots.All(s=>s==-1)&&fresh.accessoryLevels.All(l=>l==0),"a new run resets the build");
            }
            return count;
        }
    }
}
