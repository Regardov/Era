using UnityEngine;
using System.Collections.Generic;

namespace Era
{
    [DefaultExecutionOrder(100)]
    public sealed partial class GameHud : MonoBehaviour
    {
        static readonly Color Ink=new Color(.94f,.92f,.84f),Muted=new Color(.61f,.68f,.70f),Panel=new Color(.055f,.085f,.11f,1),Soft=new Color(.12f,.18f,.22f,1),Sage=new Color(.89f,.74f,.42f),Peach=new Color(.26f,.36f,.40f),Lilac=new Color(.63f,.63f,.92f),Red=new Color(.85f,.15f,.21f),Blue=new Color(.19f,.59f,1);
        Font font;GUIStyle label,bold,field;Texture2D disc;FrameCapture capture;
        readonly Queue<RewardNotice> rewardQueue=new Queue<RewardNotice>();readonly HashSet<int> seenRewards=new HashSet<int>();int rewardSeed;float rewardClock;
        bool initialized,capturing;string capturePath;float scale;
        int hoveredItem=-1,itemPage,cataloguePage;bool catalogue,ownedOnly;
        int captureItem=-1;
        int captureAttribute=-1;string pendingTooltip;Vector2 tooltipPoint;
        GameApp App=>GameApp.Instance;
        void Init()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Trebuchet MS","Segoe UI","Arial"},20);
            label=new GUIStyle{font=font,wordWrap=false,padding=new RectOffset(0,0,0,0),clipping=TextClipping.Clip};bold=new GUIStyle(label){fontStyle=FontStyle.Bold};
            field=new GUIStyle{font=font,fontSize=20,padding=new RectOffset(14,14,10,10)};field.normal.textColor=Ink;field.focused.textColor=Ink;
            disc=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f));pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(31.5f-d));}disc.SetPixels(pixels);disc.Apply();
            initialized=true;
        }
        void Update()
        {
            TickPresentation();
            var s=App?.State;if(s==null){rewardQueue.Clear();seenRewards.Clear();return;}
            if(rewardSeed!=s.seed){rewardSeed=s.seed;rewardQueue.Clear();seenRewards.Clear();rewardClock=0;}
            foreach(var reward in s.rewards)if(reward.owner==App.LocalId&&seenRewards.Add(reward.id)){if(rewardQueue.Count==0)rewardClock=4.5f;rewardQueue.Enqueue(reward);}
            if(rewardQueue.Count>0&&!App.WorldPaused){rewardClock-=Time.unscaledDeltaTime;if(rewardClock<=0){rewardQueue.Dequeue();rewardClock=4.5f;}}
        }
        public void RequestCapture(string path){capturePath=path;}
        public void CaptureItemCatalogue(string path,int page,int item=-1){catalogue=true;ownedOnly=false;cataloguePage=page;captureItem=item;App.Inventory=true;capturePath=path;}
        public void CaptureAttribute(string path,int attribute){catalogue=false;captureAttribute=attribute;App.Inventory=true;capturePath=path;}
        void LateUpdate()
        {
            if(capturePath==null || App?.View?.Camera==null)return;if(!initialized)Init();capture??=new FrameCapture(font);capturing=true;scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            try{capture.Save(capturePath,App.View.Camera,Draw);}finally{capturing=false;capturePath=null;captureItem=captureAttribute=-1;}
        }
        void OnGUI()
        {
            if(App==null)return;if(!initialized)Init();scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1600*scale)/2,(Screen.height-900*scale)/2),Quaternion.identity,Vector3.one*scale);
            try{Draw();}finally{GUI.matrix=matrix;}
        }
        void Draw(){drawAlpha=1;tooltipRequested=false;hoveredItem=-1;pendingTooltip=null;DrawPage();if(!App.InRun)LanguageToggle(1330,18);else if(!App.Inventory&&!App.Paused&&!App.UpgradeSelection)RewardToast();if(capturing&&captureAttribute>=0)Tooltip(GameData.Attributes[captureAttribute].name+"\n"+StatHelp(captureAttribute),980,340);if(capturing&&captureItem>=0)hoveredItem=captureItem;PresentTooltip();}
        void DrawPage()
        {
            if(App.InRun){if(App.Inventory&&!App.Paused&&App.State.phase==RunPhase.Playing){PageLayer(Inventory);return;}HUD();if(App.State.phase==RunPhase.Victory || App.State.phase==RunPhase.Defeat)PageLayer(Results);else if(App.Paused)PageLayer(PauseSettings);else if(App.LocalPlayer?.pendingLevels>0&&!App.LocalPlayer.downed&&!App.LocalPlayer.eliminated)PageLayer(Upgrades);return;}
            if(App.Page==MenuPage.Catalogue){PageLayer(MenuCatalogue);return;}
            if(App.Page==MenuPage.Characters){PageLayer(Characters);return;}
            if(App.State!=null || App.Connecting){PageLayer(Lobby);return;}
            if(App.Page==MenuPage.Connection)PageLayer(Connection);else if(App.Page==MenuPage.HostLobby)PageLayer(HostLobby);else if(App.Page==MenuPage.JoinLobby)PageLayer(JoinLobby);else if(App.Page==MenuPage.Settings)PageLayer(MenuSettings);else PageLayer(Home);
        }
        void Flat(float x,float y,float w,float h,Color c)
        {if(w<=0||h<=0)return;c=WithAlpha(c);if(capturing){capture.Rect(new Rect(x,y,w,h),c);return;}var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=old;}
        void Dot(float x,float y,float size,Color c)
        {c=WithAlpha(c);if(capturing){capture.Rect(new Rect(x,y,size,size),c,disc);return;}var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x,y,size,size),disc);GUI.color=old;}
        void Box(float x,float y,float w,float h,Color c,float r=16)
        {
            if(w<=0||h<=0)return;
            Flat(x,y+2,w,h,new Color(.015f,.025f,.032f,c.a*.62f));
            Color border=Color.Lerp(new Color(.08f,.12f,.14f,c.a),Sage,.20f);border.a=c.a;
            Flat(x,y,w,h,border);Flat(x+2,y+2,w-4,h-4,c);
        }
        void Text(string value,float x,float y,float w,float h,int size=20,Color? c=null,bool heavy=false,TextAnchor align=TextAnchor.UpperLeft)
        {
            if(!capturing&&Event.current.type!=EventType.Repaint)return;
            value=I18n.T(value);var rect=new Rect(x,y,w,h);
            if(capturing){capture.Text(value,rect,size,WithAlpha(c??Ink),heavy,align);return;}
            var block=HudTextLayout.Fit(font,value,size,w,h,heavy);var style=heavy?bold:label;style.fontSize=block.size;style.normal.textColor=WithAlpha(c??Ink);
            bool center=align==TextAnchor.UpperCenter||align==TextAnchor.MiddleCenter||align==TextAnchor.LowerCenter;
            bool right=align==TextAnchor.UpperRight||align==TextAnchor.MiddleRight||align==TextAnchor.LowerRight;
            style.alignment=center?TextAnchor.UpperCenter:right?TextAnchor.UpperRight:TextAnchor.UpperLeft;
            float top=HudTextLayout.Top(rect,block,align);
            GUI.BeginGroup(rect);
            for(int i=0;i<block.lines.Length;i++)GUI.Label(new Rect(0,top-y+i*block.lineHeight,w,block.lineHeight),block.lines[i],style);
            GUI.EndGroup();
        }
        void Icon(string name,float x,float y,float size)
        {var texture=IconLibrary.Get(name);if(texture==null)return;if(capturing){capture.Rect(new Rect(x,y,size,size),WithAlpha(Color.white),texture);return;}var old=GUI.color;GUI.color=WithAlpha(Color.white);GUI.DrawTexture(new Rect(x,y,size,size),texture);GUI.color=old;}
        void ItemIcon(int item,float x,float y,float size)
        {
            var texture=IconLibrary.ItemTexture(item);if(texture==null)return;
            var uv=IconLibrary.ItemUV(item);float height=size*uv.height*texture.height/(uv.width*texture.width);var rect=new Rect(x,y+(size-height)/2,size,height);
            if(capturing)capture.Rect(rect,WithAlpha(Color.white),texture,uv);else{var previous=GUI.color;GUI.color=WithAlpha(Color.white);GUI.DrawTextureWithTexCoords(rect,texture,uv);GUI.color=previous;}
        }
        void AttributeIcon(int attribute,float x,float y,float size)=>Icon(GameData.AccessoryIcons[attribute],x,y,size);
        void LanguageToggle(float x,float y)
        {if(Button("RU",x,y,62,32,I18n.Russian))App.SetLanguage(GameLanguage.Russian);if(Button("EN",x+70,y,62,32,!I18n.Russian))App.SetLanguage(GameLanguage.English);}
        bool Button(string title,float x,float y,float w,float h,bool primary=false,bool enabled=true)
        {
            var rect=new Rect(x,y,w,h);bool hover=!capturing&&rect.Contains(Event.current.mousePosition)&&enabled;
            string motionKey="button/"+pageToken+"/"+x+"/"+y;float focus=capturing?0:hudMotion.Value(motionKey,hover?1:0,18,0),press=hudMotion.Read(motionKey+"/press");
            Color color=!enabled?new Color(.09f,.12f,.14f,1):primary?new Color(.18f,.30f,.32f,1):new Color(.11f,.17f,.20f,1);color=Color.Lerp(color,Color.white,.14f*focus+.10f*press);
            Box(x,y,w,h,color,14);Text(title,x+16,y,w-32,h,18,enabled?Ink:Muted,true,TextAnchor.MiddleCenter);
            if(focus+press>.01f)Flat(x+12,y+h-3,(w-24)*Mathf.Max(focus,press),1,new Color(Sage.r,Sage.g,Sage.b,.7f*Mathf.Max(focus,press)));
            if(capturing)return false;bool old=GUI.enabled;GUI.enabled=enabled&&old;bool clicked=GUI.Button(rect,GUIContent.none,GUIStyle.none);GUI.enabled=old;if(clicked){hudMotion.Pulse(motionKey+"/press");SynthAudio.Instance?.Play(10);}return clicked;
        }
        string Field(string value,float x,float y,float w,int max=40)
        {Box(x,y,w,46,Soft,12);if(capturing){Text(value,x+14,y+10,w-28,30);return value;}return GUI.TextField(new Rect(x,y,w,46),value,max,field);}
        void Bar(float x,float y,float w,float h,float progress,Color c)
        {Flat(x,y,w,h,new Color(.025f,.035f,.045f,.9f));float smooth=capturing?progress:hudMotion.Value("bar/"+pageToken+"/"+x+"/"+y,progress,16);float f=w*Mathf.Clamp01(smooth);if(f>0){Flat(x,y,f,h,c);Flat(x,y,f,Mathf.Min(2,h/3),Color.Lerp(c,Color.white,.35f));}}
        void Brand(float x=90,float y=84){Text("E R A",x,y,500,100,73,Ink,true);Text("S T I L L W A T E R   R E A C H",x+3,y+111,500,28,14,Sage,true);}
        void Home()
        {
            Box(54,48,570,804,Panel,28);Brand();
            Text(I18n.P("SURVIVE THE RIFT","ПЕРЕЖИВИ РАЗЛОМ"),92,278,480,97,38,Ink,true);
            Text(I18n.P("Slay the horde. Forge your build.\nClaim the blessings of the old gods.","Сражайся с ордой. Собери свой билд.\nПолучи благословения древних богов."),94,400,460,72,21,Muted);
            if(Button("Play solo",92,510,494,62,true))App.BeginSelection(SessionMode.Solo);
            if(Button(I18n.P("Play co-op · 2–3 players","Кооператив · 2–3 игрока"),92,590,494,62))App.Page=MenuPage.Connection;
            if(Button(I18n.P("Item catalogue · ","Каталог предметов · ")+ItemCatalog.ActiveCount,92,670,494,48)){App.Page=MenuPage.Catalogue;ownedOnly=false;cataloguePage=0;}
            if(Button("Settings",92,736,238,48))App.Page=MenuPage.Settings;
            if(Button("Quit",348,736,238,48))Application.Quit();
            Text(I18n.P("A 3D survivor for 1–3 wanderers","3D-выживание для 1–3 игроков"),94,808,490,26,14,Muted);
            Box(1060,704,476,117,Panel,22);Text("A WORLD WORTH GETTING LOST IN",1085,724,426,25,14,Sage,true);
            Text("Explore the groves. Find treasure.\nClose the rift before it takes you.",1085,763,426,52,19,Muted);
            Text("v"+Application.version+I18n.P("   /   Seven Realms","   /   Семь миров"),1200,854,330,26,13,Muted,false,TextAnchor.UpperRight);
            if(App.Status.Length>0)Text(App.Status,674,56,840,55,19,Ink);
        }
        void Characters()
        {
            var hero=App.SelectedHero;int weapon=(int)GameData.StarterWeapon(hero);
            Box(54,78,430,788,Panel);Text(I18n.P("CHOOSE YOUR HERO","ВЫБОР ПЕРСОНАЖА"),82,105,374,36,22,Sage,true);
            for(int i=0;i<3;i++)if(Button(GameData.HeroName((Hero)i),82,162+i*61,374,49,(int)hero==i))App.SelectedHero=(Hero)i;
            Text(I18n.P("YOUR NAME","ВАШЕ ИМЯ"),82,367,374,23,13,Muted,true);App.PlayerName=Field(App.PlayerName,82,398,374,18);
            Text("TAKE ONE RELIC  /  PRESS R TO USE IT",82,465,374,23,12,Sage,true);
            for(int i=0;i<3;i++)if(Button(i==0?"Storm Compass":i==1?"Hourglass":"Home Shard",82+i*128,504,118,55,(int)App.SelectedRelic==i))App.SelectedRelic=(Relic)i;
            Text(GameData.RelicDescription(App.SelectedRelic),82,583,374,84,16,Muted);
            if(Button(App.State!=null?"Confirm loadout":App.Mode==SessionMode.Solo?"Begin the journey":"Enter the lobby",82,712,374,61,true))App.ConfirmCharacter();
            if(Button("Back",82,794,374,44)){if(App.State!=null)App.Page=MenuPage.Home;else App.Page=App.Mode==SessionMode.Solo?MenuPage.Home:App.Mode==SessionMode.Host?MenuPage.HostLobby:MenuPage.JoinLobby;}
            Box(1030,106,516,700,Panel);Text(GameData.HeroName(hero),1054,128,468,45,33,Ink,true);
            Text(I18n.P("WEAPON","ОРУЖИЕ"),1054,195,468,23,12,Sage,true);Icon(GameData.Weapons[weapon].icon,1056,232,43);
            Text(GameData.Weapons[weapon].name,1113,232,405,30,22,Ink,true);Text(GameData.Weapons[weapon].description,1054,281,468,61,16,Muted);
            Text(I18n.P("Base weapon damage: ","Базовый урон оружия: ")+GameData.WeaponBaseDamage(hero,(WeaponKind)weapon),1054,342,468,20,12,Sage,true);
            Text(I18n.P("PASSIVE ABILITY","ПАССИВНОЕ УМЕНИЕ"),1054,365,468,23,12,Sage,true);Text(GameData.PassiveDescription(hero),1054,400,468,90,17,Ink);
            AbilityInfo(GameData.QIcon(hero),GameData.Q(hero),GameData.QDescription(hero),1054,514,468);
            AbilityInfo(GameData.EIcon(hero),GameData.E(hero),GameData.EDescription(hero),1054,611,468);
            Text(I18n.P("Starting health: ","Начальное здоровье: ")+(hero==Hero.Echo?115:hero==Hero.Lumen?110:100)+I18n.P(" HP · Two weapon slots"," ХП · Два слота оружия"),1054,729,468,45,15,Sage,true);
        }
        void AbilityInfo(string key,string name,string description,float x,float y,float width)
        {Box(x,y,width,80,Soft,14);Icon(key,x+13,y+17,38);Text(name,x+62,y+9,width-75,28,19,Ink,true);Text(description,x+62,y+39,width-75,38,13,Muted);}
        void Connection()
        {
            Box(80,72,720,756,Panel,28);Text(I18n.P("NETWORK PLAY","СЕТЕВАЯ ИГРА"),120,112,640,44,35,Ink,true);
            Text(I18n.P("Choose how you want to meet your companion.","Выберите, как встретиться с напарником."),122,174,620,38,20,Muted);
            Box(120,252,640,176,Soft,20);Text(I18n.P("CREATE A LOBBY","СОЗДАТЬ ЛОББИ"),150,278,580,29,18,Sage,true);
            Text(I18n.P("Your computer becomes the host. Share its IP address with your companion.","Ваш компьютер станет хостом. Передайте напарнику его IP-адрес."),150,320,580,51,17,Muted);
            if(Button(I18n.P("Set up a new lobby","Настроить новое лобби"),150,373,580,44,true))App.Page=MenuPage.HostLobby;
            Box(120,458,640,176,Soft,20);Text(I18n.P("JOIN A LOBBY","ПОДКЛЮЧИТЬСЯ"),150,484,580,29,18,Sage,true);
            Text(I18n.P("Enter the host IP address and connect over LAN or a shared VPN.","Введите IP хоста и подключитесь через локальную сеть или общую VPN."),150,526,580,51,17,Muted);
            if(Button(I18n.P("Connect to a lobby","Подключиться к лобби"),150,579,580,44))App.Page=MenuPage.JoinLobby;
            if(Button("Back",120,744,640,45))App.Page=MenuPage.Home;
            Box(1010,665,500,130,Panel,22);Text(I18n.P("UP TO THREE PLAYERS · TCP 7777","ДО ТРЁХ ИГРОКОВ · TCP 7777"),1038,688,444,26,14,Sage,true);
            Text(I18n.P("Everyone needs the same build.\nThe run begins when all joined players are ready.","Всем нужна одинаковая сборка.\nЗабег начнётся, когда все подключённые готовы."),1038,729,444,54,18,Muted);
        }
        void HostLobby()
        {
            Box(80,72,720,756,Panel,28);Text(I18n.P("CREATE A LOBBY","СОЗДАТЬ ЛОББИ"),120,112,640,44,34,Ink,true);
            Text(I18n.P("Choose your name, then select a hero. The lobby opens on TCP port 7777.","Введите имя, затем выберите героя. Лобби откроется на TCP-порту 7777."),122,174,620,62,19,Muted);
            Text(I18n.P("PLAYER NAME","ИМЯ ИГРОКА"),122,275,620,25,13,Sage,true);App.PlayerName=Field(App.PlayerName,120,308,640,18);
            Box(120,390,640,160,Soft,18);Text(I18n.P("ADDRESS FOR YOUR COMPANION","АДРЕС ДЛЯ НАПАРНИКА"),150,416,580,25,13,Sage,true);
            Text(App.HostAddresses+"  :7777",150,457,580,34,21,Ink,true);
            Text(I18n.P("Use the address from the same LAN or VPN as your companion.","Используйте адрес из общей с напарником локальной сети или VPN."),150,503,580,36,15,Muted);
            if(Button(I18n.P("Choose hero and create lobby","Выбрать героя и создать лобби"),120,606,640,60,true))App.BeginSelection(SessionMode.Host);
            if(Button("Back",120,744,640,45))App.Page=MenuPage.Connection;
            Box(1015,596,495,199,Panel,22);Text(I18n.P("HOW IT WORKS","КАК ЭТО РАБОТАЕТ"),1044,620,435,26,14,Sage,true);
            Text(I18n.P("1. Create the lobby.\n2. Share your IP with up to two friends.\n3. All joined players press Ready.","1. Создайте лобби.\n2. Передайте IP одному или двум друзьям.\n3. Все подключённые нажимают «Готов»."),1044,661,435,116,18,Muted);
            if(App.Status.Length>0)Text(App.Status,850,92,650,80,18,Ink);
        }
        void JoinLobby()
        {
            Box(80,72,720,756,Panel,28);Text(I18n.P("JOIN A LOBBY","ПОДКЛЮЧИТЬСЯ"),120,112,640,44,34,Ink,true);
            Text(I18n.P("Enter the address sent by the host. Both computers must use the same build.","Введите адрес, который прислал хост. На обоих компьютерах должна быть одна сборка."),122,174,620,62,19,Muted);
            Text(I18n.P("PLAYER NAME","ИМЯ ИГРОКА"),122,268,620,25,13,Sage,true);App.PlayerName=Field(App.PlayerName,120,301,640,18);
            Text(I18n.P("HOST IP ADDRESS","IP-АДРЕС ХОСТА"),122,385,620,25,13,Sage,true);App.Address=Field(App.Address,120,418,640);
            Text(I18n.P("Examples: 192.168.1.25 for LAN, or the host VPN address.","Например: 192.168.1.25 в локальной сети или VPN-адрес хоста."),122,480,620,44,15,Muted);
            if(Button(I18n.P("Choose hero and connect","Выбрать героя и подключиться"),120,580,640,60,true))App.BeginSelection(SessionMode.Join);
            if(Button("Back",120,744,640,45))App.Page=MenuPage.Connection;
            Box(1015,617,495,178,Panel,22);Text(I18n.P("CONNECTION CHECK","ПЕРЕД ПОДКЛЮЧЕНИЕМ"),1044,641,435,26,14,Sage,true);
            Text(I18n.P("The host must keep the lobby open.\nAllow ERA through Windows Firewall.","Хост должен держать лобби открытым.\nРазрешите ERA доступ в брандмауэре Windows."),1044,682,435,92,18,Muted);
            if(App.Status.Length>0){Box(840,86,690,105,Panel);Text(App.Status,870,113,630,64,18,Ink);}
        }
        void Lobby()
        {
            Box(76,63,730,775,Panel,28);Text("A COMPANION AWAITS",114,100,653,29,15,Sage,true);
            Text(App.Connecting?"Finding your way...":"Better together.",112,154,655,68,43,Ink,true);
            Text(App.IsHost?I18n.P("YOUR IP  /  ","ВАШ IP  /  ")+App.HostAddresses+"  :7777":I18n.T("Let your companion know you made it."),115,237,648,53,17,Muted);
            for(int i=0;i<GameData.MaxPlayers;i++)
            {
                float y=299+i*96;Box(113,y,656,88,Soft,18);
                var p=App.State!=null&&App.State.players.Count>i?App.State.players[i]:null;
                Dot(134,y+27,44,p==null?Panel:GameData.HeroColor(p.hero));Text((i+1).ToString(),134,y+27,44,44,20,Ink,true,TextAnchor.MiddleCenter);
                Text(p?.name??"A little room for a friend",195,y+17,356,34,24,Ink,true);
                Text(p!=null?GameData.HeroName(p.hero)+"  /  "+GameData.RelicName(p.relic):"Waiting for a companion...",196,y+59,380,30,15,Muted);
                Text(p==null?"...":p.ready?"READY":"NOT READY",573,y+34,175,40,15,p?.ready==true?Sage:Muted,true,TextAnchor.MiddleRight);
            }
            var me=App.LocalPlayer;bool ready=me?.ready==true;
            Text(App.State?.readyCountdown>=0?I18n.P("Your journey begins in ","Начинаем через ")+Mathf.CeilToInt(App.State.readyCountdown)+"...":I18n.P("2–3 players · Ready when everyone is here","2–3 игрока · Нажмите «Готов», когда все в сборе"),115,600,650,45,18,Sage,true);
            if(Button(ready?"Not ready yet":"I'm ready",114,654,654,61,true,me!=null))App.ToggleReady();
            if(Button("Change character",114,736,316,49,false,me!=null&&!ready))App.Page=MenuPage.Characters;
            if(Button("Leave lobby",449,736,319,49))App.ReturnToMenu();
            Box(1047,698,461,112,Panel,22);Text("SHARED GOLD. SHARED EXPERIENCE.",1070,718,415,28,14,Sage,true);Text("Your build is your own.\nYour companion always has your back.",1070,755,415,51,18,Muted);
        }
        void HUD()
        {
            var s=App.State;var p=App.LocalPlayer;if(p==null)return;
            Box(24,24,294,p.shield>0?112:86,Panel,16);Text(GameData.HeroName(p.hero),40,32,140,21,12,Sage,true);
            Text(Mathf.CeilToInt(p.hp)+" / "+p.maxHp.ToString("0"),174,32,126,21,14,Ink,true,TextAnchor.UpperRight);
            HealthBar(40,63,260,13);
            if(p.shield>0){Icon("shield",40,81,20);Text(I18n.P("SHIELD ","ЩИТ ")+Mathf.CeilToInt(p.shield),67,82,233,19,13,Blue,true);Bar(40,104,260,5,p.shield/Mathf.Max(p.maxHp,p.shield),Blue);}
            Text(I18n.P("2 weapon slots · second at Lv.25","2 слота оружия · второй на ур.25"),40,p.shield>0?115:85,260,21,11,Muted);
            Box(672,18,256,35,Panel,7);Text(I18n.P("LEVEL ","УРОВЕНЬ ")+s.level,681,20,125,25,17,Ink,true,TextAnchor.MiddleLeft);
            Text(s.xp+" / "+s.xpRequired+" XP",806,23,111,20,10,Muted,false,TextAnchor.MiddleRight);Bar(681,48,237,2,capturing?s.xp/(float)s.xpRequired:xpFill,Color.Lerp(Blue,Ink,xpFlash));
            Text(Clock(Mathf.Ceil(CampaignData.Remaining(s.stageElapsed))),724,146,152,28,21,CampaignData.Remaining(s.stageElapsed)<60?Red:Ink,true,TextAnchor.UpperCenter);
            Box(1394,24,182,48,Panel,14);Icon("gold",1405,35,24);Text(s.coins.ToString(),1432,34,52,28,18,Ink,true);Icon("skull",1491,35,24);Text(s.kills.ToString(),1520,34,48,28,18,Ink,true);
            Box(1000,24,370,48,Panel);
            Text(s.postBossActive?I18n.P("HORDE ×2 · HP / DMG ×","ОРДА ×2 · ОЗ / УРОН ×")+Mathf.Pow(1.1f,s.postBossGrowthSteps).ToString("0.00"):s.phase==RunPhase.Victory?I18n.P("REALM CLEARED","ЭТАП ПРОЙДЕН"):s.waveActive?I18n.P("HORDE ","ОРДА ")+(s.wave-1)+" · "+I18n.P("INCOMING ","ПРИБЫВАЕТ ")+s.waveQuota:s.nextWave>0?I18n.P("NEXT HORDE IN ","СЛЕДУЮЩАЯ ОРДА ЧЕРЕЗ ")+Mathf.CeilToInt(s.nextWave)+I18n.P(" s"," с"):I18n.P("TEN MINUTES SURVIVED","ДЕСЯТЬ МИНУТ ПРОЙДЕНЫ"),1014,38,342,24,14,Sage,true,TextAnchor.UpperCenter);
            Box(1178,86,398,57,Panel);Text(I18n.P("REALM ","ЭТАП ")+s.stage+" / 7",1193,94,367,18,11,CampaignData.Color(s.stage),true);Text(CampaignData.Name(s.stage),1193,116,367,24,17,Ink,true);
            Minimap(s,1386,160,190,true);
            if(App.Online)
            {
                int row=0;foreach(var partner in s.players)if(partner.id!=p.id)
                {float y=147+row++*41;Text(partner.name+(partner.downed?I18n.P(" · DOWN"," · НУЖНА ПОМОЩЬ"):""),40,y,270,23,13,partner.downed?Red:Ink,true);Bar(40,y+27,180,5,partner.hp/partner.maxHp,Red);if(partner.shield>0)Text("+"+Mathf.CeilToInt(partner.shield),228,y+17,72,20,11,Blue,true);}
            }
            int blessingIndex=0;for(int i=0;i<p.blessings.Length;i++)if(p.blessings[i]>0){float x=30+blessingIndex++*37;Box(x,232,32,35,Panel);Icon(GameData.BlessingIcons[i],x+3,234,26);if(p.blessings[i]>1)Text(p.blessings[i].ToString(),x+20,253,10,14,9,Ink,true);if(!capturing&&new Rect(x,232,32,35).Contains(Event.current.mousePosition))Tooltip(GameData.BlessingName(i)+" · "+GameData.BlessingDescription(i),205,277);}
            foreach(var other in s.players)
            {
                if(other.eliminated)continue;PlayerHealth(other);
                if(other.id!=p.id&&other.downed&&Vector3.Distance(p.position,other.position)<3.5f)
                {Box(548,610,504,64,Panel);Text("Hold F to revive your companion",570,621,460,27,18,Sage,true,TextAnchor.UpperCenter);Bar(570,660,460,5,other.reviveProgress/2.5f,Sage);}
            }
            ChestState nearest=null;float dist=3.2f;
            foreach(var chest in s.chests)
            {
                if(chest.opened)continue;float d=Vector3.Distance(p.position,chest.position);if(d<dist){dist=d;nearest=chest;}
                if(d<14)WorldLabel(chest.position+Vector3.up*1.55f,chest.free?I18n.P("FREE CHEST","БЕСПЛАТНЫЙ СУНДУК"):I18n.T(chest.tier>0?"Moon chest":"Chest")+" · "+chest.cost,Panel);
            }
            if(nearest!=null&&!p.downed&&!p.eliminated)
            {Box(600,692,400,65,Panel,12);Text(nearest.free?I18n.P("[ F ]  Open free chest","[ F ]  Открыть бесплатно"):"[ F ]  "+I18n.P("Open","Открыть")+" · "+nearest.cost+I18n.P(" gold"," золота"),610,699,380,27,17,s.coins>=nearest.cost?Sage:Muted,true,TextAnchor.MiddleCenter);Text(I18n.P("Next price ×5/3 · opened in run: ","Следующая цена ×5/3 · открыто за забег: ")+s.openedChests,610,733,380,19,11,Muted,false,TextAnchor.UpperCenter);}
            foreach(var shrine in s.shrines)
            {
                float d=Vector3.Distance(p.position,shrine.position);if(d>18)continue;
                if(!shrine.spent)WorldLabel(shrine.position+Vector3.up*3.1f,I18n.P("BLESSING SHRINE","СВЯТИЛИЩЕ"),Panel);
                if(d<3&&!shrine.spent&&!p.downed&&!p.eliminated)
                {
                    bool mine=shrine.owner==p.id;Box(595,660,410,78,Panel);Text(shrine.owner>=0&&!mine?I18n.P("A companion is channeling","Напарник заряжает святилище"):I18n.P("HOLD [F] · CHANNEL BLESSING","УДЕРЖИВАЙ [F] · ПОЛУЧИ БАФФ"),612,672,376,25,16,Sage,true,TextAnchor.UpperCenter);
                    Bar(618,710,364,7,shrine.charge/5,Blue);Text((mine?shrine.charge.ToString("0.0"):"0.0")+" / 5.0 "+I18n.P("s","с"),620,723,360,18,11,Muted,false,TextAnchor.UpperCenter);
                }
            }
            foreach(var drop in s.loot)
            {
                var pos=Project(drop.position+Vector3.up*1.15f);if(pos.z<=0||pos.x<100||pos.x>1500||pos.y<160||pos.y>720)continue;
                if(!drop.collecting&&Vector3.Distance(p.position,drop.position)<2.5f)Text("[ F ] "+GameData.Items[drop.content].name,pos.x-150,pos.y+28,300,40,15,GameData.Items[drop.content].color,true,TextAnchor.UpperCenter);
            }
            foreach(var e in s.enemies)if(e.kind==EnemyKind.Warden)
            {Text(CampaignData.BossName(e.bossStage).ToUpperInvariant(),555,66,490,22,13,Ink,true,TextAnchor.UpperCenter);Bar(555,90,490,20,e.hp/e.maxHp,Red);Text(Mathf.CeilToInt(e.hp)+" / "+Mathf.CeilToInt(e.maxHp)+I18n.P(" HP"," ОЗ"),555,91,490,19,13,Ink,true,TextAnchor.UpperCenter);Text(I18n.P("IMMUNE TO STATUS EFFECTS","ИММУНИТЕТ К ЭФФЕКТАМ"),555,115,490,17,10,Sage,true,TextAnchor.UpperCenter);}
            if(s.portal.active)
            {
                WorldLabel(s.portal.position+Vector3.up*4.2f,I18n.P("NEXT REALM","СЛЕДУЮЩИЙ МИР"),Panel);
                if(!p.downed&&!p.eliminated&&Vector3.Distance(p.position,s.portal.position)<3.5f)
                {Box(460,610,680,74,Panel);Text(I18n.P("[ F ]  Enter portal · moves the whole party","[ F ]  Войти в портал · переход всей команды"),477,622,646,26,19,Sage,true,TextAnchor.UpperCenter);Text(CampaignData.Name(s.stage+1),477,655,646,21,14,Muted,false,TextAnchor.UpperCenter);}
            }
            for(int i=0;i<GameData.WeaponSlots;i++)WeaponSlot(p,i,346+i*57,24);
            for(int i=0;i<GameData.AccessorySlots;i++)AccessorySlotCard(p,i,346+i*57,82,49,48,true);
            Text(I18n.P("ACCESSORIES","БИЖУТЕРИЯ"),346,135,220,15,9,Muted);
            EncounterHud(s,p);AuraHud(p);DamageNumbers(s);
            Ability("Q",GameData.QIcon(p.hero),GameData.Q(p.hero),p.qCooldown,p.hero==Hero.Echo?10:8,690);
            Ability("E",GameData.EIcon(p.hero),GameData.E(p.hero),p.eCooldown,p.hero==Hero.Echo?16:18,747);
            Ability("R",GameData.RelicIcon(p.relic),GameData.RelicName(p.relic),p.relicCooldown,GameData.RelicDuration(p.relic),804);
            Ability("SHIFT","dash",I18n.T("Dash"),p.dashCooldown,3,861);
            Text(I18n.P("CTRL · Crouch   TAB · Map / Stats   ESC · Menu","CTRL · Присед   TAB · Карта / Статистика   ESC · Меню"),1030,862,540,22,12,Ink,false,TextAnchor.UpperRight);
            if(p.downed||p.eliminated)
            {Box(470,320,660,130,Panel,24);Text(p.eliminated?"Keep a little hope.":"You need a helping hand.",500,345,600,42,30,Ink,true,TextAnchor.UpperCenter);Text(p.eliminated?I18n.T("Your companion can still close the rift."):I18n.P("Ask your companion to hold F nearby. ","Напарник должен удерживать F рядом. ")+Mathf.CeilToInt(p.bleedout)+I18n.P(" s left."," с осталось."),500,403,600,40,18,Muted,false,TextAnchor.UpperCenter);}
            if(s.noticeTime>0&&s.noticeKind==1&&!App.UpgradeSelection)
            {Box(500,167,600,42,Panel,12);Text(Notice(s),512,176,576,29,17,Sage,true,TextAnchor.MiddleCenter);}
            if(App.Status.Length>0)Text(App.Status,450,751,700,40,15,Ink,false,TextAnchor.UpperCenter);
        }
        void RewardToast()
        {
            if(rewardQueue.Count==0)return;var n=rewardQueue.Peek();
            string icon=n.kind==RewardKind.Aura?GameData.AuraIcons[n.content]:n.kind==RewardKind.Accessory?GameData.AccessoryIcons[n.content]:n.kind==RewardKind.Weapon?GameData.Weapons[n.content].icon:n.kind==RewardKind.Blessing?GameData.BlessingIcons[n.content]:"";
            string title=n.kind==RewardKind.Aura?GameData.AuraName(n.content):n.kind==RewardKind.Accessory?GameData.AccessoryName(n.content):n.kind==RewardKind.Weapon?GameData.Weapons[n.content].name:n.kind==RewardKind.Blessing?GameData.BlessingName(n.content):GameData.Items[n.content].name;
            string description=n.kind==RewardKind.Aura?GameData.AuraDescription(n.content,App.LocalPlayer.auraLevels[n.content]):n.kind==RewardKind.Accessory?GameData.AccessoryDescription(n.content,n.rarity):n.kind==RewardKind.Weapon?GameData.Weapons[n.content].description:n.kind==RewardKind.Blessing?GameData.BlessingDescription(n.content):GameData.Items[n.content].description;
            float oldAlpha=drawAlpha;drawAlpha*=capturing?1:HudMotion.Envelope(4.5f-rewardClock,4.5f,.25f,.3f);float y=650+(1-HudMotion.EaseOut((4.5f-rewardClock)/.25f))*20;Box(27,y,467,128,Panel);Flat(28,y+17,3,92,n.kind==RewardKind.Item?GameData.Items[n.content].color:Sage);if(n.kind==RewardKind.Item)ItemIcon(n.content,40,y+27,65);else Icon(icon,45,y+35,48);
            Text(n.kind==RewardKind.Aura?GameData.AuraProgress(App.LocalPlayer,n.content):n.kind==RewardKind.Weapon?I18n.P("WEAPON ACQUIRED","ОРУЖИЕ ПОЛУЧЕНО"):n.kind==RewardKind.Blessing?I18n.P("BLESSING RECEIVED","БЛАГОСЛОВЕНИЕ"):I18n.P("ITEM ACQUIRED","ПРЕДМЕТ ПОЛУЧЕН"),109,y+11,370,19,10,Sage,true);
            Text(title+(n.kind==RewardKind.Item&&n.quantity>1?" ×"+n.quantity:""),109,y+34,369,27,20,Ink,true);Text(description,109,y+71,360,48,14,Muted);Bar(39,y+121,443,2,rewardClock/4.5f,Sage);drawAlpha=oldAlpha;
        }
        string Notice(WorldState s)
        {
            if(s.noticeKind==1)return I18n.P("Need ","Нужно ")+s.noticeContent+I18n.P(" gold"," золота");
            if(s.noticeKind==2||s.noticeKind==3)
            {string title=s.noticeKind==3?GameData.Weapons[s.noticeContent].name:GameData.Items[s.noticeContent].name;return (s.Player(s.noticeOwner)?.name??"")+" · "+title+(s.noticeQuantity>1?" ×"+s.noticeQuantity:"");}
            return I18n.T(s.notice);
        }
        Vector3 Project(Vector3 position)
        {
            var v=App.View.Camera.WorldToViewportPoint(position);
            return new Vector3((v.x*Screen.width-(Screen.width-1600*scale)/2)/scale,((1-v.y)*Screen.height-(Screen.height-900*scale)/2)/scale,v.z);
        }
        void PlayerHealth(PlayerState p)
        {
            var point=Project(App.View.PlayerHealthAnchor(p));if(point.z<=0||point.x<70||point.x>1530||point.y<130||point.y>735)return;
            float width=p.id==App.LocalId?72:88;Box(point.x-width/2-3,point.y-3,width+6,12,Panel,5);Bar(point.x-width/2,point.y,width,6,p.hp/p.maxHp,Red);
            if(p.id!=App.LocalId)Text(p.downed?"[F] "+p.name:p.name,point.x-100,point.y-26,200,22,12,Ink,true,TextAnchor.UpperCenter);
        }
        void WorldLabel(Vector3 position,string title,Color color)
        {
            var p=Project(position);if(p.z<=0||p.x<180||p.x>1420||p.y<210||p.y>650)return;
            Box(p.x-90,p.y,180,29,color,10);Text(title,p.x-84,p.y+4,168,22,12,Ink,true,TextAnchor.UpperCenter);
        }
        void WeaponSlot(PlayerState p,int slot,float x,float y)
        {
            int type=p.weapons[slot];Box(x,y,49,49,Panel,12);
            if(type>=0){Icon(GameData.Weapons[type].icon,x+5,y+3,39);Text(p.weaponLevels[slot].ToString(),x+32,y+31,12,15,10,Sage,true,TextAnchor.UpperRight);}
            else{Flat(x+19,y+24,11,1,Muted);Text("·",x+4,y+29,13,14,11,Muted);}
            if(type<0)Text("25",x+10,y+9,29,24,16,Muted,true,TextAnchor.UpperCenter);
            if(!capturing&&new Rect(x,y,49,49).Contains(Event.current.mousePosition))Tooltip(type>=0?GameData.Weapons[type].name+" · "+I18n.P("Lv. ","Ур. ")+p.weaponLevels[slot]:I18n.P("Choose your second weapon at level 25","Выберите второе оружие на 25-м уровне"),x+25,y<160?y+58:y-42);
        }
        void EncounterHud(WorldState s,PlayerState p)
        {
            if(!s.bossSpawned&&!s.portal.active)
            {
                float distance=Vector3.Distance(p.position,s.bossStatuePosition);
                if(distance<65)WorldLabel(s.bossStatuePosition+Vector3.up*5.2f,I18n.P("BOSS STATUE","СТАТУЯ БОССА"),Panel);
                if(distance<4&&!p.downed&&!p.eliminated)
                {Box(450,594,700,70,Panel);Text(I18n.P("[ F ]  Touch the stone statue · summon the boss","[ F ]  Коснуться каменной статуи · призвать босса"),465,606,670,24,18,Sage,true,TextAnchor.UpperCenter);Text(CampaignData.BossName(s.stage),465,639,670,21,14,Muted,false,TextAnchor.UpperCenter);}
            }
            foreach(var shrine in s.eliteShrines)
            {
                int remaining=s.enemies.FindAll(e=>e.eliteShrine==shrine.id).Count;float distance=Vector3.Distance(p.position,shrine.position);
                if(distance<50)WorldLabel(shrine.position+Vector3.up*3.1f,shrine.completed?I18n.P("TRIAL COMPLETE","ИСПЫТАНИЕ ПРОЙДЕНО"):shrine.active?I18n.P("ELITES LEFT: ","ОСТАЛОСЬ ЭЛИТЫ: ")+remaining:I18n.P("ELITE SHRINE","СВЯТИЛИЩЕ ЭЛИТЫ"),Panel);
                if(!shrine.active&&!shrine.completed&&distance<3.8f&&!s.bossSpawned&&!s.portal.active&&!p.downed&&!p.eliminated)
                {Box(470,594,660,70,Panel);Text(I18n.P("[ F ]  Summon five elite guardians","[ F ]  Призвать пять элитных стражей"),488,606,624,25,18,Sage,true,TextAnchor.UpperCenter);Text(I18n.P("Defeat all five to earn a free chest","Победите всех пятерых и получите бесплатный сундук"),488,640,624,21,13,Muted,false,TextAnchor.UpperCenter);}
            }
            foreach(var node in s.resourceNodes)if(!node.broken)
            {
                float distance=Vector3.Distance(p.position,node.position);if(distance<13)WorldLabel(node.position+Vector3.up*1.1f,I18n.P("AUTUMN LEAVES","ОСЕННИЕ ЛИСТЬЯ"),Panel);
                if(distance<2.7f&&!p.downed&&!p.eliminated){Box(560,635,480,43,Panel);Text(I18n.P("[ F ]  Scatter leaves · random loot","[ F ]  Разворошить листья · случайный лут"),572,645,456,24,17,Sage,true,TextAnchor.UpperCenter);}
            }
        }
        void AuraHud(PlayerState p)
        {
            int index=0;for(int a=0;a<p.auraBonuses.Length;a++)if(p.auraBonuses[a]>0)
            {
                float x=688+index++*30;Box(x,177,27,29,Panel);Icon(GameData.AuraIcons[a],x+2,179,23);
                if(!capturing&&new Rect(x,177,27,29).Contains(Event.current.mousePosition))Tooltip(GameData.AuraName(a)+" +"+Mathf.RoundToInt(p.auraBonuses[a]*100)+"%",800,211);
            }
            if(p.hero!=Hero.Lumen)return;
            int owned=System.Array.FindAll(p.auraLevels,a=>a>0).Length;
            Box(24,282,294,84+owned*52,Panel,16);
            Text(I18n.P("LUMEN · AURAS ","ЛЮМЕН · АУРЫ ")+owned+" / "+GameData.MaxAuras,40,292,260,22,13,Sage,true);
            Text(I18n.P("Current: ","Текущая: ")+GameData.AuraName(p.currentAura),40,316,260,20,12,Ink);
            Text(Simulation.AuraRadius(p).ToString("0.00")+I18n.P(" m · Conduit Lv."," м · Проводник ур.")+p.weaponLevels[0],40,338,260,19,11,Muted);
            int row=0;for(int a=0;a<p.auraLevels.Length;a++)if(p.auraLevels[a]>0)
            {
                float y=366+row++*52;
                if(a==p.currentAura){Flat(31,y-3,3,43,GameData.AuraColors[a]);Box(38,y-3,267,44,Soft);}
                Icon(GameData.AuraIcons[a],43,y+2,31);
                Text(GameData.AuraName(a),84,y-2,216,22,13,Ink,true);
                Text(GameData.AuraProgress(p,a),84,y+21,216,19,11,GameData.AuraColors[a]);
            }
        }
        void DamageNumbers(WorldState state)
        {
            int drawn=0;
            // Preserve critical/player feedback first; cap clutter in dense aura/chain damage.
            for(int priority=0;priority<2;priority++)for(int i=state.hits.Count-1;i>=0&&drawn<40;i--)
            {
                var hit=state.hits[i];bool important=hit.player||hit.critical||hit.evaded;if(important!=(priority==0))continue;
                var point=Project(hit.position);if(point.z<=0||point.x<30||point.x>1570||point.y<50||point.y>790)continue;
                drawn++;
                float age=1-hit.life/.9f,x=point.x+((hit.id%5)-2)*12,y=point.y-HudMotion.EaseOut(age)*62;
                bool shieldHit=hit.player&&hit.absorbed>=hit.damage&&hit.damage>0;
                string value=hit.evaded?I18n.P("Evade!","Уклонился!"):(shieldHit?"◇":"")+Mathf.Max(1,Mathf.RoundToInt(shieldHit?hit.absorbed:hit.damage-(hit.player?hit.absorbed:0))).ToString()+(hit.critical?"!":"");
                Color c=hit.evaded?GameData.Mint:shieldHit?new Color(.4f,.78f,1):hit.player?new Color(1,.34f,.31f):hit.critical?new Color(1,.84f,.25f):Color.white;c.a=HudMotion.Envelope(.9f-hit.life,.9f,.04f,.25f);
                int size=hit.critical?28:21;Text(value,x-61,y+2,124,39,size,new Color(.03f,.04f,.06f,c.a),true,TextAnchor.UpperCenter);Text(value,x-62,y,124,39,size,c,true,TextAnchor.UpperCenter);
            }
        }
        void Ability(string key,string icon,string title,float cd,float duration,float x)
        {
            const float y=816;Box(x,y,49,49,Panel,12);Icon(icon,x+5,y+3,39);
            float ready=capturing?0:hudMotion.Value("ready/"+key,cd<=0?1:0,10,cd<=0?1:0);
            if(cd<=0&&ready<.99f)Flat(x+4,y+47,41,2,new Color(Sage.r,Sage.g,Sage.b,Mathf.Sin(ready*Mathf.PI)*.8f));
            if(cd>0){Box(x+3,y+3,43,43,new Color(.025f,.04f,.06f,.85f),9);Text(Mathf.CeilToInt(cd).ToString(),x,y+5,49,33,23,Ink,true,TextAnchor.MiddleCenter);Bar(x+7,y+44,35,2,1-cd/duration,Sage);}
            Text(key,x-1,y+54,51,18,key=="SHIFT"?9:11,Ink,true,TextAnchor.UpperCenter);
            if(!capturing&&new Rect(x,y,49,49).Contains(Event.current.mousePosition))Tooltip(title,x+25,y-42);
        }
        void Tooltip(string title,float x,float y){tooltipRequested=true;pendingTooltip=title;tooltipPoint=new Vector2(x,y);}
        void DrawTooltip(){float h=pendingTooltip.Length>100?152:pendingTooltip.Length>55?100:54,x=Mathf.Clamp(tooltipPoint.x-190,20,1190),y=Mathf.Clamp(tooltipPoint.y,20,880-h);Box(x,y,390,h,Panel);Flat(x+2,y+2,3,h-4,Sage);Text(pendingTooltip,x+15,y+10,360,h-20,15,Ink,true);}
        void Upgrades()
        {
            var p=App.LocalPlayer;bool solo=!App.Online;
            bool aura=p.offerKind==RewardKind.Aura,weapon=p.offerKind==RewardKind.Weapon;
            string title=weapon?I18n.P("LEVEL 25 · CHOOSE YOUR SECOND WEAPON","УРОВЕНЬ 25 · ВЫБЕРИТЕ ВТОРОЕ ОРУЖИЕ"):aura?I18n.P("LUMEN · CHOOSE ONE OF THREE AURAS","ЛЮМЕН · ВЫБЕРИТЕ ОДНУ ИЗ ТРЁХ АУР"):I18n.P("CHOOSE AN ACCESSORY","ВЫБЕРИТЕ АКСЕССУАР");
            if(solo)
            {
                Flat(0,0,1600,900,new Color(.20f,.29f,.25f,.52f));Box(220,238,1160,410,Panel,26);
                Text(title,250,260,1100,38,23,Sage,true,TextAnchor.UpperCenter);
                Text(aura?I18n.P("Three random new auras or upgrades. Up to three aura types.","Три случайные новые ауры или улучшения. До трёх типов аур."):weapon?I18n.P("The world is paused until you choose.","Мир на паузе до выбора."):I18n.P("Four permanent slots this run. Once full, only equipped accessories can improve.","Четыре слота до конца забега. После заполнения улучшается только выбранная бижутерия."),250,305,1100,30,16,Muted,false,TextAnchor.UpperCenter);
                int cols=p.offers.Length>3?4:3;for(int i=0;i<p.offers.Length;i++)UpgradeCard(p,i,245+(i%cols)*(cols==4?280:370),355+(i/cols)*161,cols==4?266:350,210,true);
                Text(I18n.P("Mouse or keys 1–","Мышь или клавиши 1–")+p.offers.Length+I18n.P(" · Level "," · Уровень ")+(p.completedLevel+1),250,602,1100,26,15,Muted,false,TextAnchor.UpperCenter);
            }
            else
            {
                Box(1214,235,362,Mathf.Max(380,116+p.offers.Length*65),Panel,20);Text(title,1232,248,326,49,16,Sage,true);
                Text(I18n.P("1–","1–")+p.offers.Length+I18n.P(" · ALT for cursor"," · ALT для курсора"),1232,298,326,22,13,Muted);
                for(int i=0;i<p.offers.Length;i++)UpgradeCard(p,i,1226,331+i*65,338,59,false);
            }
        }
        void UpgradeCard(PlayerState p,int i,float x,float y,float w,float h,bool large)
        {
            int attribute=p.offers[i],rarity=i<p.offerRarities.Length?p.offerRarities[i]:0;
            bool aura=p.offerKind==RewardKind.Aura,weapon=p.offerKind==RewardKind.Weapon;
            Color rarityColor=aura?GameData.AuraColors[attribute]:weapon?Sage:GameData.RarityColors[rarity];
            string name=aura?GameData.AuraName(attribute):weapon?GameData.Weapons[attribute].name:GameData.AccessoryName(attribute);
            string gain=aura?GameData.AuraDescription(attribute,p.auraLevels[attribute]+1):weapon?GameData.Weapons[attribute].description:GameData.AccessoryDescription(attribute,rarity);
            int accessoryLevel=GameData.AccessoryLevel(p,attribute);
            string tag=aura?(p.auraLevels[attribute]>0?I18n.P("RANK ","РАНГ ")+p.auraLevels[attribute]+" → "+(p.auraLevels[attribute]+1)+I18n.P(" · UPGRADES "," · УЛУЧШЕНИЙ ")+p.auraLevels[attribute]:I18n.P("NEW · RANK 1 · UPGRADES 0","НОВАЯ · РАНГ 1 · УЛУЧШЕНИЙ 0")):weapon?I18n.P("SECOND SLOT","ВТОРОЙ СЛОТ"):GameData.RarityName(rarity)+" · "+(accessoryLevel==0?I18n.P("NEW · LV. 1","НОВОЕ · УР. 1"):I18n.P("LV. ","УР. ")+accessoryLevel+" → "+(accessoryLevel+1));
            Flat(x-3,y-3,w+6,h+6,rarityColor);Box(x,y,w,h,Soft,14);
            string icon=aura?GameData.AuraIcons[attribute]:weapon?GameData.Weapons[attribute].icon:GameData.AccessoryIcons[attribute];
            if(large){float iconSize=aura?43:60;Icon(icon,x+w/2-iconSize/2,y+8,iconSize);Text(tag,x+12,y+(aura?54:71),w-24,18,10,rarityColor,true,TextAnchor.UpperCenter);Text((i+1)+" · "+name,x+12,y+(aura?76:96),w-24,29,16,Ink,true,TextAnchor.UpperCenter);Text(gain,x+12,y+(aura?108:134),w-24,aura?36:68,weapon?14:15,rarityColor,true,TextAnchor.UpperCenter);}
            else{Icon(icon,x+6,y+7,42);Text((i+1)+" · "+name,x+57,y+4,w-66,20,13,Ink,true);Text(gain,x+57,y+25,w-66,weapon?27:16,11,rarityColor,true);if(!weapon)Text(tag,x+57,y+43,w-66,15,9,Muted);}
            if(!capturing&&GUI.Button(new Rect(x,y,w,h),GUIContent.none,GUIStyle.none)){App.Choose(i);SynthAudio.Instance?.Play(10);}
        }
        void ItemHover(int item,Rect rect)
        {if(!capturing&&rect.Contains(Event.current.mousePosition))hoveredItem=item;}
        void ItemTooltip(int item)
        {
            var definition=GameData.Items[item];var mouse=capturing?new Vector2(1000,460):Event.current.mousePosition;
            float x=Mathf.Clamp(mouse.x+20,45,1100),y=Mathf.Clamp(mouse.y-30,45,520);
            Box(x,y,455,330,Panel);Flat(x+2,y+2,4,326,definition.color);ItemIcon(item,x+15,y+14,46);
            Text(definition.name,x+75,y+13,365,49,21,definition.color,true);
            Text(GameData.RarityName((int)definition.rarity)+" · ×"+(App.LocalPlayer?.items[item]??0),x+75,y+60,365,22,12,Muted,true);
            Text(definition.description,x+17,y+93,421,110,17,Ink);
            Text(I18n.P("STACKING: ","ПОВТОРЫ: ")+(string.IsNullOrEmpty(definition.stacking)?I18n.P("Each copy adds its stated effect.","Каждая копия суммирует указанный эффект."):definition.stacking),x+17,y+207,421,49,13,Sage);
            if(!string.IsNullOrEmpty(definition.synergy))Text(I18n.P("SYNERGY: ","СИНЕРГИЯ: ")+definition.synergy,x+17,y+267,421,51,13,GameData.Mint);
        }
        void ItemCatalogue(PlayerState p)
        {
            Text(I18n.P("Hover an item for effects, stacking and synergies","Наведите курсор: эффект, повторы и синергии"),490,107,1038,27,16,Muted);
            if(Button(ownedOnly?I18n.P("Owned only","Только собранные"):I18n.P("All items","Все предметы"),490,143,230,34,false,p!=null)){ownedOnly=!ownedOnly;cataloguePage=0;}
            var items=new List<int>();foreach(int rarity in ItemCatalog.RarityOrder)for(int n=0;n<GameData.Items.Length;n++)if(ItemCatalog.IsAvailable(n)&&(int)GameData.Items[n].rarity==rarity&&(!ownedOnly||p==null||p.items[n]>0))items.Add(n);
            const int size=30;int pages=Mathf.Max(1,(items.Count+size-1)/size);cataloguePage=Mathf.Clamp(cataloguePage,0,pages-1);
            for(int n=cataloguePage*size;n<items.Count&&n<(cataloguePage+1)*size;n++)
            {
                int item=items[n],cell=n-cataloguePage*size;float x=490+cell%5*207,y=195+cell/5*98;var d=GameData.Items[item];
                Box(x,y,195,88,Soft);Flat(x+2,y+2,3,84,d.color);ItemIcon(item,x+8,y+9,54);Text(d.name,x+69,y+10,118,49,14,d.color,true);Text(p==null?GameData.RarityName((int)d.rarity):"×"+p.items[item],x+69,y+63,118,18,10,Muted);ItemHover(item,new Rect(x,y,195,88));
            }
            if(Button("‹",490,806,65,34))cataloguePage=Mathf.Max(0,cataloguePage-1);
            Text((cataloguePage+1)+" / "+pages+" · "+items.Count+I18n.P(" items"," предметов"),580,812,835,24,14,Muted,false,TextAnchor.UpperCenter);
            if(Button("›",1460,806,65,34))cataloguePage=Mathf.Min(pages-1,cataloguePage+1);
        }
        void MenuCatalogue()
        {
            Flat(0,0,1600,900,new Color(.04f,.07f,.09f,.95f));Box(34,38,1532,824,Panel);
            Text(I18n.P("ITEM CATALOGUE","КАТАЛОГ ПРЕДМЕТОВ"),70,64,1000,36,28,Ink,true);
            Text(I18n.P("Run items · ","Предметы забега · ")+ItemCatalog.ActiveCount,70,154,350,66,25,Sage,true);
            Text(I18n.P("Hover any card to read its effect, stacking and build interactions.","Наведите курсор на карточку: эффект, повторные подборы и взаимодействия для билдов."),70,252,350,110,20,Muted);
            for(int n=0;n<5;n++){int r=ItemCatalog.RarityOrder[n];Flat(70,410+n*48,5,30,GameData.RarityColors[r]);Text(GameData.RarityName(r),91,414+n*48,320,27,18,GameData.RarityColors[r],true);}
            if(Button(I18n.P("Back to menu","Назад в меню"),70,788,350,52))App.Page=MenuPage.Home;
            ItemCatalogue(null);
        }
        void Inventory()
        {
            Flat(0,0,1600,900,new Color(.20f,.29f,.25f,.72f));Box(34,38,1532,824,Panel,20);var p=App.LocalPlayer;var s=App.State;
            Text(I18n.P("MAP, ITEMS & ATTRIBUTES","КАРТА, ПРЕДМЕТЫ И АТРИБУТЫ"),70,62,1460,40,26,Ink,true);
            Text(GameData.HeroName(p.hero)+" · "+p.name+" · "+I18n.P("LEVEL ","УРОВЕНЬ ")+s.level,70,99,430,25,14,Sage,true);
            Text(GameData.PassiveProgress(p),70,124,360,16,11,Sage);
            Minimap(s,70,140,360);MapLegend(70,506);Text(WorldMap.Region(p.position),70,525,360,18,11,Sage,true,TextAnchor.UpperCenter);
            Text(I18n.P("HEALTH","ЗДОРОВЬЕ")+(p.shield>0?I18n.P(" · SHIELD "," · ЩИТ ")+Mathf.CeilToInt(p.shield):""),70,545,228,20,11,p.shield>0?Blue:Muted,true);Text(Mathf.CeilToInt(p.hp)+" / "+p.maxHp.ToString("0"),300,542,130,24,16,Ink,true,TextAnchor.UpperRight);Bar(70,570,360,9,p.hp/p.maxHp,Red);if(p.shield>0)Bar(70,582,360,4,p.shield/Mathf.Max(p.maxHp,p.shield),Blue);
            var odds=ItemCatalog.RarityProbabilities(p.luck,0,1+p.auraBonuses[0]);
            Text(I18n.P("DROP CHANCES · LUCK ","ШАНСЫ ДРОПА · УДАЧА ")+p.luck.ToString("0.#"),70,594,360,20,11,Muted,true);
            for(int rank=0;rank<5;rank++){int r=ItemCatalog.RarityOrder[rank];Text((odds[r]*100).ToString("0.0")+"%",70+rank*72,618,70,23,13,GameData.RarityColors[r],true,TextAnchor.UpperCenter);Flat(74+rank*72,646,64,3,GameData.RarityColors[r]);}
            Text(I18n.P("CHEST ITEMS","ПРЕДМЕТЫ ИЗ СУНДУКОВ"),70,665,360,22,12,Sage,true);
            var owned=new List<int>();for(int item=0;item<p.items.Length;item++)if(p.items[item]>0)owned.Add(item);
            itemPage=Mathf.Clamp(itemPage,0,Mathf.Max(0,(owned.Count-1)/10));
            int shown=0;for(int at=itemPage*10;at<owned.Count&&shown<10;at++){int item=owned[at],col=shown%5,row=shown/5;float x=70+col*72,y=692+row*65;Flat(x-2,y-2,58,58,GameData.Items[item].color);Flat(x,y,54,54,Soft);ItemIcon(item,x,y,54);Text("×"+p.items[item],x+33,y+39,23,15,11,Ink,true,TextAnchor.UpperRight);ItemHover(item,new Rect(x-2,y-2,58,58));shown++;}
            if(shown==0)Text(I18n.P("Open a chest to find your first item.","Откройте сундук, чтобы найти первый предмет."),70,710,360,42,13,Muted);
            if(owned.Count>10){if(Button("‹",70,820,45,27))itemPage=Mathf.Max(0,itemPage-1);Text((itemPage+1)+" / "+((owned.Count+9)/10),130,824,230,21,12,Muted,false,TextAnchor.UpperCenter);if(Button("›",385,820,45,27))itemPage=Mathf.Min((owned.Count-1)/10,itemPage+1);}
            if(Button(catalogue?I18n.P("Attributes","Атрибуты"):I18n.P("Item catalogue · ","Каталог · ")+ItemCatalog.ActiveCount,1190,62,340,35)){catalogue=!catalogue;cataloguePage=0;}
            if(catalogue){ItemCatalogue(p);return;}
            Text(I18n.P("ACCESSORIES · ","БИЖУТЕРИЯ · ")+System.Array.FindAll(p.accessorySlots,a=>a>=0).Length+" / "+GameData.AccessorySlots+I18n.P(" · LOCKED FOR THIS RUN"," · ДО КОНЦА ЗАБЕГА"),490,99,1040,25,15,Sage,true);
            for(int i=0;i<GameData.AccessorySlots;i++)AccessorySlotCard(p,i,490+i*260,140,248,96,false);
            for(int attribute=0;attribute<GameData.Attributes.Length;attribute++)AttributeTile(p,attribute,490+(attribute%5)*207,252+(attribute/5)*105,194,96);
            if(p.hero==Hero.Lumen)
            {
                int index=0;for(int a=0;a<p.auraLevels.Length;a++)if(p.auraLevels[a]>0)
                {
                    float x=697+index++*276;Box(x,672,263,96,Soft);Icon(GameData.AuraIcons[a],x+10,682,30);
                    Text(I18n.P("RANK ","РАНГ ")+p.auraLevels[a],x+48,684,201,22,14,GameData.AuraColors[a],true);
                    Text(GameData.AuraName(a),x+10,717,243,22,12,Ink,true);
                    Text(I18n.P("Upgrades: ","Улучшений: ")+GameData.AuraUpgradeCount(p,a),x+10,745,243,18,11,Muted);
                }
            }
            Text(I18n.P("TAB to return · The world keeps moving","TAB — вернуться · Мир продолжает двигаться"),490,817,1035,25,13,Muted,false,TextAnchor.UpperRight);
        }
        void AttributeTile(PlayerState p,int attribute,float x,float y,float w,float h)
        {
            Box(x,y,w,h,Soft);AttributeIcon(attribute,x+8,y+8,48);Text(GameData.Attributes[attribute].name,x+64,y+12,w-72,36,12,Ink,true);
            Text(AttributeStat(p,attribute),x+64,y+49,w-72,24,17,Sage,true);
            int level=GameData.AccessoryLevel(p,attribute);if(level>0)Text(I18n.P("Equipped · level ","В бижутерии · ур. ")+level,x+9,y+77,w-18,15,10,Muted);
            if(!capturing&&new Rect(x,y,w,h).Contains(Event.current.mousePosition))Tooltip(GameData.Attributes[attribute].name+"\n"+StatHelp(attribute),x+w/2,y+h);
        }
        void AccessorySlotCard(PlayerState p,int slot,float x,float y,float w,float h,bool compact)
        {
            int stat=p.accessorySlots[slot],level=p.accessoryLevels[slot];bool owned=stat>=0;
            Color color=owned?GameData.RarityColors[p.accessoryRarities[slot]]:Muted;
            Box(x,y,w,h,Panel);Flat(x+2,y+2,2,h-4,color);
            if(compact)
            {
                if(owned){Icon(GameData.AccessoryIcons[stat],x+12,y+4,26);Text(level.ToString(),x+4,y+31,w-8,14,10,color,true,TextAnchor.UpperCenter);}
                else Text((slot+1).ToString(),x+5,y+14,w-10,22,15,Muted,true,TextAnchor.UpperCenter);
            }
            else
            {
                if(owned)Icon(GameData.AccessoryIcons[stat],x+12,y+16,42);
                Text(owned?GameData.AccessoryName(stat):I18n.P("Empty slot ","Пустой слот ")+(slot+1),x+64,y+12,w-76,43,14,owned?Ink:Muted,true);
                Text(owned?I18n.P("LEVEL ","УРОВЕНЬ ")+level:I18n.P("Choose on level-up","Выберите при повышении уровня"),x+12,y+70,w-24,18,11,color,true);
            }
            if(owned&&!capturing&&new Rect(x,y,w,h).Contains(Event.current.mousePosition))Tooltip(GameData.AccessoryName(stat)+I18n.P(" · level "," · уровень ")+level+"\n"+GameData.Attributes[stat].name+": "+AttributeStat(p,stat)+I18n.P("\nStays equipped until the run ends.","\nОстаётся в слоте до конца забега."),x+w/2,y+h+5);
        }
        string AttributeStat(PlayerState p,int a)
        {
            switch(a)
            {
                case 0:return "×"+(p.damage*(1+p.auraBonuses[1])).ToString("0.00");case 1:return "×"+(p.attackSpeed*(1+p.auraBonuses[2])).ToString("0.00");case 2:return Mathf.RoundToInt(p.critChance*100)+"%";case 3:return Mathf.RoundToInt(p.critDamage*100)+"%";
                case 4:return p.maxHp.ToString("0");case 5:return "×"+p.healingPower.ToString("0.00");case 6:return p.hpRegen.ToString("0.##")+"/s";case 7:return p.shield.ToString("0");
                case 8:return Mathf.RoundToInt(Mathf.Min(.75f,p.armor)*100)+"%";case 9:return Mathf.RoundToInt(p.lifeSteal*100)+"% · "+GameData.LifestealHealth(p.lifeSteal)+I18n.P(" HP"," ХП");case 10:return "×"+(p.moveSpeed*(1+p.auraBonuses[4])).ToString("0.00");case 11:return (GameData.EvasionChance(p.dodge)*100).ToString("0.#")+"%";
                case 12:return "+"+Mathf.RoundToInt((p.cooldownRate*(1+p.auraBonuses[6])-1)*100)+"%";case 13:return p.weaponRange.ToString("0.#")+" m";case 14:return "×"+p.areaSize.ToString("0.00");case 15:return p.projectileCount.ToString();
                case 16:return "×"+p.projectileSpeed.ToString("0.00");case 17:return p.pickupRange.ToString("0.#")+" m";case 18:return p.luck.ToString("0.#");case 19:return "×"+(p.xpGain*(1+p.auraBonuses[7])).ToString("0.00");default:return "×"+p.goldGain.ToString("0.00");
            }
        }
        static string StatHelp(int a)=>I18n.P(new[]{
            "Multiplies direct weapon and ability damage.","Speeds up automatic weapon volleys; queued shots retain their own interval.","Chance for a direct hit to deal critical damage.","Damage multiplier of a critical hit.","Maximum health. Reaching zero downs the character.","Multiplies healing received, including a successful lifesteal proc.","Health restored each second.","Current shield absorbs incoming damage before health and decays slowly.","Reduces incoming damage, up to 75%.","Chance to heal on a direct weapon hit. Up to 100%: 1 base HP. Above 100%: 2 HP, then +1 per further 50 percentage points. Status damage cannot trigger it.","Multiplies running speed.","Chance to evade an attack. Gain x adds (1-current chance) × x. Maximum 95%; complete immunity is impossible.","Abilities recover faster. This is recharge speed, not a direct percentage reduction of duration.","Extra weapon targeting distance in metres.","Multiplies the radius of area effects.","Projectiles or strikes in each volley. Each base shot hits one enemy; ricochet requires its item.","Multiplies arrow travel speed.","Distance at which gold and experience are attracted.","Improves rarity odds using the logarithmic luck formula.","Multiplies experience earned from enemies.","Multiplies gold earned from enemies."}[a],new[]{
            "Множитель прямого урона оружия и способностей.","Ускоряет автоатаки оружия. Удары внутри очереди сохраняют собственный интервал.","Вероятность критического удара при прямом попадании.","Множитель урона критического удара.","Максимальное здоровье. При нуле персонаж падает.","Усиливает получаемое лечение, включая успешное срабатывание вампиризма.","Здоровье, восстанавливаемое каждую секунду.","Текущий щит поглощает урон раньше здоровья и медленно убывает.","Снижает входящий урон, максимум на 75%.","Шанс лечения за прямое попадание оружием. До 100% — 1 базовый ХП. Выше 100% — 2 ХП, затем +1 за следующие 50 процентных пунктов. Урон статусов не запускает лечение.","Множитель скорости бега.","Шанс избежать атаки. Прибавка x даёт (1−текущий шанс) × x. Предел 95%; полной неуязвимости нет.","Ускоряет восстановление способностей. Это темп восстановления, а не прямое уменьшение времени на указанный процент.","Дополнительная дальность выбора целей оружием в метрах.","Множитель радиуса эффектов по площади.","Снаряды или удары за атаку. Базовый снаряд поражает одну цель; рикошет требует предмета.","Множитель скорости полёта стрел.","Дальность притягивания золота и опыта.","Повышает шансы редкостей по логарифмической формуле удачи.","Множитель опыта, получаемого за врагов.","Множитель золота, получаемого за врагов."}[a]);
        void StatLine(string name,string value,float x,float y)
        {Text(name,x,y,310,26,16,Muted);Text(value,x+315,y,140,26,18,Ink,true,TextAnchor.UpperRight);Flat(x,y+35,455,1,new Color(.55f,.66f,.62f,.16f));}
        void Settings(bool paused)
        {
            Flat(0,0,1600,900,new Color(.27f,.36f,.31f,.58f));Box(470,90,660,760,Panel,28);Text(paused?"Take a breath.":"Make yourself at home.",510,152,580,53,33,Ink,true);
            Text(paused?(App.Online?"Your companion's world keeps moving.":"Your solo adventure is paused."):"A little music. A little magic.",512,222,576,42,20,Muted);
            Text("Music",513,304,574,28,20,Ink,true);if(!capturing)App.MusicVolume=GUI.HorizontalSlider(new Rect(513,353,574,24),App.MusicVolume,0,.7f);else Bar(513,353,574,6,App.MusicVolume/.7f,Sage);
            Text(I18n.P("Now playing: ","Сейчас: ")+MusicCatalog.Title(SynthAudio.Instance!=null?SynthAudio.Instance.CurrentMusicIndex:0),513,376,574,18,12,Sage);
            Text("Sound effects",513,402,574,28,20,Ink,true);if(!capturing)App.Volume=GUI.HorizontalSlider(new Rect(513,451,574,24),App.Volume,0,1);else Bar(513,451,574,6,App.Volume,Sage);
            Text("Mouse sensitivity",513,498,574,28,20,Ink,true);if(!capturing)App.MouseSensitivity=GUI.HorizontalSlider(new Rect(513,547,574,24),App.MouseSensitivity,.5f,5);else Bar(513,547,574,6,App.MouseSensitivity/5,Sage);
            Text(I18n.P("Display mode","Режим экрана"),513,577,280,25,18,Ink,true);
            if(Button(I18n.P("Windowed","Окно"),806,570,120,35,!Screen.fullScreen))App.SetFullscreen(false);
            if(Button(I18n.P("Fullscreen","Полный экран"),934,570,153,35,Screen.fullScreen))App.SetFullscreen(true);
            Text("Language",513,619,360,28,18,Ink,true);LanguageToggle(950,614);
            Text(I18n.P("Visual effects","Интенсивность эффектов"),513,659,285,26,16,Ink,true);if(!capturing)PresentationMotion.Intensity=GUI.HorizontalSlider(new Rect(513,698,275,24),PresentationMotion.Intensity,0,1);
            string frameLabel=App.FrameLimit==0?I18n.P("FPS: Unlimited","FPS: Без лимита"):"FPS: "+App.FrameLimit;
            if(Button(frameLabel,814,668,273,40)){int next=(System.Array.IndexOf(GameApp.FrameLimits,App.FrameLimit)+1)%GameApp.FrameLimits.Length;App.SetFrameLimit(GameApp.FrameLimits[next]);}
            if(Button(paused?"Keep wandering":"Save & return",512,739,576,42,true)){App.SavePreferences();if(paused)App.Paused=false;else App.Page=MenuPage.Home;}
            if(paused&&Button("Return to main menu",512,792,576,36))App.ReturnToMenu();
        }
        void Results()
        {
            var s=App.State;bool won=s.phase==RunPhase.Victory;Flat(0,0,1600,900,new Color(.27f,.36f,.31f,.64f));Box(355,162,890,586,Panel,30);
            Text(won?"A little light wins.":"Every ending is a beginning.",395,209,810,65,40,Ink,true,TextAnchor.UpperCenter);
            Text(won?"The Warden has fallen. Stillwater can breathe again.":"Rest a moment. There is always another way through.",415,302,770,60,23,Muted,false,TextAnchor.UpperCenter);
            string[] values={Clock(s.elapsed),s.kills.ToString(),s.earnedCoins.ToString(),s.openedChests.ToString()};string[] names={"TIME","KILLS","GOLD FOUND","CHESTS"};
            for(int i=0;i<4;i++){float x=393+i*207;Text(values[i],x,419,192,54,35,Ink,true,TextAnchor.UpperCenter);Text(names[i],x,487,192,29,14,Sage,true,TextAnchor.UpperCenter);}
            if(Button("Return to the sanctuary",415,613,770,63,true))App.ReturnToMenu();
        }
        static string Clock(float seconds)=>Mathf.FloorToInt(seconds/60).ToString("00")+":"+Mathf.FloorToInt(seconds%60).ToString("00");
        void OnDestroy(){capture?.Dispose();if(disc!=null)Destroy(disc);if(mapTexture!=null)Destroy(mapTexture);if(font!=null)Destroy(font);}
    }
}
