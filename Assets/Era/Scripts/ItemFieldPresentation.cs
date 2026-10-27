using UnityEngine;
namespace Era
{
    // Persistent entities use their real item ID/timer; delayed echoes have no fake ground marker.
    public sealed class ItemFieldPresentation:MonoBehaviour
    {
        ItemId item;FeedbackStyle style;Transform core;bool vortex;Renderer[] renderers;
        float age,next,lastTick;bool initialized,armed,facingBound;
        Transform[] wings;Quaternion[] wingRotations;
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        GameObject Part(string name,Mesh mesh,Vector3 at,Vector3 scale,Color color)
        {
            var go=FeedbackGeometry.MeshObject(name,transform,mesh,FeedbackGeometry.Solid);go.transform.localPosition=at;go.transform.localScale=scale;
            block.Clear();block.SetColor("_Color",color);go.GetComponent<Renderer>().SetPropertyBlock(block);return go;
        }
        void Initialize(ItemFieldState field)
        {
            initialized=true;item=(ItemId)field.item;style=field.visual==FeedbackStyle.None?MechanicFeedback.Item(item):field.visual;
            gameObject.name="Mechanic / "+item;
            if(item==ItemId.TimeMaster||item==ItemId.SplitSoul||item==ItemId.EndlessRefrain){renderers=new Renderer[0];return;}
            if(item==ItemId.BlackHole||item==ItemId.ChaosPortal||item==ItemId.VoidShepherd)
            {
                core=Part("Singularity core",FeedbackGeometry.Orb,Vector3.up*.65f,Vector3.one*.9f,new Color(.10f,.07f,.16f,.95f)).transform;
                vortex=true;
                SynthAudio.Instance?.PlaySemantic(FeedbackStyle.Void,field.position);
            }
            else if(item==ItemId.MiserFuse||item==ItemId.PacifistEngine)
            {
                core=Part("Armed movement mine",FeedbackGeometry.Saw,Vector3.up*.12f,Vector3.one*.55f,new Color(.38f,.35f,.25f,1)).transform;
                Part("Detonator",FeedbackGeometry.Shard,Vector3.up*.28f,new Vector3(.16f,.30f,.16f),new Color(.8f,.55f,.18f,1));
            }
            else if(item==ItemId.StarMap&&style!=FeedbackStyle.Lightning)
                core=Part("Falling meteor",FeedbackGeometry.Shard,Vector3.up*10,new Vector3(.7f,1.1f,.7f),new Color(.60f,.24f,.08f,1)).transform;
            else if(item==ItemId.Overgrowth||item==ItemId.WorldSeed)
            {
                var pieces=new CombineInstance[14];
                for(int i=0;i<pieces.Length;i++){float a=i*2.399963f,r=field.radius*Mathf.Sqrt((i+.5f)/pieces.Length);var local=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);local.y=WorldMap.Ground(field.position+local)-field.position.y+.12f;pieces[i]=new CombineInstance{mesh=FeedbackGeometry.Shard,transform=Matrix4x4.TRS(local,Quaternion.Euler(35,i*71,24),new Vector3(.16f,.65f,.16f))};}
                var mesh=new Mesh{name="Root shoots / "+field.id};mesh.CombineMeshes(pieces);var roots=Part("Living roots",mesh,Vector3.zero,Vector3.one,new Color(.35f,.43f,.16f,1));roots.AddComponent<OwnedVisualResource>().mesh=mesh;core=roots.transform;
            }
            else if(item==ItemId.HuntingPack||item==ItemId.GravePact||item==ItemId.HiveMind)
            {
                bool insect=item==ItemId.HiveMind,wolf=item==ItemId.HuntingPack;Color color=insect?new Color(.38f,.62f,.14f,.9f):new Color(.38f,.76f,.70f,.72f);
                core=Part(wolf?"Spectral wolf":insect?"Venom drone":"Revenant",insect?FeedbackGeometry.Orb:wolf?FeedbackGeometry.SpectralWolf:FeedbackGeometry.SpectralSpirit,insect?Vector3.up*.55f:Vector3.zero,insect?new Vector3(.48f,.65f,.45f):Vector3.one,color).transform;
                if(insect)Part("Head",FeedbackGeometry.Orb,new Vector3(0,.78f,0),Vector3.one*.34f,color);
                if(insect){wings=new Transform[2];wingRotations=new Quaternion[2];}
                for(int i=0;i<(insect?2:0);i++)
                {
                    float side=i%2==0?-1:1;var limb=Part(insect?"Venom drone wing":"Spirit limb",FeedbackGeometry.Shard,new Vector3(side*.25f,insect?.65f:.35f,(i/2-.5f)*.4f),insect?new Vector3(.8f,.06f,.26f):new Vector3(.1f,.35f,.1f),color);limb.transform.localRotation=Quaternion.Euler(0,0,side*18);
                    wings[i]=limb.transform;wingRotations[i]=limb.transform.localRotation;
                }
                SynthAudio.Instance?.PlaySemantic(FeedbackStyle.Summon,field.position);
            }
            else if(item==ItemId.ArchitectReign)
            {Part("Tower base",FeedbackGeometry.Orb,Vector3.up*.42f,new Vector3(.9f,.8f,.9f),new Color(.35f,.39f,.36f,1));core=Part("Soul turret focus",FeedbackGeometry.Shard,Vector3.up*1.15f,new Vector3(.48f,.75f,.48f),new Color(.36f,.80f,.65f,1)).transform;}
            renderers=GetComponentsInChildren<Renderer>();
        }
        public void Present(ItemFieldState field,WorldState state)
        {
            if(!initialized)Initialize(field);float dt=PresentationMotion.Delta;age+=dt;next-=dt;transform.position=field.position;
            var camera=GameApp.Instance?.View?.Camera;bool near=camera==null||(field.position-camera.transform.position).sqrMagnitude<60*60;
            foreach(var renderer in renderers)renderer.enabled=near&&PresentationMotion.Intensity>0;
            if(!near||dt<=0)return;
            if(vortex)
            {
                core.localScale=Vector3.one*(.65f+.12f*Mathf.Sin(age*4));core.Rotate(Vector3.up*dt*45);if(next<=0){next=.55f;PresentationFx.EmitStyle(field.position+Vector3.up*.5f,FeedbackStyle.Void,field.radius);}
            }
            else if(item==ItemId.StarMap&&core!=null){core.localPosition=Vector3.up*Mathf.Max(.25f,field.time/.8f*12);core.Rotate(Vector3.one*dt*110);}
            else if(item==ItemId.MiserFuse||item==ItemId.PacifistEngine)
            {if(field.armed&&!armed){armed=true;SynthAudio.Instance?.PlaySemantic(FeedbackStyle.Shield,field.position);}if(armed&&next<=0){next=.7f;PresentationFx.EmitStyle(field.position+Vector3.up*.25f,FeedbackStyle.Gold,.12f);}}
            else if(item==ItemId.HuntingPack||item==ItemId.GravePact||item==ItemId.HiveMind)
            {
                var target=state.enemies.Find(e=>e.id==field.target);var goal=target?.position??state.Player(field.owner)?.position??field.position;var direction=goal-field.position;direction.y=0;
                if(direction.sqrMagnitude>.01f){var facing=Quaternion.LookRotation(direction);transform.rotation=facingBound?Quaternion.Slerp(transform.rotation,facing,1-Mathf.Exp(-12*dt)):facing;facingBound=true;}
                core.localPosition=Vector3.up*((item==ItemId.HiveMind?.55f:.08f)+Mathf.Sin(age*5)*.06f);
                if(wings!=null)for(int i=0;i<wings.Length;i++)wings[i].localRotation=wingRotations[i]*Quaternion.Euler(0,0,(i==0?-1:1)*Mathf.Sin(age*28)*24);
            }
            else if(item==ItemId.Overgrowth||item==ItemId.WorldSeed){core.localScale=Vector3.one*Mathf.Clamp01(age*4);}
            else if((item==ItemId.DashFlint||item==ItemId.AshGarden)&&field.tick>lastTick+.15f)
                PresentationFx.EmitStyle(field.position,FeedbackStyle.Fire,field.radius);
            lastTick=field.tick;
        }
    }
    public sealed class SupportAuraPresentation:MonoBehaviour
    {
        float next;
        public void Present(PlayerState source,WorldState state)
        {
            transform.position=source.position;next-=PresentationMotion.Delta;if(next>0||PresentationMotion.Delta<=0)return;next=.8f;
            float radius=Simulation.AuraRadius(source);PresentationFx.EmitStyle(source.position+Vector3.up*1.1f,FeedbackStyle.Holy,source.auraSurge>0?.5f:.22f);
            foreach(var ally in state.players)if(ally.id!=source.id&&!ally.downed&&!ally.eliminated&&(ally.position-source.position).sqrMagnitude<radius*radius)
                PresentationFx.EmitStyle(ally.position+Vector3.up*.7f,FeedbackStyle.Holy,.17f);
        }
    }
}
