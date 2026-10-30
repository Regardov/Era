using System;
using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    // Layout always uses the 1600 x 900 design canvas, never scaled screen pixels.
    // Both IMGUI and exported captures consume these exact same lines and metrics.
    public static class HudTextLayout
    {
        public sealed class Block
        {
            public string[] lines;
            public int size;
            public float lineHeight;
        }
        static readonly Dictionary<string,Block> cache=new Dictionary<string,Block>();
        static readonly Dictionary<string,GUIStyle> measurements=new Dictionary<string,GUIStyle>();
        static readonly GUIContent content=new GUIContent();
        static HudTextLayout(){Font.textureRebuilt+=_=>cache.Clear();}
        public static float Width(Font font,string value,int size,FontStyle style)
        {
            string key=font.GetInstanceID()+"|"+size+"|"+(int)style;
            if(!measurements.TryGetValue(key,out var measure))
            {
                if(measurements.Count>256)measurements.Clear();
                measure=new GUIStyle{font=font,fontSize=size,fontStyle=style,richText=false,wordWrap=false,padding=new RectOffset()};measurements[key]=measure;
            }
            content.text=value??"";return measure.CalcSize(content).x;
        }
        public static Block Fit(Font font,string value,int size,float width,float height,bool bold)
        {
            value=(value??"").Replace("\r","");width=Mathf.Max(1,width);height=Mathf.Max(1,height);
            string key=font.GetInstanceID()+"|"+bold+"|"+size+"|"+width+"|"+height+"|"+value;
            if(cache.TryGetValue(key,out var existing))return existing;
            FontStyle style=bold?FontStyle.Bold:FontStyle.Normal;
            Block result=null;
            int minimum=Mathf.Max(8,Mathf.CeilToInt(size*.7f));
            for(int current=size;current>=minimum;current--)
            {
                font.RequestCharactersInTexture(value,current,style);
                var lines=Wrap(font,value,current,width,style);
                float lineHeight=current*1.3f;
                result=new Block{lines=lines,size=current,lineHeight=lineHeight};
                if(lines.Length*lineHeight<=height+.5f)break;
            }
            if(cache.Count>=1024)cache.Clear();cache[key]=result;return result;
        }
        static string[] Wrap(Font font,string value,int size,float width,FontStyle style)
        {
            var lines=new List<string>();
            foreach(string paragraph in value.Split('\n'))
            {
                string line="";
                foreach(string word in paragraph.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))
                {
                    string next=line.Length==0?word:line+" "+word;
                    if(Width(font,next,size,style)<=width){line=next;continue;}
                    if(line.Length>0){lines.Add(line);line="";}
                    // Long names, addresses and compound words must also stay inside the panel.
                    foreach(char c in word)
                    {
                        if(line.Length>0&&Width(font,line+c,size,style)>width){lines.Add(line);line="";}
                        line+=c;
                    }
                }
                lines.Add(line);
            }
            return lines.ToArray();
        }
        public static float Top(Rect rect,Block block,TextAnchor align)
        {
            bool middle=align==TextAnchor.MiddleCenter||align==TextAnchor.MiddleLeft||align==TextAnchor.MiddleRight;
            bool lower=align==TextAnchor.LowerCenter||align==TextAnchor.LowerLeft||align==TextAnchor.LowerRight;
            return rect.y+(middle?Mathf.Max(0,(rect.height-block.lines.Length*block.lineHeight)/2):lower?Mathf.Max(0,rect.height-block.lines.Length*block.lineHeight):0);
        }
    }
}
