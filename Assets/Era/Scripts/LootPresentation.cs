using UnityEngine;
using UnityEngine.Rendering;

namespace Era
{
    // One icon, a soft aura, a ground ring and a bounded spark system per item.
    // Higher tiers add a narrow light column and a second orbit; no scene lights or colliders.
    public sealed class LootPresentation:MonoBehaviour
    {
        static Texture2D softTexture;static Sprite glowSprite;static readonly Material[] glowMaterials=new Material[5];
        Transform picture,aura,column;SpriteRenderer auraRenderer,columnRenderer;LineRenderer halo,orbit;ParticleSystem sparks;
        int rank;float shownAge=-1,observedAge=-1;bool collecting;
        public int Rank=>rank;
        public int Variant{get;private set;}
        public Color GlowColor{get;private set;}
        public int ParticleLimit=>24;
        public bool IconOnly=>picture!=null&&picture.GetComponent<SpriteRenderer>()?.sprite==IconLibrary.ItemSprite(item);
        int item;
        static void Prepare()
        {
            if(softTexture!=null)return;
            const int size=64;softTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Loot light / radial alpha",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {float r=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1).magnitude;pixels[y*size+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2.4f));}
            softTexture.SetPixels(pixels);softTexture.Apply();glowSprite=Sprite.Create(softTexture,new Rect(0,0,size,size),Vector2.one*.5f,size,0,SpriteMeshType.FullRect);
            for(int i=0;i<5;i++)
            {int rarity=ItemCatalog.RarityOrder[i];glowMaterials[i]=new Material(Shader.Find("Era/Loot Glow")){name="Loot glow / "+i,mainTexture=softTexture};glowMaterials[i].SetColor("_Tint",GameData.RarityColors[rarity]*1.7f);}
        }
        public void Initialize(LootState drop)
        {
            Prepare();item=drop.content;rank=LootMotion.Rank(LootMotion.Rarity(drop));Variant=LootMotion.Evaluate(drop).variant;GlowColor=GameData.RarityColor(LootMotion.Rarity(drop));
            var icon=new GameObject("Item picture / isolated transparent object");picture=icon.transform;picture.SetParent(transform,false);
            var sr=icon.AddComponent<SpriteRenderer>();sr.sprite=IconLibrary.ItemSprite(item);sr.color=Color.white;sr.sortingOrder=32;
            aura=Glow("Soft rarity aura",out auraRenderer,20);aura.localScale=Vector3.one*(1.7f+rank*.2f);
            halo=Ring("Rarity ground light",.47f+rank*.075f,.023f+rank*.004f);
            if(rank>=2){column=Glow("Rare light column",out columnRenderer,18);column.localScale=new Vector3(.42f+rank*.05f,2.1f+rank*.35f,1);}
            if(rank==4)orbit=Ring("Legendary constellation orbit",.76f,.017f);
            var particles=new GameObject("Rarity sparks / capped 24");particles.transform.SetParent(transform,false);sparks=particles.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.duration=10000;main.loop=false;main.startLifetime=.65f+rank*.13f;main.startSpeed=.22f+rank*.05f;main.startSize=.035f+rank*.006f;main.startColor=new Color(1,1,1,.8f);main.gravityModifier=-.025f;main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.22f+rank*.035f;
            var emission=sparks.emission;emission.rateOverTime=.7f+rank*.55f;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(6+rank*3))});
            var color=sparks.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.85f,.15f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=sparks.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.2f),new Keyframe(.2f,1),new Keyframe(1,0)));
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=glowMaterials[rank];renderer.sortingOrder=25;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        Transform Glow(string name,out SpriteRenderer renderer,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=glowSprite;renderer.sharedMaterial=glowMaterials[rank];renderer.sortingOrder=order;return go.transform;
        }
        LineRenderer Ring(string name,float radius,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=glowMaterials[rank];line.useWorldSpace=false;line.loop=true;line.positionCount=48;line.widthMultiplier=width;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            // A white texture keeps the thin rim visible; per-renderer UV samples the radial core.
            line.textureMode=LineTextureMode.Stretch;
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}return line;
        }
        public void Present(LootState drop,Camera camera)
        {
            if(shownAge<0||drop.age<observedAge-.01f)shownAge=drop.age;
            if(drop.age>observedAge+.12f)shownAge=drop.age;
            observedAge=drop.age;shownAge=Mathf.Min(drop.age+.045f,shownAge+Time.deltaTime);
            var pose=LootMotion.Evaluate(drop,shownAge);transform.position=pose.position;transform.localScale=Vector3.one;
            picture.rotation=camera.transform.rotation*Quaternion.Euler(0,0,pose.roll);picture.localScale=Vector3.one*pose.scale;
            aura.rotation=camera.transform.rotation;auraRenderer.color=new Color(1,1,1,pose.glow*.7f*pose.scale);
            halo.transform.position=drop.origin+Vector3.up*.04f;halo.transform.rotation=Quaternion.identity;halo.startColor=halo.endColor=new Color(1,1,1,pose.glow*.55f*(drop.collecting?pose.scale:1));
            if(column!=null){column.position=drop.origin+Vector3.up*(1.02f+rank*.17f);column.rotation=Quaternion.Euler(0,camera.transform.eulerAngles.y,0);columnRenderer.color=new Color(1,1,1,pose.glow*.24f*(drop.collecting?pose.scale:1));}
            if(orbit!=null){orbit.transform.localRotation=Quaternion.Euler(52,shownAge*32,shownAge*16);orbit.startColor=orbit.endColor=new Color(1,1,1,.65f*pose.glow*pose.scale);}
            if(!sparks.isPlaying&&!collecting&&!drop.collecting){sparks.randomSeed=(uint)(drop.id*197+37);sparks.Play();}
            if(collecting!=drop.collecting){collecting=drop.collecting;var emission=sparks.emission;emission.enabled=!collecting;}
        }
    }
}
