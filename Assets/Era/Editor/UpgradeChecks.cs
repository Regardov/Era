using System;
using System.Linq;
using UnityEngine;
namespace Era.Editor
{
    public static class UpgradeChecks
    {
        public static int Run()
        {
            int count=0;void Check(bool value,string reason){count++;if(!value)throw new Exception("ERA upgrade layout: "+reason);}
            foreach(bool online in new[]{false,true})foreach(int offers in new[]{3,4,5})
            {
                var layout=UpgradeLayout.Create(offers,online);Check(layout.cards.Length==offers,"every offer has its own card");
                Check(layout.panel.xMin>=0&&layout.panel.yMin>=0&&layout.panel.xMax<=1600&&layout.panel.yMax<=900,"whole panel inside design canvas");
                foreach(var card in layout.cards)
                {
                    Check(card.xMin>layout.panel.xMin&&card.yMin>layout.subtitle.yMax&&card.xMax<layout.panel.xMax&&card.yMax<layout.panel.yMax,"card fully inside panel below heading");
                    if(!online)Check(!card.Overlaps(layout.footer),"footer never covers a choice");
                }
                for(int a=0;a<offers;a++)for(int b=a+1;b<offers;b++)Check(!layout.cards[a].Overlaps(layout.cards[b]),"cards do not overlap");
            }
            foreach(Hero hero in Enum.GetValues(typeof(Hero)))
            {
                int total=hero==Hero.Lumen?5:4;
                for(int choice=0;choice<total;choice++)
                {
                    var sim=new Simulation(810+choice,true);var p=sim.AddPlayer(0,"Level 25",hero);
                    sim.State.level=24;p.completedLevel=24;sim.GrantExperience(sim.State.xpRequired-sim.State.xp);
                    Check(p.offerKind==RewardKind.Weapon&&p.offers.Length==total,"actual level 25 offers all eligible weapons / "+hero);
                    int expected=p.offers[choice];Check(sim.ChooseItem(p,choice)&&p.weapons[1]==expected,"every visible choice, including fifth, equips the second slot");
                    Check(p.pendingLevels==1&&p.offerKind==RewardKind.Accessory,"weapon choice keeps the normal accessory choice");
                }
            }
            var wand=Resources.Load<GameObject>("Weapons/EchoWand");var grip=wand.GetComponent<WeaponGripPoints>();
            Check(grip!=null&&!grip.profile.twoHanded&&grip.profile.kind==WeaponKind.LightningStaff,"short one-handed wand keeps lightning mechanics");
            Check(wand.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y<.7f,"wand has a short silhouette");
            return count;
        }
    }
}
