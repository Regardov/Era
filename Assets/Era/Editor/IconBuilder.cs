using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Era.Editor
{
    // Original, transparent line icons. Rasterized at 2x for soft edges at small HUD sizes.
    public static class IconBuilder
    {
        public static readonly string[] Names={"axe","lightning","bow","spear","sunburst","frost","overdrive","resonance","compass","hourglass","home","dash","heart","dawn","coil","bell","lantern","lens","amber","satellite","gold","skull","jump","shield","shrine","blade","boots","clover","meal","potion","stars","gloves","wingboots","fang","target","book","fireorb","magnet","flameblade","icecore","stormamulet","necronomicon","chronosphere","portal","crown","dragon","wings","infinity","blackhole","rebirth","timemaster","cube","ring","amulet","leaf","armor","arrow","aura"};
        [MenuItem("ERA/Create interface icons")]
        public static void Prepare()
        {
            const string root="Assets/Era/Resources/Icons";Directory.CreateDirectory(root);bool refresh=!File.Exists(root+"/Clean-v07.txt");
            foreach(string name in Names)
            {
                string path=root+"/"+name+".png";if(File.Exists(path)&&!refresh)continue;
                var drawing=new Drawing();Draw(name,drawing);var texture=drawing.Texture();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.GUI;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            File.WriteAllText(root+"/Clean-v07.txt","Original centered icons v0.7; individual transparent 128x128 textures, no atlas cropping.");
        }
        static void Draw(string n,Drawing d)
        {
            switch(n)
            {
                case "blade":d.Poly(new[]{new Vector2(88,16),new Vector2(96,45),new Vector2(55,86),new Vector2(42,73)},false);d.Line(34,66,63,95,7);d.Line(47,82,27,103,10);d.Line(22,98,31,108,8);break;
                case "boots":Boot(d);break;
                case "wingboots":Boot(d);d.Path(new[]{new Vector2(83,44),new Vector2(108,24),new Vector2(101,51),new Vector2(89,62)},5);d.Line(90,46,103,39,4);break;
                case "clover":for(int i=0;i<4;i++){float a=i*Mathf.PI/2;d.Circle(64+Mathf.Cos(a)*20,54+Mathf.Sin(a)*20,17,true);}d.Path(new[]{new Vector2(64,54),new Vector2(68,91),new Vector2(54,109)},6);break;
                case "meal":d.Circle(64,67,32);d.Circle(64,67,23);d.Line(18,27,18,102,5);d.Line(10,28,10,48,4);d.Line(26,28,26,48,4);d.Line(10,48,26,48,5);d.Line(109,27,109,102,6);d.Line(102,28,102,62,7);break;
                case "potion":d.Path(new[]{new Vector2(50,19),new Vector2(50,42),new Vector2(29,72),new Vector2(30,96),new Vector2(42,108),new Vector2(86,108),new Vector2(98,96),new Vector2(99,72),new Vector2(78,42),new Vector2(78,19)},5);d.Line(46,20,82,20,9);d.Line(37,77,91,77,5);d.Line(64,84,64,100,5);d.Line(56,92,72,92,5);break;
                case "stars":Star(d,50,48,28);Star(d,90,82,19);Star(d,34,100,12);break;
                case "gloves":d.Path(new[]{new Vector2(38,103),new Vector2(27,62),new Vector2(31,50),new Vector2(46,62),new Vector2(45,30),new Vector2(54,27),new Vector2(60,58),new Vector2(61,21),new Vector2(70,21),new Vector2(74,57),new Vector2(80,28),new Vector2(89,31),new Vector2(87,65),new Vector2(97,49),new Vector2(105,55),new Vector2(92,102),new Vector2(38,103)},5);d.Line(43,89,89,89,5);break;
                case "fang":d.Poly(new[]{new Vector2(31,27),new Vector2(95,27),new Vector2(91,63),new Vector2(74,90),new Vector2(46,111),new Vector2(53,69)},false);d.Line(37,42,91,42,5);break;
                case "target":d.Circle(64,64,36);d.Circle(64,64,21);d.Circle(64,64,6,true);d.Line(64,12,64,35,5);d.Line(64,93,64,116,5);d.Line(12,64,35,64,5);d.Line(93,64,116,64,5);break;
                case "book":Book(d);d.Line(39,45,51,45,4);d.Line(39,58,51,58,4);d.Line(78,45,90,45,4);d.Line(78,58,90,58,4);break;
                case "fireorb":d.Circle(64,73,32);Flame(d);break;
                case "magnet":d.Path(new[]{new Vector2(31,25),new Vector2(31,73),new Vector2(38,93),new Vector2(54,104),new Vector2(74,104),new Vector2(90,93),new Vector2(97,73),new Vector2(97,25),new Vector2(78,25),new Vector2(78,72),new Vector2(73,82),new Vector2(55,82),new Vector2(50,72),new Vector2(50,25),new Vector2(31,25)},6);d.Line(31,45,50,45,5);d.Line(78,45,97,45,5);break;
                case "flameblade":Draw("blade",d);d.Path(new[]{new Vector2(34,55),new Vector2(24,44),new Vector2(34,19),new Vector2(41,35)},5);d.Path(new[]{new Vector2(89,80),new Vector2(107,63),new Vector2(108,89),new Vector2(93,105)},5);break;
                case "icecore":d.Poly(new[]{new Vector2(64,17),new Vector2(97,49),new Vector2(87,92),new Vector2(64,110),new Vector2(34,86),new Vector2(30,48)},false);d.Line(64,24,64,103,4);d.Line(36,49,91,86,4);d.Line(33,84,91,50,4);break;
                case "stormamulet":Amulet(d);d.Poly(new[]{new Vector2(69,55),new Vector2(53,78),new Vector2(66,78),new Vector2(59,96),new Vector2(78,71),new Vector2(64,72)},true);break;
                case "necronomicon":Book(d);d.Circle(64,65,18);d.Circle(56,63,4,true);d.Circle(72,63,4,true);d.Line(57,77,72,77,5);break;
                case "chronosphere":d.Circle(64,64,43);Draw("hourglass",d);break;
                case "portal":d.Arc(64,64,42,.15f,Mathf.PI*1.85f);d.Arc(64,64,28,1,Mathf.PI*2.6f);d.Arc(64,64,13,2,7);d.Line(105,46,108,65,6);d.Line(108,65,92,55,6);break;
                case "crown":d.Poly(new[]{new Vector2(27,90),new Vector2(17,38),new Vector2(45,58),new Vector2(64,22),new Vector2(83,58),new Vector2(111,38),new Vector2(101,90)},false);d.Line(29,104,99,104,7);d.Circle(64,76,7,true);break;
                case "dragon":d.Poly(new[]{new Vector2(29,42),new Vector2(52,20),new Vector2(57,41),new Vector2(88,46),new Vector2(108,65),new Vector2(91,81),new Vector2(68,73),new Vector2(55,108),new Vector2(24,101),new Vector2(37,76)},false);d.Circle(68,54,4,true);d.Line(83,67,101,67,4);d.Line(28,52,17,42,6);break;
                case "wings":Wing(d,false);Wing(d,true);d.Line(64,65,64,104,6);break;
                case "infinity":d.Path(new[]{new Vector2(64,64),new Vector2(44,41),new Vector2(27,39),new Vector2(16,51),new Vector2(16,76),new Vector2(30,89),new Vector2(45,86),new Vector2(83,42),new Vector2(100,39),new Vector2(112,51),new Vector2(112,76),new Vector2(98,89),new Vector2(83,86),new Vector2(64,64)},8);break;
                case "blackhole":d.Circle(64,64,17,true);d.Arc(64,64,33,.3f,4.9f);d.Arc(64,64,46,2,6.6f);d.Line(36,93,27,104,6);d.Line(27,104,42,103,6);break;
                case "rebirth":d.Arc(64,65,39,.4f,5.6f);d.Path(new[]{new Vector2(87,20),new Vector2(98,43),new Vector2(74,39)},6);d.Line(64,49,64,84,8);d.Line(47,66,81,66,8);break;
                case "timemaster":Draw("hourglass",d);d.Line(12,40,27,40,5);d.Line(101,88,116,88,5);d.Line(12,55,23,55,4);d.Line(105,73,116,73,4);break;
                case "cube":d.Poly(new[]{new Vector2(64,16),new Vector2(107,41),new Vector2(107,87),new Vector2(64,112),new Vector2(21,87),new Vector2(21,41)},false);d.Line(21,41,64,66,5);d.Line(107,41,64,66,5);d.Line(64,66,64,112,5);d.Line(64,16,64,43,4);break;
                case "ring":d.Circle(64,78,29);d.Poly(new[]{new Vector2(45,18),new Vector2(83,18),new Vector2(93,35),new Vector2(64,59),new Vector2(35,35)},false);d.Line(35,35,93,35,4);d.Line(54,18,64,59,4);d.Line(74,18,64,59,4);break;
                case "amulet":Amulet(d);d.Line(64,61,64,93,7);d.Line(49,77,79,77,7);break;
                case "leaf":d.Poly(new[]{new Vector2(103,20),new Vector2(105,60),new Vector2(86,88),new Vector2(56,96),new Vector2(33,80),new Vector2(36,50),new Vector2(64,30)},false);d.Line(23,109,89,40,6);d.Line(51,81,48,55,4);d.Line(66,65,87,65,4);break;
                case "armor":d.Poly(new[]{new Vector2(31,24),new Vector2(49,19),new Vector2(64,36),new Vector2(79,19),new Vector2(97,24),new Vector2(108,51),new Vector2(90,60),new Vector2(87,105),new Vector2(41,105),new Vector2(38,60),new Vector2(20,51)},false);d.Line(64,40,64,100,5);d.Line(41,76,87,76,4);break;
                case "arrow":d.Line(27,103,94,30,7);d.Poly(new[]{new Vector2(76,28),new Vector2(108,16),new Vector2(98,49)},false);d.Path(new[]{new Vector2(35,74),new Vector2(18,75),new Vector2(18,96),new Vector2(39,96),new Vector2(41,83)},5);break;
                case "aura":d.Circle(64,64,15,true);d.Circle(64,64,29);d.Circle(64,64,45);for(int i=0;i<4;i++){float a=i*Mathf.PI/2;d.Circle(64+Mathf.Cos(a)*45,64+Mathf.Sin(a)*45,6,true);}break;
                case "jump":d.Path(new[]{new Vector2(34,66),new Vector2(64,27),new Vector2(94,66)},7);d.Line(64,29,64,93,7);d.Arc(64,74,36,.35f,2.8f);d.Line(29,110,99,110,4);break;
                case "shield":d.Poly(new[]{new Vector2(64,17),new Vector2(103,34),new Vector2(97,77),new Vector2(64,111),new Vector2(31,77),new Vector2(25,34)},false);d.Line(64,29,64,95,5);d.Line(40,51,88,51,5);break;
                case "shrine":d.Line(27,109,101,109,7);d.Line(37,96,91,96,7);d.Line(43,50,43,93,7);d.Line(85,50,85,93,7);d.Poly(new[]{new Vector2(64,15),new Vector2(82,37),new Vector2(64,59),new Vector2(46,37)},false);break;
                case "axe":d.Line(43,108,77,25,9);d.Poly(new[]{new Vector2(70,28),new Vector2(98,21),new Vector2(110,58),new Vector2(79,57)},true);d.Line(53,87,65,92,5);break;
                case "lightning":d.Line(44,111,67,54,8);d.Poly(new[]{new Vector2(79,12),new Vector2(53,43),new Vector2(76,40),new Vector2(66,66),new Vector2(98,29),new Vector2(77,34)},true);d.Line(28,32,20,22,5);d.Line(105,70,113,75,5);break;
                case "bow":d.Path(new[]{new Vector2(37,14),new Vector2(65,35),new Vector2(77,64),new Vector2(65,94),new Vector2(37,114)},7);d.Line(37,14,37,114,3);d.Line(22,64,108,64,5);d.Line(94,51,109,64,5);d.Line(94,77,109,64,5);d.Line(22,64,15,55,4);d.Line(22,64,15,73,4);break;
                case "spear":d.Line(24,106,86,35,7);d.Poly(new[]{new Vector2(76,33),new Vector2(109,15),new Vector2(95,49)},true);d.Line(36,82,47,93,5);d.Line(27,92,37,102,4);break;
                case "sunburst":d.Circle(64,64,22,true);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;d.Line(64+Mathf.Cos(a)*36,64+Mathf.Sin(a)*36,64+Mathf.Cos(a)*51,64+Mathf.Sin(a)*51,6);}break;
                case "frost":for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Vector2 u=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),v=new Vector2(-u.y,u.x);d.Line(new Vector2(64,64),new Vector2(64,64)+u*48,5);d.Line(new Vector2(64,64)+u*29,new Vector2(64,64)+u*39+v*12,4);d.Line(new Vector2(64,64)+u*29,new Vector2(64,64)+u*39-v*12,4);}break;
                case "overdrive":d.Path(new[]{new Vector2(27,94),new Vector2(54,64),new Vector2(27,34)},7);d.Path(new[]{new Vector2(65,94),new Vector2(94,64),new Vector2(65,34)},7);d.Line(18,109,104,109,4);break;
                case "resonance":d.Line(64,29,64,91,13);d.Line(33,60,95,60,13);d.Arc(64,64,47,0.4f,1.25f);d.Arc(64,64,47,1.9f,2.75f);d.Arc(64,64,47,3.55f,4.4f);d.Arc(64,64,47,5.05f,5.9f);break;
                case "compass":d.Circle(64,64,43);d.Poly(new[]{new Vector2(88,34),new Vector2(72,72),new Vector2(40,94),new Vector2(56,56)},false);d.Circle(64,64,5,true);d.Line(64,10,64,23,5);break;
                case "hourglass":d.Line(31,18,97,18,7);d.Line(31,110,97,110,7);d.Path(new[]{new Vector2(38,20),new Vector2(38,40),new Vector2(86,87),new Vector2(86,109)},5);d.Path(new[]{new Vector2(90,20),new Vector2(90,40),new Vector2(42,87),new Vector2(42,109)},5);d.Poly(new[]{new Vector2(64,79),new Vector2(45,101),new Vector2(83,101)},true);break;
                case "home":d.Poly(new[]{new Vector2(64,17),new Vector2(102,57),new Vector2(89,102),new Vector2(39,102),new Vector2(26,57)},false);d.Path(new[]{new Vector2(45,67),new Vector2(64,48),new Vector2(83,67)},5);d.Path(new[]{new Vector2(49,65),new Vector2(49,85),new Vector2(79,85),new Vector2(79,65)},5);break;
                case "dash":d.Path(new[]{new Vector2(57,32),new Vector2(94,64),new Vector2(57,96)},8);d.Line(29,64,93,64,7);d.Line(16,43,43,43,5);d.Line(16,85,43,85,5);break;
                case "heart":d.Poly(new[]{new Vector2(64,103),new Vector2(22,64),new Vector2(18,44),new Vector2(28,27),new Vector2(46,25),new Vector2(64,41),new Vector2(82,25),new Vector2(100,27),new Vector2(110,44),new Vector2(106,64)},false);d.Line(43,64,57,64,4);d.Line(57,64,64,51,4);d.Path(new[]{new Vector2(64,51),new Vector2(71,77),new Vector2(78,64),new Vector2(89,64)},4);break;
                case "dawn":d.Poly(new[]{new Vector2(64,15),new Vector2(91,47),new Vector2(79,104),new Vector2(47,104),new Vector2(35,47)},false);d.Line(64,15,58,63,4);d.Line(58,63,79,104,4);d.Line(35,47,58,63,4);d.Line(58,63,91,47,4);break;
                case "coil":d.Line(36,21,36,105,5);d.Line(92,21,92,105,5);for(int i=0;i<5;i++)d.Line(37,29+i*16,91,16+i*16,7);break;
                case "bell":d.Path(new[]{new Vector2(27,89),new Vector2(38,74),new Vector2(38,51),new Vector2(46,33),new Vector2(64,27),new Vector2(82,33),new Vector2(90,51),new Vector2(90,74),new Vector2(101,89),new Vector2(27,89)},5);d.Circle(64,102,7,true);d.Line(64,16,64,27,6);break;
                case "lantern":d.Poly(new[]{new Vector2(40,42),new Vector2(88,42),new Vector2(98,95),new Vector2(64,110),new Vector2(30,95)},false);d.Arc(64,36,18,Mathf.PI,Mathf.PI*2);d.Line(64,48,64,99,4);d.Line(33,90,95,90,4);d.Circle(64,72,9,true);break;
                case "lens":d.Circle(57,54,33);d.Line(81,79,108,106,10);d.Line(44,54,70,54,4);d.Line(57,41,57,67,4);break;
                case "amber":d.Poly(new[]{new Vector2(47,18),new Vector2(87,30),new Vector2(103,72),new Vector2(76,109),new Vector2(29,91),new Vector2(22,46)},false);d.Path(new[]{new Vector2(45,82),new Vector2(64,66),new Vector2(58,43)},5);d.Line(64,66,82,60,4);break;
                case "satellite":d.Circle(65,65,12,true);d.Arc(65,65,38,0,Mathf.PI*1.65f);d.Circle(87,33,9,true);d.Line(40,83,49,78,4);break;
                case "gold":d.Circle(64,64,43);d.Circle(64,64,31);d.Line(64,46,64,82,6);d.Line(55,51,64,46,5);d.Line(54,82,76,82,5);break;
                case "skull":d.Path(new[]{new Vector2(42,101),new Vector2(42,84),new Vector2(28,75),new Vector2(24,52),new Vector2(34,30),new Vector2(64,20),new Vector2(94,30),new Vector2(104,52),new Vector2(100,75),new Vector2(86,84),new Vector2(86,101),new Vector2(42,101)},5);d.Circle(46,60,8,true);d.Circle(82,60,8,true);d.Line(57,90,57,102,4);d.Line(71,90,71,102,4);break;
            }
        }
        static void Boot(Drawing d){d.Path(new[]{new Vector2(41,21),new Vector2(82,21),new Vector2(79,68),new Vector2(104,83),new Vector2(108,104),new Vector2(32,104),new Vector2(29,86),new Vector2(40,64),new Vector2(41,21)},6);d.Line(43,36,80,36,5);d.Line(35,91,105,91,4);}
        static void Book(Drawing d){d.Path(new[]{new Vector2(64,31),new Vector2(43,22),new Vector2(22,25),new Vector2(22,99),new Vector2(43,97),new Vector2(64,108),new Vector2(85,97),new Vector2(106,99),new Vector2(106,25),new Vector2(85,22),new Vector2(64,31),new Vector2(64,108)},5);}
        static void Amulet(Drawing d){d.Arc(64,23,32,0,Mathf.PI);d.Line(32,23,45,63,5);d.Line(96,23,83,63,5);d.Poly(new[]{new Vector2(64,49),new Vector2(90,76),new Vector2(64,110),new Vector2(38,76)},false);}
        static void Flame(Drawing d){d.Path(new[]{new Vector2(47,90),new Vector2(38,73),new Vector2(48,48),new Vector2(55,61),new Vector2(70,19),new Vector2(77,54),new Vector2(91,69),new Vector2(81,91)},5);}
        static void Star(Drawing d,float x,float y,float r){var p=new Vector2[8];for(int i=0;i<8;i++){float a=i*Mathf.PI/4;float length=i%2==0?r:r*.3f;p[i]=new Vector2(x+Mathf.Cos(a)*length,y+Mathf.Sin(a)*length);}d.Poly(p,false);}
        static void Wing(Drawing d,bool right){float s=right?1:-1;d.Path(new[]{new Vector2(64,86),new Vector2(64+s*18,44),new Vector2(64+s*47,19),new Vector2(64+s*45,61),new Vector2(64+s*34,89),new Vector2(64+s*13,102)},6);for(int i=0;i<3;i++)d.Line(64+s*(13+i*8),81-i*10,64+s*(37+i*3),47-i*7,4);}
        sealed class Drawing
        {
            readonly float[] alpha=new float[256*256];
            public void Line(float x,float y,float a,float b,float width=6)=>Line(new Vector2(x,y),new Vector2(a,b),width);
            public void Line(Vector2 a,Vector2 b,float width=6)
            {
                var delta=b-a;float sq=delta.sqrMagnitude;
                Paint(p=>Vector2.Distance(p,a+delta*(sq>0?Mathf.Clamp01(Vector2.Dot(p-a,delta)/sq):0))-width/2);
            }
            public void Path(Vector2[] points,float width=6){for(int i=1;i<points.Length;i++)Line(points[i-1],points[i],width);}
            public void Circle(float x,float y,float radius,bool fill=false)=>Paint(p=>fill?Vector2.Distance(p,new Vector2(x,y))-radius:Mathf.Abs(Vector2.Distance(p,new Vector2(x,y))-radius)-2.7f);
            public void Arc(float x,float y,float r,float from,float to){Vector2 prev=new Vector2(x+Mathf.Cos(from)*r,y+Mathf.Sin(from)*r);for(int i=1;i<=24;i++){float a=Mathf.Lerp(from,to,i/24f);var next=new Vector2(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r);Line(prev,next,5);prev=next;}}
            public void Poly(Vector2[] points,bool fill)
            {
                for(int i=0;i<points.Length;i++)Line(points[i],points[(i+1)%points.Length],5);
                if(!fill)return;Paint(p=>{bool inside=false;for(int i=0,j=points.Length-1;i<points.Length;j=i++){var a=points[i];var b=points[j];if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;}return inside?-1:1;});
            }
            void Paint(Func<Vector2,float> distance){for(int y=0;y<256;y++)for(int x=0;x<256;x++){int at=y*256+x;float value=Mathf.Clamp01(.5f-distance(new Vector2((x+.5f)/2,(y+.5f)/2))*2);alpha[at]=Mathf.Max(alpha[at],value);}}
            public Texture2D Texture()
            {
                var t=new Texture2D(128,128,TextureFormat.RGBA32,false);var pixels=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++){int at=y*2*256+x*2;pixels[(127-y)*128+x]=new Color(.90f,.81f,.57f,(alpha[at]+alpha[at+1]+alpha[at+256]+alpha[at+257])/4);}t.SetPixels(pixels);t.Apply();return t;
            }
        }
    }
}
