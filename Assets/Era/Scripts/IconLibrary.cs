using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    public static class IconLibrary
    {
        static readonly Dictionary<string,Texture2D> Cache=new Dictionary<string,Texture2D>();
        // Exact supplied sheets. Inventory IDs remain independent of the artwork order.
        static readonly ItemId[][] Sheets={
            new[]{ItemId.SharpBlade,ItemId.SwiftBoots,ItemId.StoutHeart,ItemId.SteelShield,ItemId.Hourglass,
                ItemId.GoldenCoin,ItemId.BeginnersLuck,ItemId.HeartyMeal,ItemId.SmallPotion,ItemId.SpikedGloves,
                ItemId.WingedBoots,ItemId.TrueSight,ItemId.Magnet,ItemId.CopperSpring,ItemId.Spyglass,
                ItemId.ChalkCircle,ItemId.WindFletching,ItemId.ScholarSeal,ItemId.FeatherAnklet,ItemId.IronBrooch,
                ItemId.HealersRibbon,ItemId.DewBead,ItemId.NomadPin,ItemId.DuelistKnot,ItemId.CartographerSeal,
                ItemId.LeadPendulum,ItemId.WardensBadge,ItemId.ApprenticeCord,ItemId.BloodGlass,ItemId.CometDust},
            new[]{ItemId.MerchantScale,ItemId.SilverThread,ItemId.SteadyHand,ItemId.DoubleShot,ItemId.BookOfKnowledge,
                ItemId.FireSphere,ItemId.GlassCannon,ItemId.LeadBoots,ItemId.BloodEngine,ItemId.SniperOath,
                ItemId.BrawlerOath,ItemId.ThornMail,ItemId.FrostBell,ItemId.VenomNeedle,ItemId.SerratedCoin,
                ItemId.ThunderRod,ItemId.DashFlint,ItemId.LandingStone,ItemId.RetaliationSeal,ItemId.SoulJar,
                ItemId.PowderKeg,ItemId.HuntersDebt,ItemId.SilentVow,ItemId.LoadedDice,ItemId.HeavyArrow,
                ItemId.RicochetSeed,ItemId.FlameBlade,ItemId.IceCore,ItemId.StormAmulet,ItemId.Necronomicon},
            new[]{ItemId.Chronosphere,ItemId.ChaosPortal,ItemId.CrownOfGreed,ItemId.DragonBreath,ItemId.SteamValve,
                ItemId.StormConductor,ItemId.SepticWound,ItemId.ShatterLens,ItemId.AshGarden,ItemId.BloodCompass,
                ItemId.ExecutionLedger,ItemId.EchoChamber,ItemId.Alternator,ItemId.KineticSpool,ItemId.GlacialPrison,
                ItemId.Capacitor,ItemId.WarDrum,ItemId.HuntingPack,ItemId.MirrorShard,ItemId.OrbitalSaw,
                ItemId.Thunderstep,ItemId.SacrificialBrazier,ItemId.MiserFuse,ItemId.VoidAnchor,ItemId.PhoenixAsh,
                ItemId.PlagueDoctor,ItemId.RedThread,ItemId.SpellLoom,ItemId.FrostfireCovenant,ItemId.DuelistClock},
            new[]{ItemId.Overgrowth,ItemId.SoulTithe,ItemId.BrokenMetronome,ItemId.StarMap,ItemId.AngelWings,
                ItemId.BlackHole,ItemId.TimeMaster,ItemId.PacifistEngine,ItemId.GravePact,ItemId.TemporalDebt,
                ItemId.MobiusQuiver,ItemId.ParasiteCrown,ItemId.InversionHeart,ItemId.EventHorizon,ItemId.GlassWorld,
                ItemId.WalkingCathedral,ItemId.BloodMoon,ItemId.StormCircuit,ItemId.SplitSoul,ItemId.WinterSleep,
                ItemId.LivingArsenal,ItemId.VoidShepherd,ItemId.AlchemyEngine,ItemId.FateLoom,ItemId.HiveMind,
                ItemId.InfinitePower,ItemId.Rebirth,ItemId.RealityCube,ItemId.WorldSeed,ItemId.LastPilgrim}
        };
        // Square icon bounds exclude the card text and rarity strip.
        static readonly int[][] X={new[]{56,387,712,1024,1348},new[]{55,398,728,1048,1364},new[]{48,377,706,1026,1348},new[]{54,391,713,1030,1350}};
        static readonly int[][] Y={new[]{36,176,320,464,609,754},new[]{37,180,320,461,603,747},new[]{42,184,327,470,620,770},new[]{38,181,324,467,610,752}};
        static readonly int[] Sizes={92,95,97,97};
        static bool Cell(int item,out int sheet,out int cell)
        {
            for(sheet=0;sheet<Sheets.Length;sheet++)for(cell=0;cell<Sheets[sheet].Length;cell++)if((int)Sheets[sheet][cell]==item)return true;
            sheet=-1;cell=-1;return false;
        }
        public static Texture2D ItemAtlas=>Resources.Load<Texture2D>("UI/item-sheet-1");
        public static bool HasSuppliedItemArt(int item)=>Cell(item,out _,out _);
        static readonly Texture2D[] ItemTextures=new Texture2D[ItemCatalog.Count];
        public static readonly ItemId[] SuppliedObjects={ItemId.SharpBlade,ItemId.SteelShield,ItemId.GoldenCoin,ItemId.StoutHeart,ItemId.SpikedGloves,ItemId.WingedBoots,ItemId.Hourglass,ItemId.HeartyMeal,ItemId.SmallPotion,ItemId.BeginnersLuck};
        public static bool HasSuppliedObject(int item)=>System.Array.IndexOf(SuppliedObjects,(ItemId)item)>=0;
        public static string ItemResourcePath(int item)=>"Items/"+(HasSuppliedObject(item)?"v029/":"v025/")+(ItemId)item;
        public static Texture2D ItemTexture(int item)
        {
            if(item<0||item>=ItemCatalog.Count)return null;
            if(ItemTextures[item]==null)ItemTextures[item]=Resources.Load<Texture2D>(ItemResourcePath(item));
            return ItemTextures[item];
        }
        public static Rect ItemPixels(int item)
        {
            var texture=ItemTexture(item);return texture==null?Rect.zero:new Rect(0,0,texture.width,texture.height);
        }
        public static Rect ItemUV(int item){var rect=ItemPixels(item);var texture=ItemTexture(item);return new Rect(rect.x/texture.width,rect.y/texture.height,rect.width/texture.width,rect.height/texture.height);}
        static readonly Sprite[] ItemSprites=new Sprite[ItemCatalog.Count];
        public static Sprite ItemSprite(int item)
        {
            if(ItemSprites[item]!=null)return ItemSprites[item];
            var texture=ItemTexture(item);if(texture==null)return null;
            var rect=ItemPixels(item);
            return ItemSprites[item]=Sprite.Create(texture,rect,Vector2.one*.5f,rect.width/1.2f,0,SpriteMeshType.FullRect);
        }
        public static Texture2D Get(string name){if(!Cache.TryGetValue(name,out var texture)){texture=Resources.Load<Texture2D>("Icons/"+name);Cache[name]=texture;}return texture;}
    }
}
