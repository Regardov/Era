using System;
using UnityEngine;
namespace Era
{
    // Describes presentation, never damage. None means a stat modifier, not a hidden proc.
    public enum FeedbackStyle { None,Impact,Blade,Critical,Fire,Frost,Poison,Lightning,Heal,Shield,Speed,Dust,Pickup,Summon,Death,Steam,Void,Root,Meteor,Echo,Blood,Mark,Holy,Gold,Arcane }
    public static class MechanicFeedback
    {
        static readonly FeedbackStyle[] styles=new FeedbackStyle[ItemCatalog.Count];
        static readonly bool[] covered=new bool[ItemCatalog.Count];
        static MechanicFeedback()
        {
            Set(FeedbackStyle.None,ItemId.SharpBlade,ItemId.StoutHeart,ItemId.Hourglass,ItemId.GoldenCoin,ItemId.BeginnersLuck,ItemId.SmallPotion,ItemId.DoubleShot,ItemId.SpikedGloves,ItemId.TrueSight,ItemId.Magnet,ItemId.InfinitePower,ItemId.CopperSpring,ItemId.Spyglass,ItemId.ChalkCircle,ItemId.WindFletching,ItemId.ScholarSeal,ItemId.FeatherAnklet,ItemId.IronBrooch,ItemId.DuelistKnot,ItemId.CartographerSeal,ItemId.LeadPendulum,ItemId.ApprenticeCord,ItemId.CometDust,ItemId.MerchantScale,ItemId.SilverThread,ItemId.SteadyHand,ItemId.GlassCannon,ItemId.LeadBoots,ItemId.SniperOath,ItemId.BrawlerOath,ItemId.HuntersDebt,ItemId.SilentVow,ItemId.LoadedDice,ItemId.HeavyArrow,ItemId.RicochetSeed,ItemId.SepticWound,ItemId.WarDrum,ItemId.DuelistClock,ItemId.LivingArsenal,ItemId.DebtOfKing,ItemId.Chronicle);
            Set(FeedbackStyle.Speed,ItemId.SwiftBoots,ItemId.WingedBoots,ItemId.NomadPin);
            Set(FeedbackStyle.Heal,ItemId.HeartyMeal,ItemId.HealersRibbon,ItemId.DewBead,ItemId.SoulJar,ItemId.BloodCompass,ItemId.Rebirth,ItemId.ImmortalContract);
            Set(FeedbackStyle.Shield,ItemId.SteelShield,ItemId.WardensBadge,ItemId.RetaliationSeal,ItemId.BrokenMetronome);
            Set(FeedbackStyle.Pickup,ItemId.BookOfKnowledge);
            Set(FeedbackStyle.Fire,ItemId.FireSphere,ItemId.FlameBlade,ItemId.DragonBreath,ItemId.DashFlint,ItemId.AshGarden,ItemId.SacrificialBrazier,ItemId.PhoenixAsh);
            Set(FeedbackStyle.Frost,ItemId.IceCore,ItemId.Chronosphere,ItemId.FrostBell,ItemId.ShatterLens,ItemId.GlacialPrison,ItemId.WinterSleep);
            Set(FeedbackStyle.Poison,ItemId.VenomNeedle,ItemId.PlagueDoctor,ItemId.HiveMind);
            Set(FeedbackStyle.Lightning,ItemId.StormAmulet,ItemId.ThunderRod,ItemId.StormConductor,ItemId.Alternator,ItemId.Capacitor,ItemId.Thunderstep,ItemId.InversionHeart,ItemId.StormCircuit);
            Set(FeedbackStyle.Summon,ItemId.Necronomicon,ItemId.HuntingPack,ItemId.GravePact,ItemId.ArchitectReign);
            Set(FeedbackStyle.Void,ItemId.ChaosPortal,ItemId.BlackHole,ItemId.VoidAnchor,ItemId.EventHorizon,ItemId.VoidShepherd,ItemId.SingularityTestament);
            Set(FeedbackStyle.Echo,ItemId.TimeMaster,ItemId.EchoChamber,ItemId.TemporalDebt,ItemId.SplitSoul,ItemId.EndlessRefrain);
            Set(FeedbackStyle.Blood,ItemId.BloodGlass,ItemId.BloodEngine,ItemId.SerratedCoin,ItemId.RedThread,ItemId.SoulTithe,ItemId.ParasiteCrown,ItemId.BloodMoon);
            Set(FeedbackStyle.Holy,ItemId.AngelWings,ItemId.WalkingCathedral,ItemId.Ouroboros,ItemId.SevenfoldOath);
            Set(FeedbackStyle.Arcane,ItemId.RealityCube,ItemId.SpellLoom,ItemId.AlchemyEngine,ItemId.EclipseCrown,ItemId.KineticSpool);
            Set(FeedbackStyle.Gold,ItemId.CrownOfGreed);
            Set(FeedbackStyle.Impact,ItemId.ThornMail,ItemId.LandingStone,ItemId.LastPilgrim);
            Set(FeedbackStyle.Meteor,ItemId.PowderKeg,ItemId.MiserFuse,ItemId.PacifistEngine,ItemId.StarMap);
            Set(FeedbackStyle.Steam,ItemId.SteamValve,ItemId.FrostfireCovenant);
            Set(FeedbackStyle.Blade,ItemId.MirrorShard,ItemId.OrbitalSaw,ItemId.MobiusQuiver,ItemId.GlassWorld);
            Set(FeedbackStyle.Root,ItemId.Overgrowth,ItemId.WorldSeed);
            Set(FeedbackStyle.Mark,ItemId.ExecutionLedger,ItemId.FateLoom);
            for(int i=0;i<covered.Length;i++)if(!covered[i])throw new InvalidOperationException("Unreviewed item presentation: "+(ItemId)i);
        }
        static void Set(FeedbackStyle style,params ItemId[] ids){foreach(var id in ids){if(covered[(int)id])throw new InvalidOperationException("Duplicate item presentation: "+id);styles[(int)id]=style;covered[(int)id]=true;}}
        public static FeedbackStyle Item(ItemId item)=>styles[(int)item];
        public static bool Reviewed(int item)=>item>=0&&item<covered.Length&&covered[item];
        public static FeedbackStyle Status(Ailment ailment)=>ailment==Ailment.Burn?FeedbackStyle.Fire:ailment==Ailment.Poison?FeedbackStyle.Poison:ailment==Ailment.Bleed?FeedbackStyle.Blood:ailment==Ailment.Chill?FeedbackStyle.Frost:ailment==Ailment.Shock?FeedbackStyle.Lightning:FeedbackStyle.Mark;
        public static FeedbackStyle Hazard(int stage)=>stage==4?FeedbackStyle.Fire:stage==3?FeedbackStyle.Frost:stage==5?FeedbackStyle.Poison:stage==6?FeedbackStyle.Lightning:stage==7?FeedbackStyle.Void:FeedbackStyle.Impact;
        public static FeedbackStyle Style(EffectState e)
        {
            if(e.style!=FeedbackStyle.None)return e.style;
            if(e.kind>=9&&e.kind<=11)return e.weapon==WeaponKind.LightningStaff?FeedbackStyle.Lightning:FeedbackStyle.Blade;
            return e.kind==0?FeedbackStyle.Holy:e.kind==1?FeedbackStyle.Frost:e.kind==2?FeedbackStyle.Speed:e.kind==3?FeedbackStyle.Heal:e.kind==4?FeedbackStyle.Fire:e.kind==5?FeedbackStyle.Impact:e.kind==6?FeedbackStyle.Death:e.kind==7?FeedbackStyle.Pickup:FeedbackStyle.Summon;
        }
        public static Color Color(FeedbackStyle style)
        {
            switch(style)
            {
                case FeedbackStyle.Fire:return new Color(1,.32f,.045f);case FeedbackStyle.Frost:return new Color(.46f,.83f,1);
                case FeedbackStyle.Poison:return new Color(.58f,.92f,.17f);case FeedbackStyle.Root:return new Color(.38f,.73f,.13f);case FeedbackStyle.Lightning:return new Color(.64f,.82f,1);
                case FeedbackStyle.Heal:return new Color(.41f,1,.65f);case FeedbackStyle.Shield:return new Color(.35f,.68f,1);
                case FeedbackStyle.Speed:return new Color(.77f,.90f,.90f);case FeedbackStyle.Void:return new Color(.32f,.20f,.51f);
                case FeedbackStyle.Blood:return new Color(.65f,.14f,.17f);case FeedbackStyle.Echo:return new Color(.43f,.71f,.77f);
                case FeedbackStyle.Steam:return new Color(.78f,.82f,.79f);case FeedbackStyle.Holy:case FeedbackStyle.Gold:return new Color(1,.81f,.36f);
                case FeedbackStyle.Mark:return new Color(.88f,.44f,.26f);case FeedbackStyle.Critical:return new Color(1,.97f,.72f);
                default:return new Color(.76f,.77f,.66f);
            }
        }
    }
}
