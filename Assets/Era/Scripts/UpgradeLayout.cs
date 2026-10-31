using UnityEngine;
namespace Era
{
    public sealed class UpgradeLayout
    {
        public Rect panel,title,subtitle,footer;
        public Rect[] cards;
        static readonly UpgradeLayout[,] cached=new UpgradeLayout[6,2];
        public static UpgradeLayout Create(int count,bool online)
        {
            count=Mathf.Clamp(count,1,5);int mode=online?1:0;
            if(cached[count,mode]!=null)return cached[count,mode];
            var layout=new UpgradeLayout{cards=new Rect[count]};cached[count,mode]=layout;
            if(online)
            {
                layout.panel=new Rect(1214,235,362,Mathf.Max(380,116+count*92));
                layout.title=new Rect(1232,248,326,49);layout.subtitle=new Rect(1232,298,326,22);
                for(int i=0;i<count;i++)layout.cards[i]=new Rect(1226,331+i*92,338,86);
                return layout;
            }
            bool multiple=count>4;int cols=count==4?4:3;
            float width=cols==4?266:350,gap=20,top=multiple?265:355;
            layout.panel=multiple?new Rect(210,135,1180,660):new Rect(220,238,1160,410);
            layout.title=new Rect(250,multiple?157:260,1100,38);
            layout.subtitle=new Rect(250,multiple?205:305,1100,44);
            layout.footer=new Rect(250,multiple?742:602,1100,26);
            for(int i=0;i<count;i++)
            {
                int row=i/cols,inRow=Mathf.Min(cols,count-row*cols);
                float start=800-(inRow*width+(inRow-1)*gap)/2;
                layout.cards[i]=new Rect(start+i%cols*(width+gap),top+row*228,width,210);
            }
            return layout;
        }
    }
}
