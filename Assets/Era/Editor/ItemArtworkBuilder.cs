using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Era.Editor
{
    // Original editable vector illustrations; PNGs are rasterized directly from this source.
    // No atlas cells, card backgrounds, labels or rarity badges are baked into the objects.
    public static class ItemArtworkBuilder
    {
        const string Root="Assets/Era/Resources/Items/v025",Source="ArtSource/Items/v0.25";
        const string Revision="objects-v025-1";
        static readonly Color Gold=C("e9b859"),Steel=C("c4d9df"),Wood=C("805239"),Leather=C("ac6742"),Red=C("e45e68"),Blue=C("59c5ee"),Green=C("64c895"),Violet=C("b38cf0"),Ivory=C("f1e3be"),Dark=C("293549"),Ink=C("172332");
        static Color C(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
        [MenuItem("ERA/Create all transparent item illustrations")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Source);
            string fingerprint;using(var hash=System.Security.Cryptography.SHA256.Create())fingerprint=Revision+":"+BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes("Assets/Era/Editor/ItemArtworkBuilder.cs"))).Replace("-","");
            bool rebuild=!File.Exists(Source+"/revision.txt")||File.ReadAllText(Source+"/revision.txt")!=fingerprint;
            var manifest=new StringBuilder("# ERA item artwork v0.25\n\nOriginal object illustrations, RGBA PNG 256x256 and editable SVG. All 129 stable IDs are covered, including 116 active items.\n\n| ID | Object | PNG | SVG |\n| --- | --- | --- | --- |\n");
            foreach(ItemId id in Enum.GetValues(typeof(ItemId)))
            {
                string png=Root+"/"+id+".png",svg=Source+"/"+id+".svg";
                if(rebuild||!File.Exists(png)||!File.Exists(svg))
                {
                    var d=new Drawing();Illustrate(id,d);var texture=d.Texture();
                    File.WriteAllBytes(png,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                    File.WriteAllText(svg,d.Svg(id.ToString()),new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(png,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(png);
                    importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency=true;importer.isReadable=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
                    importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=256;importer.SaveAndReimport();
                }
                manifest.AppendLine("| "+(int)id+" | "+id+" | [PNG](../../../"+png+") | [SVG]("+id+".svg) |");
            }
            File.WriteAllText(Source+"/README.md",manifest.ToString(),new UTF8Encoding(false));File.WriteAllText(Source+"/revision.txt",fingerprint);
            if(rebuild)for(int page=0;page<4;page++)
            {
                var sheet=new Texture2D(1536,1536,TextureFormat.RGBA32,false);sheet.SetPixels(new Color[1536*1536]);
                for(int cell=0;cell<36;cell++){int index=page*36+cell;if(index>=ItemCatalog.Count)break;var icon=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+(ItemId)index+".png");var colors=icon.GetPixels();for(int i=0;i<colors.Length;i++)if(colors[i].a==0)colors[i]=Color.clear;sheet.SetPixels(cell%6*256,(5-cell/6)*256,256,256,colors);}
                sheet.Apply();File.WriteAllBytes(Source+"/ContactSheet-"+(page+1)+".png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
            }
            Debug.Log("ERA_ITEM_ART_READY 129 distinct transparent objects / 256px / editable SVG");
        }
        static void Illustrate(ItemId id,Drawing d)
        {
            switch(id)
            {
                case ItemId.SharpBlade:Sword(d,Steel,Gold);break;
                case ItemId.SwiftBoots:Boot(d,Leather);d.Line(Ivory,4,77,49,89,44);d.Line(Ivory,3,75,58,86,53);break;
                case ItemId.StoutHeart:Heart(d,Red);d.Line(Ivory,4,38,57,47,49);break;
                case ItemId.SteelShield:Shield(d,Steel);d.Poly(Gold,58,35,70,35,70,79,58,79);d.Line(Gold,5,43,53,85,53);break;
                case ItemId.Hourglass:Hourglass(d,Gold,Blue);break;
                case ItemId.GoldenCoin:Coin(d,64,66,35,Gold);d.Star(Ivory,64,65,17,5);break;
                case ItemId.BeginnersLuck:Clover(d);break;
                case ItemId.HeartyMeal:Meal(d);break;
                case ItemId.SmallPotion:Bottle(d,Red);d.Line(Ivory,5,56,77,72,77);d.Line(Ivory,5,64,69,64,85);break;
                case ItemId.DoubleShot:Arrow(d,-13,Blue);Arrow(d,13,Gold);break;
                case ItemId.SpikedGloves:Glove(d,Leather,true);break;
                case ItemId.WingedBoots:Wing(d,64,56,1,Ivory);Boot(d,Blue);break;
                case ItemId.TrueSight:Lens(d,Blue);d.Line(Gold,3,60,26,60,40);d.Line(Gold,3,60,76,60,90);d.Line(Gold,3,30,57,44,57);d.Line(Gold,3,77,57,90,57);break;
                case ItemId.BookOfKnowledge:Book(d,Blue);d.Star(Gold,65,64,17,6);break;
                case ItemId.FireSphere:Orb(d,Red);Flame(d,64,57,30,Gold);break;
                case ItemId.Magnet:Magnet(d);break;
                case ItemId.FlameBlade:Flame(d,86,45,28,Red);Sword(d,Gold,Red);Flame(d,31,72,17,Gold);break;
                case ItemId.IceCore:Gem(d,64,66,34,46,Blue);d.Line(Ivory,2,64,31,64,95);d.Line(Ivory,2,43,53,83,78);break;
                case ItemId.StormAmulet:Amulet(d,Gold,Blue);Bolt(d,64,78,22,Ivory);break;
                case ItemId.Necronomicon:Book(d,C("50666b"));Skull(d,65,65,23,Ivory);break;
                case ItemId.Chronosphere:Orb(d,Blue);ClockFace(d,64,65,26,Ivory);break;
                case ItemId.ChaosPortal:Portal(d,Violet);d.Gem(92,32,8,12,Gold);break;
                case ItemId.CrownOfGreed:Crown(d,Gold,Green);Coin(d,92,92,17,Gold);break;
                case ItemId.DragonBreath:Dragon(d,Red);break;
                case ItemId.AngelWings:Wing(d,64,61,-1,Ivory);Wing(d,64,61,1,Ivory);Gem(d,64,68,9,17,Gold);break;
                case ItemId.InfinitePower:Infinity(d,Gold);d.Gem(64,65,10,16,Red);break;
                case ItemId.BlackHole:Portal(d,Gold);d.Ellipse(Dark,64,64,22,28);d.Ellipse(C("090e19"),64,64,13,18);break;
                case ItemId.Rebirth:Wing(d,64,57,-1,Gold);Wing(d,64,57,1,Gold);Flame(d,64,73,28,Red);break;
                case ItemId.TimeMaster:Hourglass(d,Gold,Violet);d.Arc(Blue,4,64,64,48,-2.6f,1.1f);d.Poly(Blue,110,59,113,79,96,70);break;
                case ItemId.RealityCube:Cube(d,64,64,39,Violet);Gem(d,63,61,8,12,Gold);break;
                case ItemId.CopperSpring:Spring(d);break;
                case ItemId.Spyglass:Spyglass(d);break;
                case ItemId.ChalkCircle:d.Poly(Ivory,29,84,83,40,98,50,44,97);d.Poly(C("d5c4aa"),29,84,44,97,24,99);d.Ellipse(Ivory,49,102,20,2);break;
                case ItemId.WindFletching:Feather(d,Blue);d.Line(Gold,6,31,102,90,32);break;
                case ItemId.ScholarSeal:Scroll(d,Ivory);Seal(d,76,82,Blue);break;
                case ItemId.FeatherAnklet:d.Arc(Gold,9,64,77,29,-3.4f,2.9f);Feather(d,Ivory,.63f,29,7);break;
                case ItemId.IronBrooch:Shield(d,C("7b96a0"),.7f,19,21);Gem(d,64,63,13,20,Green);break;
                case ItemId.HealersRibbon:Ribbon(d,Red);d.Line(Ivory,5,58,58,73,58);d.Line(Ivory,5,65,51,65,66);break;
                case ItemId.DewBead:Chain(d,Gold);Gem(d,64,77,22,31,Blue);d.Ellipse(Ivory,56,64,4,7);break;
                case ItemId.NomadPin:Pin(d,Gold);Boot(d,Leather,.46f,35,23);break;
                case ItemId.DuelistKnot:d.Line(Red,12,31,95,82,27);d.Line(Red,11,40,24,87,96);d.Ellipse(Red,48,59,19,14);d.Ellipse(Red,77,59,19,14);d.Ellipse(Dark,48,59,9,5);d.Ellipse(Dark,77,59,9,5);d.Gem(64,69,9,10,Gold);break;
                case ItemId.CartographerSeal:Scroll(d,Ivory);d.Line(Blue,3,42,45,76,58);d.Line(Blue,3,76,58,49,78);d.Star(Gold,78,83,17,4);break;
                case ItemId.LeadPendulum:d.Line(Gold,4,64,19,43,81);Gem(d,43,87,17,24,Steel);d.Arc(Gold,3,64,58,45,0,1.7f);break;
                case ItemId.WardensBadge:Shield(d,Blue);d.Star(Gold,64,61,25,5);break;
                case ItemId.ApprenticeCord:d.Arc(Leather,7,64,55,31,-3.1f,0);d.Line(Leather,6,33,54,45,91);d.Line(Leather,6,95,54,81,91);Gem(d,64,86,17,24,Violet);break;
                case ItemId.BloodGlass:Gem(d,64,65,31,45,Red);d.Line(Dark,3,67,25,57,58);d.Line(Dark,3,57,58,78,80);d.Line(Ivory,2,46,53,52,37);break;
                case ItemId.CometDust:Bottle(d,Blue);d.Star(Ivory,65,77,14,5);d.Star(Gold,86,28,8,4);break;
                case ItemId.MerchantScale:Scale(d);break;
                case ItemId.SilverThread:d.Ellipse(Steel,64,64,29,31);d.Ellipse(Dark,64,64,16,19);for(int i=0;i<7;i++)d.Arc(Ivory,2,64,62,20+i*1.2f,-2.8f,1.8f);d.Line(Steel,3,87,81,110,102);break;
                case ItemId.SteadyHand:Glove(d,Steel,false);d.Line(Gold,4,51,81,81,81);break;
                case ItemId.GlassCannon:Cannon(d,Blue);d.Line(Ivory,2,75,39,60,66);break;
                case ItemId.LeadBoots:Boot(d,C("798d9c"));d.Poly(Steel,40,76,88,76,104,97,30,98);break;
                case ItemId.BloodEngine:Machine(d,Red);Heart(d,Red,.42f,39,39);break;
                case ItemId.SniperOath:Scroll(d,Ivory);Lens(d,Blue,.5f,43,32);break;
                case ItemId.BrawlerOath:Scroll(d,C("d4ba91"));Glove(d,Red,false,.5f,32,27);break;
                case ItemId.ThornMail:Armor(d,Steel);for(int i=0;i<3;i++){d.Poly(Steel,25+i*4,47+i*15,12,43+i*14,30+i*3,58+i*14);d.Poly(Steel,103-i*4,47+i*15,116,43+i*14,98-i*3,58+i*14);}break;
                case ItemId.FrostBell:Bell(d,Blue);d.Star(Ivory,64,64,14,6);break;
                case ItemId.VenomNeedle:d.Line(Steel,6,31,103,86,35);d.Poly(Green,69,38,84,19,99,31,83,51);d.Ellipse(Green,37,86,9,13);break;
                case ItemId.SerratedCoin:Coin(d,64,65,34,Gold,16);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;d.Poly(Steel,64+Mathf.Cos(a)*30,65+Mathf.Sin(a)*30,64+Mathf.Cos(a+.17f)*43,65+Mathf.Sin(a+.17f)*43,64+Mathf.Cos(a+.4f)*32,65+Mathf.Sin(a+.4f)*32);}d.Gem(64,65,11,15,Red);break;
                case ItemId.ThunderRod:Staff(d,Wood);Bolt(d,64,34,28,Gold);break;
                case ItemId.DashFlint:d.Poly(Steel,36,39,70,22,92,60,65,94,27,86);Flame(d,93,70,17,Gold);break;
                case ItemId.LandingStone:Rock(d,C("8a9a96"));d.Star(Gold,64,61,16,4);break;
                case ItemId.RetaliationSeal:Shield(d,Gold);d.Poly(Red,64,31,81,61,64,86,47,61);d.Star(Ivory,64,60,12,4);break;
                case ItemId.SoulJar:Bottle(d,C("8ee9c8"));Skull(d,64,77,15,Ivory);break;
                case ItemId.PowderKeg:Barrel(d,Wood);d.Line(Leather,6,72,27,90,18);Flame(d,92,20,9,Gold);break;
                case ItemId.HuntersDebt:Coin(d,63,66,33,Gold);Arrow(d,0,Red,.65f,23,19);break;
                case ItemId.SilentVow:Book(d,C("7d628a"));d.Line(Leather,9,31,46,92,86);d.Poly(Gold,56,55,73,55,73,75,56,75);break;
                case ItemId.LoadedDice:Dice(d);break;
                case ItemId.HeavyArrow:Arrow(d,0,Steel);d.Poly(Steel,70,43,96,14,108,52);break;
                case ItemId.RicochetSeed:Seed(d);break;
                case ItemId.SteamValve:Valve(d);d.Arc(Ivory,5,90,36,11,-2,1);d.Arc(Ivory,5,103,23,8,-2,1);break;
                case ItemId.StormConductor:Machine(d,Blue);Bolt(d,64,63,28,Gold);break;
                case ItemId.SepticWound:Bottle(d,Green);d.Poly(Red,53,65,74,74,57,86,75,95);break;
                case ItemId.ShatterLens:Lens(d,Blue);d.Line(Ivory,3,57,30,64,55);d.Line(Ivory,3,64,55,43,70);d.Line(Ivory,3,64,55,84,60);break;
                case ItemId.AshGarden:Plant(d,C("756b74"),Gold);Flame(d,65,74,25,Red);break;
                case ItemId.BloodCompass:Compass(d,Red);break;
                case ItemId.ExecutionLedger:Book(d,Red);d.Line(Steel,6,48,46,81,80);d.Line(Steel,6,80,46,49,80);break;
                case ItemId.EchoChamber:Bell(d,Violet);d.Arc(Blue,3,64,61,44,-1.1f,1.1f);d.Arc(Blue,3,64,61,48,2.1f,4.2f);break;
                case ItemId.Alternator:Machine(d,Steel);d.Gem(45,55,10,15,Red);d.Gem(83,78,10,15,Blue);d.Line(Gold,4,45,77,83,55);break;
                case ItemId.KineticSpool:Spool(d,Gold,Blue);break;
                case ItemId.GlacialPrison:d.Poly(Blue,35,33,64,15,97,35,104,95,64,111,27,93);for(int i=0;i<4;i++)d.Line(Ivory,3,43+i*14,34,43+i*14,96);Skull(d,64,71,16,Blue);break;
                case ItemId.Capacitor:Machine(d,Gold);d.Poly(Blue,45,40,83,40,83,87,45,87);d.Line(Ivory,5,52,56,76,56);d.Line(Ivory,5,64,45,64,67);break;
                case ItemId.WarDrum:Drum(d);break;
                case ItemId.HuntingPack:Wolf(d,Steel);d.Arc(Blue,4,64,63,44,.1f,2.8f);break;
                case ItemId.MirrorShard:Gem(d,63,65,28,46,Steel);d.Poly(Blue,39,53,61,29,83,52,59,79);d.Line(Ivory,4,46,68,77,40);break;
                case ItemId.OrbitalSaw:Saw(d);break;
                case ItemId.Thunderstep:Boot(d,Dark);Bolt(d,85,43,24,Blue);break;
                case ItemId.SacrificialBrazier:Brazier(d,Gold,Red);break;
                case ItemId.MiserFuse:Coin(d,53,74,29,Gold);d.Poly(Dark,74,61,98,61,101,96,72,96);d.Line(Leather,5,84,61,100,42);Flame(d,101,35,11,Red);break;
                case ItemId.VoidAnchor:Anchor(d,Violet);break;
                case ItemId.PhoenixAsh:Flame(d,64,68,35,Red);Wing(d,64,55,-1,Gold,.7f);Wing(d,64,55,1,Gold,.7f);break;
                case ItemId.PlagueDoctor:Mask(d);break;
                case ItemId.RedThread:Spool(d,Wood,Red);d.Line(Red,4,79,85,106,107);break;
                case ItemId.SpellLoom:Loom(d);break;
                case ItemId.FrostfireCovenant:Scroll(d,Ivory);Flame(d,50,62,22,Red);Gem(d,82,66,14,25,Blue);break;
                case ItemId.DuelistClock:Clock(d,Gold);d.Poly(Steel,85,28,110,34,94,52);break;
                case ItemId.Overgrowth:Plant(d,Green,C("a2cf65"));break;
                case ItemId.SoulTithe:Lantern(d,Steel,Green);Skull(d,64,72,13,Ivory);break;
                case ItemId.BrokenMetronome:Metronome(d);break;
                case ItemId.StarMap:Scroll(d,C("546489"));for(int i=0;i<4;i++)d.Star(Gold,42+i*13,46+(i%2)*28,6,4);d.Line(Ivory,2,42,46,55,74);d.Line(Ivory,2,55,74,68,46);d.Line(Ivory,2,68,46,81,74);break;
                case ItemId.PacifistEngine:Machine(d,Green);d.Poly(Gold,31,33,40,19,49,33);d.Poly(Gold,82,97,91,111,100,97);break;
                case ItemId.GravePact:Scroll(d,C("b3ab9f"));Skull(d,63,64,24,Steel);break;
                case ItemId.TemporalDebt:Clock(d,Violet);d.Poly(Dark,72,29,58,58,73,67,59,96,86,66,69,56);break;
                case ItemId.MobiusQuiver:Quiver(d);break;
                case ItemId.ParasiteCrown:Crown(d,C("8ec8a1"),Red);d.Line(Red,8,35,83,25,103);d.Line(Red,7,88,83,102,107);break;
                case ItemId.InversionHeart:Heart(d,Blue);d.Gem(62,65,16,25,Violet);d.Line(Gold,3,47,56,37,47);d.Line(Gold,3,77,76,88,86);break;
                case ItemId.EventHorizon:Orb(d,Dark);d.Arc(Red,8,64,64,43,-2.7f,1.8f);d.Arc(Gold,4,64,65,31,.3f,4.2f);break;
                case ItemId.GlassWorld:Orb(d,Blue);d.Line(Ivory,3,70,24,53,51);d.Line(Ivory,3,53,51,79,69);d.Line(Ivory,3,79,69,59,102);d.Line(Ivory,2,53,51,30,65);break;
                case ItemId.WalkingCathedral:Cathedral(d);break;
                case ItemId.BloodMoon:Moon(d,Red);d.Gem(93,83,9,15,Red);break;
                case ItemId.StormCircuit:Spool(d,Steel,Blue);Bolt(d,64,64,25,Gold);break;
                case ItemId.SplitSoul:MaskHalf(d,-1,Blue);MaskHalf(d,1,Violet);break;
                case ItemId.WinterSleep:Lantern(d,Blue,Ivory);d.Star(Blue,64,71,19,6);break;
                case ItemId.LivingArsenal:Sword(d,Steel,Red);Arrow(d,24,Gold,.8f,13,16);d.Line(Leather,7,29,106,98,106);break;
                case ItemId.VoidShepherd:Staff(d,Dark);d.Arc(Violet,10,64,37,24,-3.1f,1.7f);d.Gem(60,37,9,15,Blue);break;
                case ItemId.AlchemyEngine:Machine(d,Gold);d.Ellipse(Red,46,46,10,10);d.Ellipse(Blue,84,46,10,10);d.Ellipse(Green,65,83,11,11);break;
                case ItemId.FateLoom:Loom(d);d.Ellipse(Gold,65,64,21,13);d.Gem(65,64,7,10,Violet);break;
                case ItemId.HiveMind:Hive(d);break;
                case ItemId.WorldSeed:Seed(d);d.Arc(Gold,3,64,67,46,-1,1);d.Arc(Gold,3,64,67,46,2.1f,4.1f);break;
                case ItemId.LastPilgrim:Staff(d,Wood);Boot(d,Leather,.62f,20,41);d.Poly(Ivory,72,26,99,38,74,50);break;
                case ItemId.DebtOfKing:Crown(d,Gold,Red);Scroll(d,Ivory,.48f,14,55);Coin(d,93,93,15,Gold);break;
                case ItemId.Chronicle:Book(d,Ivory);d.Poly(Gold,48,36,77,36,77,91,62,82,48,91);d.Star(Blue,64,57,11,4);break;
                case ItemId.Ouroboros:Ouroboros(d);break;
                case ItemId.ArchitectReign:Tower(d,Steel);d.Poly(Gold,39,30,35,17,52,24,64,11,76,24,92,17,88,30);break;
                case ItemId.EclipseCrown:Crown(d,Dark,Gold);d.Ellipse(Gold,64,45,24,24);d.Ellipse(Dark,70,42,20,22);break;
                case ItemId.ImmortalContract:Scroll(d,Ivory);Heart(d,Red,.41f,37,30);Seal(d,80,87,Gold);break;
                case ItemId.SingularityTestament:Book(d,Dark);Portal(d,Gold,.56f,28,28);break;
                case ItemId.SevenfoldOath:Scroll(d,Ivory);for(int i=0;i<7;i++)d.Line(Gold,3,41+i*7,49,41+i*7,81+(i%2)*7);Seal(d,81,93,Red);break;
                case ItemId.EndlessRefrain:TuningFork(d);d.Arc(Gold,3,64,59,44,-1.1f,1.1f);d.Arc(Gold,3,64,59,44,2.1f,4.2f);break;
                default:throw new Exception("Missing unique item illustration: "+id);
            }
        }
        static void Sword(Drawing d,Color blade,Color hilt){d.Poly(blade,79,17,98,19,96,39,56,83,43,70);d.Poly(Ivory,79,21,88,22,51,77,47,72);d.Line(hilt,9,34,64,64,91);d.Line(Leather,10,42,83,26,102);d.Ellipse(hilt,25,103,7,7);}
        static void Boot(Drawing d,Color c,float s=1,float x=0,float y=0){d.Push(s,x,y);d.Poly(c,43,23,83,23,79,72,99,83,106,99,28,99,25,89,42,72);d.Poly(Dark,28,97,106,97,105,106,28,106);d.Poly(Gold,42,34,82,34,81,43,42,43);d.Line(Ivory,3,55,53,74,55);d.Line(Ivory,3,53,62,73,64);d.Pop();}
        static void Heart(Drawing d,Color c,float s=1,float x=0,float y=0){d.Push(s,x,y);d.Poly(c,64,109,24,71,17,52,21,33,35,23,50,25,64,38,78,25,94,23,108,34,112,53,104,72);d.Poly(Color.Lerp(c,Ivory,.25f),29,49,33,35,45,34,55,43,43,48);d.Pop();}
        static void Shield(Drawing d,Color c,float s=1,float x=0,float y=0){d.Push(s,x,y);d.Poly(Gold,64,16,105,34,98,79,64,112,30,79,23,34);d.Poly(c,64,26,94,40,88,76,64,100,40,76,34,40);d.Poly(Color.Lerp(c,Ivory,.2f),64,27,64,97,41,74,35,41);d.Pop();}
        static void Book(Drawing d,Color c){d.Poly(Dark,29,21,94,26,100,102,33,109,23,99);d.Poly(Ivory,31,28,89,32,94,96,32,103);for(int i=0;i<4;i++)d.Line(C("ac9675"),1.1f,36,91+i*3,90,86+i*3);d.Poly(c,25,19,87,23,94,92,29,100);d.Poly(Gold,26,20,35,21,39,99,29,100);d.Line(Gold,2,43,33,77,35);d.Line(Gold,2,46,83,82,79);}
        static void Bottle(Drawing d,Color liquid){d.Poly(Steel,48,23,79,23,78,45,96,67,99,93,85,109,43,109,28,93,31,67,49,45);d.Poly(liquid,36,71,92,71,93,91,81,103,47,103,35,90);d.Ellipse(Color.Lerp(liquid,Ivory,.3f),64,72,28,6);d.Poly(Leather,47,15,80,15,79,30,48,30);d.Line(Ivory,4,40,60,36,77);d.Line(Ivory,3,35,82,35,89);}
        static void Coin(Drawing d,float x,float y,float r,Color c,int sides=24){d.Ellipse(C("946133"),x+3,y+4,r,r,sides);d.Ellipse(c,x,y,r,r,sides);d.Ellipse(Color.Lerp(c,Ivory,.3f),x,y,r*.77f,r*.77f,sides);d.Ellipse(c,x,y,r*.67f,r*.67f,sides);}
        static void ClockFace(Drawing d,float x,float y,float r,Color face){d.Ellipse(Gold,x,y,r,r);d.Ellipse(face,x,y,r*.8f,r*.8f);for(int i=0;i<12;i++){float a=i*Mathf.PI/6;d.Line(Dark,1.8f,x+Mathf.Cos(a)*r*.63f,y+Mathf.Sin(a)*r*.63f,x+Mathf.Cos(a)*r*.72f,y+Mathf.Sin(a)*r*.72f);}d.Line(Dark,3,x,y,x-r*.37f,y-r*.32f);d.Line(Dark,3,x,y,x+r*.42f,y);d.Ellipse(Gold,x,y,3,3);}
        static void Clock(Drawing d,Color metal){d.Ellipse(metal,64,20,10,9);d.Poly(metal,57,21,71,21,71,34,57,34);ClockFace(d,64,70,38,Ivory);}
        static void Hourglass(Drawing d,Color metal,Color sand){d.Poly(Steel,40,26,88,26,85,46,70,63,85,81,88,101,40,101,43,81,58,63,43,46);d.Poly(sand,47,38,81,38,77,48,64,59,51,48);d.Poly(sand,64,76,47,96,82,96);d.Line(sand,2,64,59,64,80);d.Poly(metal,32,17,96,17,96,27,32,27);d.Poly(metal,32,101,96,101,96,111,32,111);d.Line(metal,4,35,27,35,101);d.Line(metal,4,93,27,93,101);}
        static void Gem(Drawing d,float x,float y,float w,float h,Color c){d.Gem(x,y,w,h,c);}
        static void Orb(Drawing d,Color c){d.Ellipse(Gold,64,66,42,42);d.Ellipse(c,64,65,37,37);d.Ellipse(Color.Lerp(c,Ivory,.45f),50,49,9,6);d.Arc(Ivory,2,64,65,28,-2.8f,-1.7f);}
        static void Bolt(Drawing d,float x,float y,float r,Color c){d.Poly(c,x+4,y-r,x-r*.62f,y+3,x-1,y+3,x-8,y+r,x+r*.66f,y-7,x+2,y-7);}
        static void Flame(Drawing d,float x,float y,float r,Color c){d.Poly(c,x,y-r*1.2f,x+r*.21f,y-r*.3f,x+r*.6f,y-r*.6f,x+r*.78f,y+r*.1f,x+r*.56f,y+r*.73f,x,y+r,x-r*.65f,y+r*.55f,x-r*.72f,y-r*.05f,x-r*.36f,y-r*.65f,x-r*.27f,y-r*.04f);d.Poly(Gold,x,y-r*.2f,x+r*.28f,y+r*.45f,x,y+r*.77f,x-r*.29f,y+r*.4f);}
        static void Chain(Drawing d,Color metal){d.Arc(metal,5,64,24,35,0,Mathf.PI);d.Line(metal,5,29,24,47,62);d.Line(metal,5,99,24,81,62);d.Ellipse(metal,64,62,9,9);d.Ellipse(Dark,64,62,4,4);}
        static void Amulet(Drawing d,Color metal,Color gem){Chain(d,metal);d.Gem(64,80,27,33,metal);d.Gem(64,80,19,25,gem);}
        static void Wing(Drawing d,float x,float y,int sign,Color c,float s=1){d.Push(s,x*(1-s),y*(1-s));d.Poly(c,x,y+29,x+sign*17,y-17,x+sign*47,y-39,x+sign*45,y+3,x+sign*31,y+29,x+sign*9,y+42);for(int i=0;i<4;i++)d.Line(Color.Lerp(c,Gold,.4f),2,x+sign*(9+i*6),y+26-i*4,x+sign*(29+i*4),y-15-i*4);d.Pop();}
        static void Arrow(Drawing d,float shift,Color c,float s=1,float x=0,float y=0){d.Push(s,x,y);d.Line(Wood,5,30+shift,105,88+shift,35);d.Poly(c,75+shift,36,101+shift,15,99+shift,47,89+shift,39);d.Poly(c,29+shift,84,24+shift,86,23+shift,108,36+shift,105,45+shift,92);d.Line(Ivory,1.5f,29+shift,94,36+shift,96);d.Pop();}
        static void Scroll(Drawing d,Color c,float s=1,float x=0,float y=0){d.Push(s,x,y);d.Poly(c,31,25,93,25,87,105,28,105,36,89);d.Ellipse(Gold,34,25,10,9);d.Ellipse(c,61,25,29,8);d.Ellipse(Gold,88,25,8,9);d.Ellipse(c,57,105,29,8);d.Line(Color.Lerp(c,Wood,.45f),2,44,40,77,40);d.Line(Color.Lerp(c,Wood,.45f),2,41,91,76,91);d.Pop();}
        static void Seal(Drawing d,float x,float y,Color c){d.Poly(c,x-11,y+2,x-15,y+30,x,y+22,x+11,y+30,x+15,y+2);d.Star(c,x,y,18,12);d.Ellipse(Gold,x,y,11,11);d.Star(c,x,y,6,5);}
        static void Feather(Drawing d,Color c,float s=1,float x=0,float y=0){d.Push(s,x,y);d.Poly(c,99,17,104,46,89,75,50,95,28,102,36,74,60,38);d.Line(Gold,3,26,111,94,27);for(int i=0;i<4;i++)d.Line(Color.Lerp(c,Dark,.3f),1.6f,48+i*10,85-i*13,42+i*13,65-i*12);d.Pop();}
        static void Pin(Drawing d,Color c){d.Poly(c,64,17,101,37,95,82,64,107,32,82,26,37);d.Poly(Dark,64,27,89,42,85,75,64,94,43,75,39,42);}
        static void Staff(Drawing d,Color c){d.Line(c,10,54,108,69,28);d.Line(Gold,4,56,94,65,94);d.Line(Gold,4,60,78,69,78);d.Poly(Gold,53,23,80,26,80,39,55,36);}
        static void Lens(Drawing d,Color c,float s=1,float x=0,float y=0){d.Push(s,x,y);d.Line(Wood,15,81,81,108,109);d.Ellipse(Gold,58,57,36,36);d.Ellipse(c,58,57,29,29);d.Line(Ivory,5,42,56,59,38);d.Line(Ivory,2,53,73,77,48);d.Pop();}
        static void Spring(Drawing d){d.Poly(Steel,37,22,92,22,92,30,37,30);d.Poly(Steel,37,100,92,100,92,109,37,109);for(int i=0;i<5;i++){d.Line(C("7e452d"),9,39,33+i*14,91,28+i*14);d.Line(C("d9965f"),7,39,33+i*14,91,42+i*14);} }
        static void Spyglass(Drawing d){d.Poly(Leather,26,86,78,27,99,45,47,104);d.Poly(Gold,24,82,33,72,54,92,46,103);d.Poly(Gold,72,28,80,18,108,43,99,53);d.Poly(Blue,85,23,103,40,98,44,80,29);d.Line(Gold,5,51,54,72,72);}
        static void Glove(Drawing d,Color c,bool spikes,float s=1,float x=0,float y=0){d.Push(s,x,y);d.Poly(c,37,106,26,65,29,51,42,62,41,32,50,27,58,57,60,20,69,20,74,57,81,28,90,31,87,64,98,49,106,56,93,102);d.Poly(Gold,36,89,94,89,94,107,38,107);d.Line(Dark,2,51,60,55,83);d.Line(Dark,2,68,59,68,83);if(spikes)for(int i=0;i<3;i++)d.Poly(Steel,41+i*18,66,48+i*18,49,55+i*18,66);d.Pop();}
        static void Clover(Drawing d){d.Line(Green,7,66,64,51,108);for(int i=0;i<4;i++){float a=i*Mathf.PI/2;d.Ellipse(Green,64+Mathf.Cos(a)*21,54+Mathf.Sin(a)*21,17,19);d.Line(C("d3e79c"),2,64,54,64+Mathf.Cos(a)*27,54+Mathf.Sin(a)*27);}d.Ellipse(Gold,64,54,6,6);}
        static void Meal(Drawing d){d.Ellipse(Steel,64,76,46,25);d.Ellipse(Ivory,64,73,39,20);d.Poly(C("b86b47"),29,70,35,46,54,33,82,38,99,60,89,83,51,88);d.Poly(C("eaa46c"),35,65,42,48,56,42,80,45,92,61,79,71,49,77);d.Line(Ivory,5,48,57,57,48);d.Line(Ivory,5,67,60,77,50);d.Poly(Green,82,87,108,76,101,97,81,99);}
        static void Magnet(Drawing d){d.Poly(Red,29,25,48,25,48,74,56,84,72,84,80,74,80,25,99,25,99,78,88,100,73,108,54,108,39,99,29,78);d.Poly(Steel,28,24,49,24,49,45,28,45);d.Poly(Steel,79,24,100,24,100,45,79,45);}
        static void Portal(Drawing d,Color c,float s=1,float x=0,float y=0){d.Push(s,x,y);d.Ellipse(Dark,64,65,36,45);d.Arc(c,10,64,65,37,-2.8f,2.2f);d.Arc(Color.Lerp(c,Ivory,.5f),4,64,65,24,-.6f,4.3f);d.Arc(c,4,64,65,12,1,6);d.Gem(96,39,9,14,c);d.Gem(29,88,7,10,c);d.Pop();}
        static void Crown(Drawing d,Color metal,Color jewel){d.Poly(metal,31,94,17,36,44,54,64,19,84,54,110,36,97,94);d.Poly(Color.Lerp(metal,Wood,.3f),30,88,99,88,96,105,33,105);d.Gem(64,69,12,17,jewel);d.Ellipse(jewel,36,69,5,6);d.Ellipse(jewel,92,69,5,6);}
        static void Dragon(Drawing d,Color c){d.Poly(c,32,43,49,18,58,41,80,40,109,58,104,77,81,79,63,70,57,103,29,106,22,88,36,72);d.Poly(Gold,35,42,37,20,49,36);d.Poly(Gold,65,48,78,50,68,59);d.Poly(Dark,82,66,106,64,102,72,83,73);d.Poly(Ivory,92,67,96,76,99,66);d.Line(Color.Lerp(c,Dark,.4f),3,35,86,47,78);}
        static void Infinity(Drawing d,Color c){d.Ellipse(c,39,64,26,23);d.Ellipse(c,89,64,26,23);d.Ellipse(Dark,39,64,13,11);d.Ellipse(Dark,89,64,13,11);d.Line(c,12,49,46,79,82);d.Line(c,12,49,82,79,46);}
        static void Cube(Drawing d,float x,float y,float r,Color c){d.Poly(c,x,y-r,x+r,y-r*.47f,x+r,y+r*.48f,x,y+r,x-r,y+r*.48f,x-r,y-r*.47f);d.Poly(Color.Lerp(c,Ivory,.3f),x,y-r,x+r,y-r*.47f,x,y+3,x-r,y-r*.47f);d.Line(Gold,2,x,y+3,x,y+r);d.Line(Gold,2,x-r,y-r*.47f,x,y+3);d.Line(Gold,2,x+r,y-r*.47f,x,y+3);}
        static void Ribbon(Drawing d,Color c){d.Poly(c,64,49,23,27,18,59,54,73,35,111,65,96,89,111,75,72,111,59,105,26);d.Ellipse(c,64,60,16,18);d.Line(Gold,2,27,37,52,55);d.Line(Gold,2,101,37,77,55);}
        static void Scale(Drawing d){d.Line(Gold,7,64,28,64,107);d.Line(Gold,6,24,43,104,43);d.Poly(Gold,41,109,87,109,79,98,49,98);for(int side=-1;side<=1;side+=2){float x=64+side*32;d.Line(Gold,2,x,43,x-16,76);d.Line(Gold,2,x,43,x+16,76);d.Poly(Steel,x-21,76,x+21,76,x+12,90,x-12,90);}d.Gem(64,28,8,12,Green);}
        static void Cannon(Drawing d,Color c){d.Ellipse(Wood,44,93,18,18);d.Ellipse(Dark,44,93,8,8);d.Poly(Gold,30,67,93,25,112,54,48,95);d.Poly(c,35,66,89,30,105,53,49,90);d.Ellipse(Dark,100,41,12,18);d.Poly(Wood,21,105,86,105,78,94,29,94);}
        static void Armor(Drawing d,Color c){d.Poly(c,32,22,49,18,64,34,80,18,97,23,111,52,91,65,88,108,40,108,37,65,17,52);d.Poly(Gold,49,19,64,34,79,19,76,39,64,48,52,39);d.Line(Dark,3,64,48,64,102);d.Line(Gold,4,41,84,87,84);}
        static void Machine(Drawing d,Color c){d.Poly(Dark,31,26,93,26,102,100,23,100);d.Poly(c,36,33,88,33,95,92,30,92);for(int i=0;i<3;i++){d.Poly(Gold,20,42+i*20,34,42+i*20,34,50+i*20,20,50+i*20);d.Poly(Gold,94,42+i*20,108,42+i*20,108,50+i*20,94,50+i*20);}d.Line(Steel,4,45,21,45,34);d.Line(Steel,4,79,21,79,34);}
        static void Skull(Drawing d,float x,float y,float r,Color c){d.Ellipse(c,x,y-r*.15f,r,r*.82f);d.Poly(c,x-r*.6f,y+r*.25f,x+r*.6f,y+r*.25f,x+r*.48f,y+r,x-r*.48f,y+r);d.Ellipse(Dark,x-r*.36f,y-r*.03f,r*.22f,r*.24f);d.Ellipse(Dark,x+r*.36f,y-r*.03f,r*.22f,r*.24f);d.Poly(Dark,x,y+r*.24f,x-r*.16f,y+r*.48f,x+r*.16f,y+r*.48f);for(int i=-1;i<=1;i++)d.Line(Dark,1.4f,x+i*r*.25f,y+r*.7f,x+i*r*.25f,y+r*.98f);}
        static void Bell(Drawing d,Color c){d.Ellipse(Gold,64,19,7,8);d.Poly(c,26,91,36,76,38,48,48,31,64,26,81,31,91,48,93,76,103,91);d.Ellipse(Gold,64,93,40,7);d.Ellipse(Gold,64,107,8,8);d.Line(Ivory,3,48,44,45,61);}
        static void Rock(Drawing d,Color c){d.Poly(c,30,48,58,24,91,33,105,69,88,102,39,106,20,78);d.Poly(Color.Lerp(c,Ivory,.4f),30,48,58,24,77,53,43,73);d.Poly(Color.Lerp(c,Dark,.25f),77,53,91,33,105,69,88,102,74,87);}
        static void Barrel(Drawing d,Color c){d.Poly(c,37,29,90,29,99,60,94,103,35,103,28,63);d.Ellipse(c,64,29,27,10);for(int i=0;i<4;i++)d.Line(C("503c30"),2,44+i*14,38,42+i*15,100);d.Poly(Steel,31,47,96,47,98,58,29,58);d.Poly(Steel,31,83,97,83,95,95,33,95);d.Ellipse(Gold,65,27,8,5);}
        static void Dice(Drawing d){Cube(d,64,65,39,Ivory);foreach(var p in new[]{new Vector2(48,52),new Vector2(65,43),new Vector2(82,52),new Vector2(43,73),new Vector2(54,88),new Vector2(81,74),new Vector2(81,88)})d.Ellipse(Dark,p.x,p.y,4,5);}
        static void Seed(Drawing d){d.Poly(Wood,40,56,59,45,83,53,94,73,87,97,65,108,43,94,34,75);d.Line(Gold,3,64,53,64,102);d.Line(Green,6,64,55,64,31);d.Poly(Green,63,39,34,33,27,16,51,21,64,34);d.Poly(C("9bdb79"),65,34,83,16,104,16,92,35,66,43);}
        static void Valve(Drawing d){d.Poly(Steel,23,71,103,71,103,94,23,94);d.Line(Gold,12,64,82,64,43);d.Ellipse(Red,64,39,27,15);d.Ellipse(Dark,64,39,15,7);d.Line(Red,5,37,39,90,39);d.Line(Red,5,64,24,64,54);}
        static void Plant(Drawing d,Color leaves,Color pot){d.Poly(pot,36,84,93,84,85,110,45,110);d.Line(leaves,5,64,86,65,27);for(int side=-1;side<=1;side+=2)for(int i=0;i<2;i++)d.Poly(leaves,64,68-i*22,64+side*31,65-i*25,64+side*39,42-i*18,64+side*12,45-i*22);}
        static void Compass(Drawing d,Color c){d.Ellipse(Gold,64,21,10,9);d.Ellipse(Gold,64,70,40,40);d.Ellipse(Ivory,64,70,32,32);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;d.Line(Wood,2,64+Mathf.Cos(a)*24,70+Mathf.Sin(a)*24,64+Mathf.Cos(a)*29,70+Mathf.Sin(a)*29);}d.Poly(c,83,44,69,77,45,95,58,63);d.Poly(Steel,83,44,69,77,64,70);}
        static void Spool(Drawing d,Color baseColor,Color thread){d.Ellipse(baseColor,64,31,35,12);d.Poly(thread,38,31,91,31,91,97,38,97);for(int i=0;i<9;i++)d.Line(Color.Lerp(thread,Ivory,.3f),2,39,39+i*6,90,35+i*6);d.Ellipse(baseColor,64,99,36,12);d.Ellipse(baseColor,64,30,35,10);d.Ellipse(Dark,64,30,9,5);}
        static void Drum(Drawing d){d.Poly(Red,27,45,101,45,97,96,31,96);d.Ellipse(Gold,64,96,33,11);d.Ellipse(Gold,64,45,38,15);d.Ellipse(Ivory,64,43,31,11);for(int i=0;i<4;i++){d.Line(Ivory,2,31+i*20,56,40+i*16,86);d.Line(Ivory,2,48+i*16,56,38+i*17,86);}d.Line(Wood,5,22,21,83,43);d.Line(Wood,5,103,19,46,41);}
        static void Wolf(Drawing d,Color c){d.Poly(c,25,20,49,38,78,38,104,20,98,66,88,92,64,110,38,92,28,66);d.Poly(Dark,37,54,56,63,43,69);d.Poly(Dark,91,54,72,63,85,69);d.Poly(Ivory,64,67,85,87,64,104,43,87);d.Poly(Dark,54,83,74,83,64,94);d.Line(Gold,2,40,61,48,63);d.Line(Gold,2,88,61,80,63);}
        static void Saw(Drawing d){d.Star(Steel,64,64,47,14);d.Ellipse(C("7b919c"),64,64,31,31);d.Ellipse(Gold,64,64,18,18);d.Ellipse(Dark,64,64,9,9);d.Arc(Blue,3,64,64,52,-1.1f,.6f);}
        static void Brazier(Drawing d,Color metal,Color fire){d.Poly(metal,26,68,102,68,91,88,75,94,75,108,53,108,53,94,37,88);Flame(d,64,44,29,fire);d.Line(metal,5,30,65,20,48);d.Line(metal,5,98,65,108,48);}
        static void Anchor(Drawing d,Color c){d.Ellipse(Gold,64,24,12,12);d.Ellipse(Dark,64,24,5,5);d.Line(c,11,64,35,64,98);d.Line(c,8,40,55,88,55);d.Arc(c,9,64,67,38,0,Mathf.PI);d.Poly(c,22,62,22,87,42,74);d.Poly(c,106,62,106,87,86,74);}
        static void Mask(Drawing d){d.Poly(Dark,26,34,83,29,104,51,88,73,68,106,55,77,29,65);d.Poly(Ivory,38,43,83,36,97,52,71,79,65,99,56,68,34,58);d.Ellipse(Gold,49,52,12,12);d.Ellipse(Blue,49,52,7,7);d.Line(Wood,3,67,62,79,58);d.Poly(Dark,25,25,89,22,102,35,16,39);}
        static void Loom(Drawing d){d.Poly(Wood,24,21,36,21,36,108,24,108);d.Poly(Wood,92,21,104,21,104,108,92,108);d.Poly(Gold,20,24,108,24,108,35,20,35);d.Poly(Gold,20,95,108,95,108,107,20,107);for(int i=0;i<7;i++)d.Line(i%2==0?Violet:Blue,2.2f,42+i*7,35,42+i*7,95);d.Poly(Ivory,32,63,88,54,97,64,41,75);}
        static void Metronome(Drawing d){d.Poly(Wood,49,20,79,20,101,108,26,108);d.Poly(Gold,51,29,76,29,90,96,38,96);d.Poly(Dark,57,34,71,34,81,90,47,90);d.Line(Steel,4,61,90,91,26);d.Poly(Red,72,49,88,55,83,68,68,61);d.Line(Dark,2,49,81,59,72);d.Line(Dark,2,59,72,55,62);}
        static void Quiver(Drawing d){Arrow(d,-17,Steel,.85f,12,0);Arrow(d,9,Steel,.85f,12,0);d.Poly(Leather,38,45,93,55,83,111,47,108);d.Poly(Gold,35,43,95,54,93,65,36,55);d.Line(Gold,3,49,95,82,100);d.Line(Leather,7,38,57,23,90);}
        static void Cathedral(Drawing d){d.Poly(Ivory,27,58,103,58,103,107,27,107);d.Poly(Gold,19,59,40,36,64,50,88,36,112,59);d.Poly(Ivory,51,29,77,29,77,105,51,105);d.Poly(Gold,45,30,64,10,83,30);d.Poly(Blue,57,55,64,46,71,55,71,75,57,75);d.Poly(Dark,54,106,54,90,64,82,74,90,74,106);d.Line(Gold,3,64,12,64,4);}
        static void Moon(Drawing d,Color c){d.Poly(c,79,17,53,23,32,40,23,66,30,91,51,108,78,111,101,95,77,96,55,83,48,63,53,41);d.Star(Gold,89,51,13,4);}
        static void MaskHalf(Drawing d,int sign,Color c){float x=64+sign*7;d.Poly(c,x,21,x+sign*32,32,x+sign*38,61,x+sign*25,92,x,109);d.Poly(Ivory,x+sign*4,51,x+sign*24,46,x+sign*21,62,x+sign*5,66);d.Line(Gold,3,x+sign*7,86,x+sign*20,81);}
        static void Lantern(Drawing d,Color metal,Color glass){d.Arc(metal,5,64,28,15,Mathf.PI,Mathf.PI*2);d.Poly(metal,37,38,91,38,100,98,64,112,28,98);d.Poly(glass,44,46,84,46,89,91,64,101,38,91);d.Line(metal,5,64,40,64,106);d.Poly(metal,31,91,96,91,99,101,29,101);}
        static void Hive(Drawing d){d.Poly(Gold,43,24,85,24,104,59,92,99,64,112,36,99,24,59);for(int y=0;y<3;y++)for(int x=0;x<3;x++)d.Ellipse(C("ad753d"),43+x*21+(y%2)*4,45+y*23,9,9,6);d.Ellipse(Dark,66,91,12,13);d.Ellipse(Ivory,105,35,10,5);d.Ellipse(Ivory,103,24,8,6);d.Ellipse(Green,99,35,7,9);}
        static void Ouroboros(Drawing d){d.Ellipse(Green,64,65,43,43);d.Ellipse(Dark,64,65,27,27);for(int i=0;i<14;i++){float a=i*Mathf.PI/7;d.Line(Gold,2,64+Mathf.Cos(a)*34,65+Mathf.Sin(a)*34,64+Mathf.Cos(a+.15f)*41,65+Mathf.Sin(a+.15f)*41);}d.Poly(Green,77,22,105,34,105,56,81,53,70,38);d.Ellipse(Gold,92,36,4,4);d.Poly(Ivory,92,54,95,65,98,53);}
        static void Tower(Drawing d,Color c){d.Poly(c,39,37,88,37,96,108,31,108);d.Poly(Gold,31,22,45,22,45,34,57,34,57,21,71,21,71,34,83,34,83,22,98,22,96,44,33,44);d.Poly(Dark,54,107,54,82,64,73,75,82,75,107);d.Poly(Blue,57,51,72,51,72,65,57,65);}
        static void TuningFork(Drawing d){d.Line(Gold,12,64,66,64,111);d.Poly(Gold,33,19,45,19,45,61,53,70,75,70,83,61,83,19,96,19,96,64,82,83,46,83,33,64);d.Line(Ivory,2,38,25,38,56);d.Line(Ivory,2,88,25,88,56);d.Gem(64,93,9,13,Violet);}

        sealed class Drawing
        {
            const int Size=256;readonly Color[] pixels=new Color[Size*Size];readonly StringBuilder body=new StringBuilder(),defs=new StringBuilder();
            readonly Stack<Vector3> transforms=new Stack<Vector3>();Vector3 transform=new Vector3(1,0,0);int gradient;
            static string F(float v)=>v.ToString("0.###",CultureInfo.InvariantCulture);
            Vector2 Map(float x,float y)=>new Vector2(x*transform.x+transform.y,y*transform.x+transform.z);
            public void Push(float s,float x,float y){transforms.Push(transform);transform=new Vector3(transform.x*s,transform.y+x*transform.x,transform.z+y*transform.x);}
            public void Pop(){transform=transforms.Pop();}
            public void Poly(Color c,params float[] xy){var pts=new Vector2[xy.Length/2];for(int i=0;i<pts.Length;i++)pts[i]=Map(xy[i*2],xy[i*2+1]);Polygon(c,pts,1.35f*transform.x);}
            public void Line(Color c,float width,float x,float y,float a,float b)
            {
                Vector2 from=Map(x,y),to=Map(a,b);Vector2 n=new Vector2(-(to-from).y,(to-from).x).normalized*width*transform.x*.5f;
                Polygon(c,new[]{from-n,to-n,to+n,from+n},.85f*transform.x);
            }
            public void Ellipse(Color c,float x,float y,float rx,float ry,int sides=32){var pts=new float[sides*2];for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;pts[i*2]=x+Mathf.Cos(a)*rx;pts[i*2+1]=y+Mathf.Sin(a)*ry;}Poly(c,pts);}
            public void Arc(Color c,float width,float x,float y,float radius,float start,float end){for(int i=0;i<24;i++){float a=Mathf.Lerp(start,end,i/24f),b=Mathf.Lerp(start,end,(i+1)/24f);Line(c,width,x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius);}}
            public void Star(Color c,float x,float y,float r,int points){var xy=new float[points*4];for(int i=0;i<points*2;i++){float a=i*Mathf.PI/points-Mathf.PI*.5f,rr=i%2==0?r:r*.52f;xy[i*2]=x+Mathf.Cos(a)*rr;xy[i*2+1]=y+Mathf.Sin(a)*rr;}Poly(c,xy);}
            public void Gem(float x,float y,float w,float h,Color c){Poly(c,x,y-h,x+w,y-h*.23f,x+w*.7f,y+h*.51f,x,y+h,x-w*.7f,y+h*.51f,x-w,y-h*.23f);Poly(Color.Lerp(c,Ivory,.35f),x,y-h,x,y+h,x-w,y-h*.23f);Poly(Color.Lerp(c,Dark,.17f),x,y+h,x+w*.7f,y+h*.51f,x+w,y-h*.23f);Line(Ivory,1.1f,x-w*.5f,y-h*.34f,x-w*.17f,y-h*.69f);}
            void Polygon(Color c,Vector2[] pts,float stroke)
            {
                float minX=128,minY=128,maxX=0,maxY=0;foreach(var p in pts){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
                int x0=Mathf.Max(0,Mathf.FloorToInt((minX-stroke-1)*2)),x1=Mathf.Min(Size-1,Mathf.CeilToInt((maxX+stroke+1)*2));
                int y0=Mathf.Max(0,Mathf.FloorToInt((minY-stroke-1)*2)),y1=Mathf.Min(Size-1,Mathf.CeilToInt((maxY+stroke+1)*2));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    Vector2 p=new Vector2((x+.5f)*.5f,(y+.5f)*.5f);bool inside=false;float distance=999;
                    for(int i=0,j=pts.Length-1;i<pts.Length;j=i++)
                    {Vector2 a=pts[j],b=pts[i],v=b-a;float sq=v.sqrMagnitude;distance=Mathf.Min(distance,(p-a-v*(sq>0?Mathf.Clamp01(Vector2.Dot(p-a,v)/sq):0)).magnitude);if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;}
                    float signed=inside?-distance:distance,coverage=Mathf.Clamp01((stroke-signed)*2+.5f);if(coverage<=0)continue;
                    float fill=Mathf.Clamp01(-signed*2+.5f);float light=Mathf.Lerp(1.2f,.73f,Mathf.Clamp01((p.x-minX)/Mathf.Max(1,maxX-minX)*.34f+(p.y-minY)/Mathf.Max(1,maxY-minY)*.66f));
                    Color tint=Color.Lerp(Ink,new Color(Mathf.Min(1,c.r*light),Mathf.Min(1,c.g*light),Mathf.Min(1,c.b*light)),fill);int at=(Size-1-y)*Size+x;Color dst=pixels[at];float alpha=coverage+dst.a*(1-coverage);pixels[at]=new Color((tint.r*coverage+dst.r*dst.a*(1-coverage))/alpha,(tint.g*coverage+dst.g*dst.a*(1-coverage))/alpha,(tint.b*coverage+dst.b*dst.a*(1-coverage))/alpha,alpha);
                }
                string name="g"+gradient++;Color lightC=Color.Lerp(c,Color.white,.2f),darkC=c*.73f;
                defs.Append("<linearGradient id=\"").Append(name).Append("\" x1=\"0\" y1=\"0\" x2=\".6\" y2=\"1\"><stop stop-color=\"#").Append(ColorUtility.ToHtmlStringRGB(lightC)).Append("\"/><stop offset=\"1\" stop-color=\"#").Append(ColorUtility.ToHtmlStringRGB(darkC)).Append("\"/></linearGradient>\n");
                body.Append("<polygon points=\"");foreach(var p in pts)body.Append(F(p.x)).Append(',').Append(F(p.y)).Append(' ');body.Append("\" fill=\"url(#").Append(name).Append(")\" stroke=\"#172332\" stroke-width=\"").Append(F(stroke*2)).Append("\" stroke-linejoin=\"round\"/>\n");
            }
            public Texture2D Texture(){var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name="Original transparent object"};texture.SetPixels(pixels);texture.Apply();return texture;}
            public string Svg(string name)=>"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 128 128\" width=\"256\" height=\"256\"><title>"+name+"</title><defs>"+defs+"</defs>\n"+body+"</svg>\n";
        }
    }
}
