using System;
using UnityEngine;
namespace Era
{
    public sealed partial class GameHud
    {
        readonly HudMotion hudMotion=new HudMotion();readonly HudHealthMeter health=new HudHealthMeter();
        bool tooltipRequested;float drawAlpha=1,pageAge,tipAlpha,xpFill,xpFlash;int motionSeed,lastMotionLevel;string pageToken="",tipText="";int tipItem=-1;Vector2 lastTipPoint;
        void TickPresentation()
        {
            if(App==null)return;float dt=Mathf.Max(0,Time.unscaledDeltaTime);hudMotion.Tick(dt);
            string page=App.InRun?(App.Paused?"pause":App.Inventory?"inventory":App.UpgradeSelection?"upgrades":App.State.phase.ToString()):App.Page+"/"+(App.State!=null?"lobby":"menu");
            if(pageToken!=page){pageToken=page;pageAge=0;}else pageAge+=dt;
            var state=App.State;var p=App.LocalPlayer;
            if(p!=null&&state!=null)
            {
                if(motionSeed!=state.seed){motionSeed=state.seed;lastMotionLevel=state.level;xpFill=state.xp/(float)Mathf.Max(1,state.xpRequired);hudMotion.Clear();}
                health.Set(p.hp/Mathf.Max(1,p.maxHp));health.Tick(dt);
                if(state.level!=lastMotionLevel){xpFlash=1;xpFill=0;lastMotionLevel=state.level;}
                xpFill=PresentationMotion.Damp(xpFill,state.xp/(float)Mathf.Max(1,state.xpRequired),10,dt);xpFlash=Mathf.Max(0,xpFlash-dt*1.5f);
            }
            tipAlpha=PresentationMotion.Damp(tipAlpha,hoveredItem>=0||tooltipRequested?1:0,18,dt);
        }
        Color WithAlpha(Color color){color.a*=drawAlpha;return color;}
        void PauseSettings()=>Settings(true);
        void MenuSettings()=>Settings(false);
        void PageLayer(Action draw)
        {
            if(capturing){draw();return;}
            float old=drawAlpha;var matrix=GUI.matrix;
            float eased=HudMotion.EaseOut(pageAge/.22f);drawAlpha=old*eased;
            GUI.matrix=matrix*Matrix4x4.Translate(Vector3.up*(1-eased)*10);
            try{draw();}finally{GUI.matrix=matrix;drawAlpha=old;}
        }
        void PresentTooltip()
        {
            if(hoveredItem>=0){tipItem=hoveredItem;tipText=null;}
            else if(pendingTooltip!=null){tipText=pendingTooltip;tipItem=-1;lastTipPoint=tooltipPoint;}
            float old=drawAlpha;drawAlpha*=capturing?1:tipAlpha;
            if(drawAlpha>.01f){if(tipItem>=0)ItemTooltip(tipItem);else if(!string.IsNullOrEmpty(tipText)){pendingTooltip=tipText;tooltipPoint=lastTipPoint;DrawTooltip();}}
            drawAlpha=old;
            // The next Update observes only this frame's hover, not the fading cached text.
            if(hoveredItem<0&&pendingTooltip==tipText)pendingTooltip=null;
        }
        void HealthBar(float x,float y,float w,float h)
        {
            Flat(x,y,w,h,new Color(.025f,.035f,.045f,.9f));Flat(x,y,w*(capturing?App.LocalPlayer.hp/App.LocalPlayer.maxHp:health.Trail),h,new Color(.7f,.46f,.34f));
            Flat(x,y,w*(capturing?App.LocalPlayer.hp/App.LocalPlayer.maxHp:health.Value),h,Color.Lerp(Red,Ink,health.Damage*.3f));
        }
    }
}
