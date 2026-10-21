using UnityEngine;
using System.Collections.Generic;

namespace Era
{
    [DefaultExecutionOrder(100)]
    public sealed class GameHud : MonoBehaviour
    {
        static readonly Color Ink=new Color(.94f,.92f,.84f),Muted=new Color(.61f,.68f,.70f),Panel=new Color(.055f,.085f,.11f,1),Soft=new Color(.12f,.18f,.22f,1),Sage=new Color(.89f,.74f,.42f),Peach=new Color(.26f,.36f,.40f),Lilac=new Color(.63f,.63f,.92f),Red=new Color(.85f,.15f,.21f),Blue=new Color(.19f,.59f,1);
        Font font;GUIStyle label,bold,field;Texture2D disc;FrameCapture capture;
        readonly Queue<RewardNotice> rewardQueue=new Queue<RewardNotice>();readonly HashSet<int> seenRewards=new HashSet<int>();int rewardSeed;float rewardClock;
        bool initialized,capturing;string capturePath;float scale;
        GameApp App=>GameApp.Instance;
        void Init()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Trebuchet MS","Segoe UI","Arial"},20);
            label=new GUIStyle{font=font,wordWrap=true,padding=new RectOffset(0,0,0,0)};bold=new GUIStyle(label){fontStyle=FontStyle.Bold};
            field=new GUIStyle{font=font,fontSize=20,padding=new RectOffset(14,14,10,10)};field.normal.textColor=Ink;field.focused.textColor=Ink;
            disc=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f));pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(31.5f-d));}disc.SetPixels(pixels);disc.Apply();
            initialized=true;
        }
        void Update()
        {
            var s=App?.State;if(s==null){rewardQueue.Clear();seenRewards.Clear();return;}
            if(rewardSeed!=s.seed){rewardSeed=s.seed;rewardQueue.Clear();seenRewards.Clear();rewardClock=0;}
            foreach(var reward in s.rewards)if(reward.owner==App.LocalId&&seenRewards.Add(reward.id)){if(rewardQueue.Count==0)rewardClock=4.5f;rewardQueue.Enqueue(reward);}
            if(rewardQueue.Count>0&&!App.WorldPaused){rewardClock-=Time.unscaledDeltaTime;if(rewardClock<=0){rewardQueue.Dequeue();rewardClock=4.5f;}}
        }
        public void RequestCapture(string path){capturePath=path;}
        void LateUpdate()
        {
            if(capturePath==null || App?.View?.Camera==null)return;if(!initialized)Init();capture??=new FrameCapture(font);capturing=true;scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            try{capture.Save(capturePath,App.View.Camera,Draw);}finally{capturing=false;capturePath=null;}
        }
        void OnGUI()
        {
            if(App==null)return;if(!initialized)Init();scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1600*scale)/2,(Screen.height-900*scale)/2),Quaternion.identity,Vector3.one*scale);
            Draw();GUI.matrix=matrix;
        }
        void Draw(){DrawPage();if(!App.InRun)LanguageToggle(1330,18);else if(!App.Inventory&&!App.Paused)RewardToast();}
        void DrawPage()
        {
            if(App.InRun){if(App.Inventory&&!App.Paused&&App.State.phase==RunPhase.Playing){Inventory();return;}HUD();if(App.State.phase==RunPhase.Victory || App.State.phase==RunPhase.Defeat)Results();else if(App.Paused)Settings(true);else if(App.LocalPlayer?.pendingLevels>0&&!App.LocalPlayer.downed&&!App.LocalPlayer.eliminated)Upgrades();return;}
            if(App.Page==MenuPage.Characters){Characters();return;}
            if(App.State!=null || App.Connecting){Lobby();return;}
            if(App.Page==MenuPage.Connection)Connection();else if(App.Page==MenuPage.HostLobby)HostLobby();else if(App.Page==MenuPage.JoinLobby)JoinLobby();else if(App.Page==MenuPage.Settings)Settings(false);else Home();
        }
        void Flat(float x,float y,float w,float h,Color c)
        {if(w<=0||h<=0)return;if(capturing){capture.Rect(new Rect(x,y,w,h),c);return;}var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=old;}
        void Dot(float x,float y,float size,Color c)
        {if(capturing){capture.Rect(new Rect(x,y,size,size),c,disc);return;}var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x,y,size,size),disc);GUI.color=old;}
        void Box(float x,float y,float w,float h,Color c,float r=16)
        {
            if(w<=0||h<=0)return;
            Flat(x,y+2,w,h,new Color(.015f,.025f,.032f,c.a*.62f));
            Color border=Color.Lerp(new Color(.08f,.12f,.14f,c.a),Sage,.20f);border.a=c.a;
            Flat(x,y,w,h,border);Flat(x+2,y+2,w-4,h-4,c);
        }
        void Text(string value,float x,float y,float w,float h,int size=20,Color? c=null,bool heavy=false,TextAnchor align=TextAnchor.UpperLeft)
        {value=I18n.T(value);if(capturing){capture.Text(value,new Rect(x,y,w,h),size,c??Ink,heavy,align);return;}var style=heavy?bold:label;style.fontSize=size;style.normal.textColor=c??Ink;style.alignment=align;GUI.Label(new Rect(x,y,w,h),value,style);}
        void Icon(string name,float x,float y,float size)
        {var texture=IconLibrary.Get(name);if(texture==null)return;if(capturing){capture.Rect(new Rect(x,y,size,size),Color.white,texture);return;}var old=GUI.color;GUI.color=Color.white;GUI.DrawTexture(new Rect(x,y,size,size),texture);GUI.color=old;}
        void LanguageToggle(float x,float y)
        {if(Button("RU",x,y,62,32,I18n.Russian))App.SetLanguage(GameLanguage.Russian);if(Button("EN",x+70,y,62,32,!I18n.Russian))App.SetLanguage(GameLanguage.English);}
        bool Button(string title,float x,float y,float w,float h,bool primary=false,bool enabled=true)
        {
            var rect=new Rect(x,y,w,h);bool hover=!capturing&&rect.Contains(Event.current.mousePosition)&&enabled;
            Color color=!enabled?new Color(.09f,.12f,.14f,1):primary?new Color(.18f,.30f,.32f,1):new Color(.11f,.17f,.20f,1);if(hover)color=Color.Lerp(color,Color.white,.16f);
            Box(x,y,w,h,color,14);Text(title,x+16,y,w-32,h,18,enabled?Ink:Muted,true,TextAnchor.MiddleCenter);
            if(capturing)return false;bool old=GUI.enabled;GUI.enabled=enabled;bool clicked=GUI.Button(rect,GUIContent.none,GUIStyle.none);GUI.enabled=old;if(clicked)SynthAudio.Instance?.Play(10);return clicked;
        }
        string Field(string value,float x,float y,float w,int max=40)
        {Box(x,y,w,46,Soft,12);if(capturing){Text(value,x+14,y+10,w-28,30);return value;}return GUI.TextField(new Rect(x,y,w,46),value,max,field);}
        void Bar(float x,float y,float w,float h,float progress,Color c)
        {Flat(x,y,w,h,new Color(.025f,.035f,.045f,.9f));float f=w*Mathf.Clamp01(progress);if(f>0){Flat(x,y,f,h,c);Flat(x,y,f,Mathf.Min(2,h/3),Color.Lerp(c,Color.white,.35f));}}
        void Brand(float x=90,float y=84){Text("E R A",x,y,500,100,73,Ink,true);Text("S T I L L W A T E R   R E A C H",x+3,y+111,500,28,14,Sage,true);}
        void Home()
        {
            Box(54,48,570,804,Panel,28);Brand();
            Text(I18n.P("SURVIVE THE RIFT","ПЕРЕЖИВИ РАЗЛОМ"),92,278,480,97,38,Ink,true);
            Text(I18n.P("Slay the horde. Forge your build.\nClaim the blessings of the old gods.","Сражайся с ордой. Собери свой билд.\nПолучи благословения древних богов."),94,400,460,72,21,Muted);
            if(Button("Play solo",92,510,494,62,true))App.BeginSelection(SessionMode.Solo);
            if(Button("Play with a companion",92,590,494,62))App.Page=MenuPage.Connection;
            if(Button("Settings",92,670,238,54))App.Page=MenuPage.Settings;
            if(Button("Quit",348,670,238,54))Application.Quit();
            Text("A 3D survivor for one or two wanderers",94,774,490,40,17,Muted);
            Box(1060,704,476,117,Panel,22);Text("A WORLD WORTH GETTING LOST IN",1085,724,426,25,14,Sage,true);
            Text("Explore the groves. Find treasure.\nClose the rift before it takes you.",1085,763,426,52,19,Muted);
            Text(I18n.P("v0.5   /   Made in Unity","v0.5   /   Создано в Unity"),1200,854,330,26,13,Muted,false,TextAnchor.UpperRight);
            if(App.Status.Length>0)Text(App.Status,674,56,840,55,19,Ink);
        }
        void Characters()
        {
            Box(54,36,590,830,Panel,28);Text("CHOOSE YOUR WANDERER",88,64,518,34,16,Sage,true);
            bool ember=App.SelectedHero==Hero.Ember;
            if(Button("Ember",88,121,250,58,ember))App.SelectedHero=Hero.Ember;
            if(Button("Echo",354,121,250,58,!ember))App.SelectedHero=Hero.Echo;
            Text(ember?"A spark of courage.":"A quiet kind of magic.",88,213,516,50,32,Ink,true);
            Text(ember?"EMBER  /  FIRE WEAVER  /  100 HP":"ECHO  /  FROST KEEPER  /  115 HP",90,278,512,30,15,Sage,true);
            Text(ember?"Fast, fearless and just a little reckless. Turn a tiny spark into a storm.":"Keep your companion close. A little frost and kindness go a long way.",90,323,510,62,20,Muted);
            AbilityInfo(GameData.QIcon(App.SelectedHero),GameData.Q(App.SelectedHero),GameData.QDescription(App.SelectedHero),90,407,510);
            AbilityInfo(GameData.EIcon(App.SelectedHero),GameData.E(App.SelectedHero),GameData.EDescription(App.SelectedHero),90,493,510);
            Text("TAKE ONE RELIC  /  PRESS R TO USE IT",90,588,510,25,13,Sage,true);
            for(int i=0;i<3;i++)if(Button(i==0?"Storm Compass":i==1?"Hourglass":"Home Shard",90+i*174,623,162,45,(int)App.SelectedRelic==i))App.SelectedRelic=(Relic)i;
            Text(GameData.RelicDescription(App.SelectedRelic),90,686,510,52,16,Muted);
            App.PlayerName=Field(App.PlayerName,90,746,510,18);
            if(Button(App.State!=null?"Confirm loadout":App.Mode==SessionMode.Solo?"Begin the journey":"Enter the lobby",90,805,510,45,true))App.ConfirmCharacter();
            if(Button("Back",1358,48,180,48)){if(App.State!=null)App.Page=MenuPage.Home;else App.Page=App.Mode==SessionMode.Solo?MenuPage.Home:App.Mode==SessionMode.Host?MenuPage.HostLobby:MenuPage.JoinLobby;}
            Box(1033,699,456,115,Panel,22);Text(ember?"FOLLOW THE WARMTH":"LEAVE A LITTLE LIGHT",1057,720,409,24,14,Sage,true);
            Text(ember?"Sunburst clears the way.\nOverdrive keeps you moving.":"Frost Bloom holds enemies still.\nResonance heals you both.",1057,757,409,52,19,Muted);
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
            Box(1010,665,500,130,Panel,22);Text(I18n.P("TWO PLAYERS · TCP 7777","ДВА ИГРОКА · TCP 7777"),1038,688,444,26,14,Sage,true);
            Text(I18n.P("Both players need the same game build.\nThe run begins when both are ready.","Обоим игрокам нужна одинаковая сборка.\nЗабег начнётся, когда оба будут готовы."),1038,729,444,54,18,Muted);
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
            Text(I18n.P("1. Create the lobby.\n2. Send your IP to your companion.\n3. Both players press Ready.","1. Создайте лобби.\n2. Отправьте IP напарнику.\n3. Оба игрока нажимают «Готов»."),1044,661,435,116,18,Muted);
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
            for(int i=0;i<2;i++)
            {
                float y=319+i*120;Box(113,y,656,100,Soft,18);
                var p=App.State!=null&&App.State.players.Count>i?App.State.players[i]:null;
                Dot(134,y+27,44,p==null?Panel:GameData.HeroColor(p.hero));Text((i+1).ToString(),134,y+27,44,44,20,Ink,true,TextAnchor.MiddleCenter);
                Text(p?.name??"A little room for a friend",195,y+17,356,34,24,Ink,true);
                Text(p!=null?GameData.HeroName(p.hero)+"  /  "+GameData.RelicName(p.relic):"Waiting for a companion...",196,y+59,380,30,15,Muted);
                Text(p==null?"...":p.ready?"READY":"NOT READY",573,y+34,175,40,15,p?.ready==true?Sage:Muted,true,TextAnchor.MiddleRight);
            }
            var me=App.LocalPlayer;bool ready=me?.ready==true;
            Text(App.State?.readyCountdown>=0?I18n.P("Your journey begins in ","Начинаем через ")+Mathf.CeilToInt(App.State.readyCountdown)+"...":I18n.T("Both players must press Ready to begin."),115,583,650,45,22,Sage,true);
            if(Button(ready?"Not ready yet":"I'm ready",114,654,654,61,true,me!=null))App.ToggleReady();
            if(Button("Change character",114,736,316,49,false,me!=null&&!ready))App.Page=MenuPage.Characters;
            if(Button("Leave lobby",449,736,319,49))App.ReturnToMenu();
            Box(1047,698,461,112,Panel,22);Text("SHARED GOLD. SHARED EXPERIENCE.",1070,718,415,28,14,Sage,true);Text("Your build is your own.\nYour companion always has your back.",1070,755,415,51,18,Muted);
        }
        void HUD()
        {
            var s=App.State;var p=App.LocalPlayer;if(p==null)return;
            Box(24,24,294,86,Panel,16);Text(GameData.HeroName(p.hero),40,32,140,21,12,Sage,true);
            Text(Mathf.CeilToInt(p.hp)+" / "+p.maxHp.ToString("0")+(p.shield>0?" +"+Mathf.CeilToInt(p.shield):""),174,32,126,21,14,Ink,true,TextAnchor.UpperRight);
            Bar(40,58,260,13,p.hp/p.maxHp,Red);Bar(40,84,260,4,s.xp/(float)s.xpRequired,Blue);
            Text(I18n.P("Lv. ","Ур. ")+s.level,40,91,120,19,11,Muted);Text(s.xp+" / "+s.xpRequired+" XP",175,91,125,19,11,Muted,false,TextAnchor.UpperRight);
            Box(736,24,128,52,Panel,14);Text(Clock(s.elapsed),742,27,116,40,27,Ink,true,TextAnchor.MiddleCenter);
            Box(1394,24,182,48,Panel,14);Icon("gold",1405,35,24);Text(s.coins.ToString(),1432,34,52,28,18,Ink,true);Icon("skull",1491,35,24);Text(s.kills.ToString(),1520,34,48,28,18,Ink,true);
            if(App.Online)
            {
                var partner=s.players.Find(o=>o.id!=p.id);Text(partner?.name??I18n.T("Companion disconnected"),40,124,260,24,14,Ink,true);
                if(partner!=null){Bar(40,152,180,5,partner.hp/partner.maxHp,Red);if(partner.downed)Text(I18n.P("Needs help ","Нужна помощь ")+Mathf.CeilToInt(partner.bleedout)+I18n.P(" s"," с"),40,168,260,24,14,Ink,true);}
            }
            int blessingIndex=0;for(int i=0;i<p.blessings.Length;i++)if(p.blessings[i]>0){float x=30+blessingIndex++*37;Box(x,199,32,35,Panel);Icon(GameData.BlessingIcons[i],x+3,201,26);if(p.blessings[i]>1)Text(p.blessings[i].ToString(),x+20,220,10,14,9,Ink,true);if(!capturing&&new Rect(x,199,32,35).Contains(Event.current.mousePosition))Tooltip(GameData.BlessingName(i)+" · "+GameData.BlessingDescription(i),205,244);}
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
                if(d<14)WorldLabel(chest.position+Vector3.up*1.55f,I18n.T(chest.tier>0?"Moon chest":"Chest")+" · "+chest.cost,Panel);
            }
            if(nearest!=null&&!p.downed&&!p.eliminated)
            {Box(600,692,400,42,Panel,12);Text("[ F ]  "+I18n.P("Open","Открыть")+" · "+nearest.cost+I18n.P(" gold"," золота"),610,699,380,30,17,s.coins>=nearest.cost?Sage:Muted,true,TextAnchor.MiddleCenter);}
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
                Box(pos.x-23,pos.y-23,46,46,Panel,12);Icon(drop.kind==RewardKind.Weapon?GameData.Weapons[drop.content].icon:GameData.ItemIcons[drop.content],pos.x-19,pos.y-19,38);
            }
            foreach(var e in s.enemies)if(e.kind==EnemyKind.Warden)
            {Text("THE STILLWATER WARDEN",555,165,490,24,13,Ink,true,TextAnchor.UpperCenter);Bar(555,192,490,7,e.hp/e.maxHp,Red);}
            for(int i=0;i<4;i++)WeaponSlot(p,i,690+i*57,89);
            Ability("Q",GameData.QIcon(p.hero),GameData.Q(p.hero),p.qCooldown,p.hero==Hero.Ember?8:10,690);
            Ability("E",GameData.EIcon(p.hero),GameData.E(p.hero),p.eCooldown,p.hero==Hero.Ember?18:16,747);
            Ability("R",GameData.RelicIcon(p.relic),GameData.RelicName(p.relic),p.relicCooldown,GameData.RelicDuration(p.relic),804);
            Ability("SHIFT","dash",I18n.T("Dash"),p.dashCooldown,3,861);
            Text(I18n.P("CTRL · Crouch   TAB · Map / Stats   ESC · Menu","CTRL · Присед   TAB · Карта / Статистика   ESC · Меню"),1030,862,540,22,12,Ink,false,TextAnchor.UpperRight);
            if(p.downed||p.eliminated)
            {Box(470,320,660,130,Panel,24);Text(p.eliminated?"Keep a little hope.":"You need a helping hand.",500,345,600,42,30,Ink,true,TextAnchor.UpperCenter);Text(p.eliminated?I18n.T("Your companion can still close the rift."):I18n.P("Ask your companion to hold F nearby. ","Напарник должен удерживать F рядом. ")+Mathf.CeilToInt(p.bleedout)+I18n.P(" s left."," с осталось."),500,403,600,40,18,Muted,false,TextAnchor.UpperCenter);}
            if(s.noticeTime>0&&s.noticeKind==1&&!App.UpgradeSelection)
            {Box(500,167,600,42,Panel,12);Text(Notice(s),512,176,576,29,17,Sage,true,TextAnchor.MiddleCenter);}
            if(App.Status.Length>0)Text(App.Status,450,751,700,40,15,Ink,false,TextAnchor.UpperCenter);
            Flat(793,449,5,1,Ink);Flat(802,449,5,1,Ink);Flat(799,443,1,5,Ink);Flat(799,452,1,5,Ink);
        }
        void RewardToast()
        {
            if(rewardQueue.Count==0)return;var n=rewardQueue.Peek();
            string icon=n.kind==RewardKind.Weapon?GameData.Weapons[n.content].icon:n.kind==RewardKind.Blessing?GameData.BlessingIcons[n.content]:GameData.ItemIcons[n.content];
            string title=n.kind==RewardKind.Weapon?GameData.Weapons[n.content].name:n.kind==RewardKind.Blessing?GameData.BlessingName(n.content):GameData.Items[n.content].name;
            string description=n.kind==RewardKind.Weapon?GameData.Weapons[n.content].description:n.kind==RewardKind.Blessing?GameData.BlessingDescription(n.content):GameData.Items[n.content].description;
            float y=650+Mathf.Max(0,rewardClock-4.25f)*70;Box(27,y,467,128,Panel);Flat(28,y+17,3,92,Sage);Icon(icon,45,y+35,48);
            Text(n.kind==RewardKind.Weapon?I18n.P("WEAPON ACQUIRED","ОРУЖИЕ ПОЛУЧЕНО"):n.kind==RewardKind.Blessing?I18n.P("BLESSING RECEIVED","БЛАГОСЛОВЕНИЕ"):I18n.P("ITEM ACQUIRED","ПРЕДМЕТ ПОЛУЧЕН"),109,y+11,370,19,10,Sage,true);
            Text(title+(n.quantity>1?" ×"+n.quantity:""),109,y+34,369,27,20,Ink,true);Text(description,109,y+71,360,48,14,Muted);Bar(39,y+121,443,2,rewardClock/4.5f,Sage);
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
            var point=Project(p.position+Vector3.up*(p.crouching?2.1f:2.6f));if(point.z<=0||point.x<70||point.x>1530||point.y<130||point.y>735)return;
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
            if(!capturing&&new Rect(x,y,49,49).Contains(Event.current.mousePosition))Tooltip(type>=0?GameData.Weapons[type].name+" · "+I18n.P("Lv. ","Ур. ")+p.weaponLevels[slot]:I18n.T("Find weapons in chests"),x+25,y<160?y+58:y-42);
        }
        void Ability(string key,string icon,string title,float cd,float duration,float x)
        {
            const float y=816;Box(x,y,49,49,Panel,12);Icon(icon,x+5,y+3,39);
            if(cd>0){Box(x+3,y+3,43,43,new Color(.025f,.04f,.06f,.85f),9);Text(Mathf.CeilToInt(cd).ToString(),x,y+5,49,33,23,Ink,true,TextAnchor.MiddleCenter);Bar(x+7,y+44,35,2,1-cd/duration,Sage);}
            Text(key,x-1,y+54,51,18,key=="SHIFT"?9:11,Ink,true,TextAnchor.UpperCenter);
            if(!capturing&&new Rect(x,y,49,49).Contains(Event.current.mousePosition))Tooltip(title,x+25,y-42);
        }
        void Tooltip(string title,float x,float y){float h=title.Length>55?76:32;Box(x-155,y,310,h,Panel,10);Text(title,x-145,y+5,290,h-9,14,Ink,true,TextAnchor.UpperCenter);}
        void Minimap(WorldState s,float x,float y,float size)
        {
            float centerX=x+size*.5f,centerY=y+size*.5f,radius=size*.5f,k=(radius-15)/GameData.ArenaRadius;
            Dot(x,y,size,new Color(.07f,.13f,.15f,.98f));
            Flat(x+18,centerY,size-36,1,new Color(.53f,.67f,.62f,.16f));Flat(centerX,y+18,1,size-36,new Color(.53f,.67f,.62f,.16f));
            for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96;Dot(centerX+Mathf.Cos(a)*(radius-5)-1,centerY+Mathf.Sin(a)*(radius-5)-1,2,new Color(Sage.r,Sage.g,Sage.b,.7f));}
            foreach(var landmark in WorldMap.Landmarks)Dot(centerX+landmark.x*k-4,centerY-landmark.z*k-4,8,new Color(.78f,.83f,.73f,.8f));
            foreach(var chest in s.chests)if(!chest.opened)Dot(centerX+chest.position.x*k-3,centerY-chest.position.z*k-3,6,Sage);
            foreach(var shrine in s.shrines)if(!shrine.spent)Dot(centerX+shrine.position.x*k-4,centerY-shrine.position.z*k-4,8,Blue);
            foreach(var e in s.enemies)Dot(centerX+e.position.x*k-2,centerY-e.position.z*k-2,e.kind==EnemyKind.Warden?8:4,Red);
            foreach(var p in s.players)if(!p.eliminated){float marker=p.id==App.LocalId?12:10;Dot(centerX+p.position.x*k-marker*.5f,centerY-p.position.z*k-marker*.5f,marker,p.id==App.LocalId?GameData.Mint:Lilac);}
            Text("N",centerX-15,y+17,30,22,12,Muted,true,TextAnchor.UpperCenter);
        }
        void Upgrades()
        {
            var p=App.LocalPlayer;bool solo=!App.Online;
            if(solo)
            {
                Flat(0,0,1600,900,new Color(.20f,.29f,.25f,.52f));Box(250,248,1100,400,Panel,26);
                Text("A LITTLE STRONGER",290,276,1020,38,25,Sage,true,TextAnchor.UpperCenter);
                Text("The world is paused until you choose.",290,323,1020,30,17,Muted,false,TextAnchor.UpperCenter);
                for(int i=0;i<p.offers.Length&&i<3;i++)UpgradeCard(p,i,280+i*350,382,334,190,true);
                Text(I18n.P("Choose with the mouse or 1 / 2 / 3","Выберите мышью или клавишами 1 / 2 / 3")+(p.pendingLevels>1?I18n.P(" · Remaining: "," · Осталось: ")+p.pendingLevels:""),290,599,1020,28,15,Muted,false,TextAnchor.UpperCenter);
            }
            else
            {
                Box(1232,243,344,396,Panel,20);Text("Choose an upgrade",1250,260,308,29,21,Sage,true);
                Text(I18n.P("1 / 2 / 3 · ALT for cursor","1 / 2 / 3 · ALT для курсора"),1250,297,308,24,14,Muted);
                for(int i=0;i<p.offers.Length&&i<3;i++)UpgradeCard(p,i,1245,334+i*88,318,79,false);
                Text("The fight continues. Choose when ready.",1250,606,310,24,11,Muted);
            }
        }
        void UpgradeCard(PlayerState p,int i,float x,float y,float w,float h,bool large)
        {
            var item=GameData.Items[p.offers[i]];Box(x,y,w,h,Soft,14);
            if(large){Icon(GameData.ItemIcons[p.offers[i]],x+w/2-25,y+12,50);Text((i+1)+" · "+item.name,x+15,y+77,w-30,29,19,Ink,true,TextAnchor.UpperCenter);Text(item.description,x+21,y+119,w-42,61,16,Muted,false,TextAnchor.UpperCenter);}
            else{Icon(GameData.ItemIcons[p.offers[i]],x+9,y+12,34);Text((i+1)+" · "+item.name,x+51,y+9,w-61,24,16,Ink,true);Text(item.description,x+51,y+36,w-61,38,12,Muted);}
            if(!capturing&&GUI.Button(new Rect(x,y,w,h),GUIContent.none,GUIStyle.none)){App.Choose(i);SynthAudio.Instance?.Play(10);}
        }
        void Inventory()
        {
            Flat(0,0,1600,900,new Color(.20f,.29f,.25f,.54f));Box(155,60,1290,780,Panel,28);var p=App.LocalPlayer;var s=App.State;
            Text(I18n.P("MAP & RUN STATUS","КАРТА И СТАТИСТИКА"),195,91,1210,42,28,Ink,true);
            Text(I18n.P("Stillwater Reach","Тиховодье"),195,130,590,28,15,Sage,true);
            Minimap(s,195,174,570);
            Text(WorldMap.Region(p.position),195,760,570,28,14,Sage,true,TextAnchor.UpperCenter);
            Text(I18n.P("You · Green     Companion · Violet     Chests · Gold     Shrines · Blue","Вы · Зелёный     Напарник · Фиолетовый     Сундуки · Золотой     Святилища · Синий"),190,794,1160,24,12,Muted,false,TextAnchor.UpperCenter);
            Box(825,155,535,555,Soft,22);Text(GameData.HeroName(p.hero),865,190,455,38,25,Ink,true);
            Text(p.name,865,229,455,27,15,Muted);
            Text(I18n.P("HEALTH","ЗДОРОВЬЕ"),865,283,200,22,12,Sage,true);Text(Mathf.CeilToInt(p.hp)+" / "+p.maxHp.ToString("0"),1110,280,210,25,18,Ink,true,TextAnchor.UpperRight);
            Bar(865,315,455,12,p.hp/p.maxHp,Red);
            Text(I18n.P("LEVEL PROGRESS","ПРОГРЕСС УРОВНЯ"),865,350,250,22,12,Sage,true);Text(I18n.P("Level ","Уровень ")+s.level,1110,347,210,25,18,Ink,true,TextAnchor.UpperRight);
            Bar(865,382,455,8,s.xp/(float)s.xpRequired,Blue);Text(s.xp+" / "+s.xpRequired+" XP",865,397,455,22,12,Muted,false,TextAnchor.UpperRight);
            StatLine(I18n.P("Time","Время"),Clock(s.elapsed),865,450);
            StatLine(I18n.P("Enemies defeated","Побеждено врагов"),s.kills.ToString(),865,498);
            StatLine(I18n.P("Gold","Золото"),s.coins.ToString(),865,546);
            StatLine(I18n.P("Chests opened","Открыто сундуков"),s.openedChests+" / "+s.chests.Count,865,594);
            if(App.Online){var partner=s.players.Find(other=>other.id!=p.id);StatLine(I18n.P("Companion","Напарник"),partner?.name??I18n.P("Disconnected","Нет связи"),865,642);}
            Text(I18n.P("TAB to return · The world keeps moving","TAB — вернуться · Мир продолжает двигаться"),825,745,535,32,16,Ink,true,TextAnchor.UpperCenter);
        }
        void StatLine(string name,string value,float x,float y)
        {Text(name,x,y,310,26,16,Muted);Text(value,x+315,y,140,26,18,Ink,true,TextAnchor.UpperRight);Flat(x,y+35,455,1,new Color(.55f,.66f,.62f,.16f));}
        void Settings(bool paused)
        {
            Flat(0,0,1600,900,new Color(.27f,.36f,.31f,.58f));Box(470,115,660,670,Panel,28);Text(paused?"Take a breath.":"Make yourself at home.",510,152,580,53,33,Ink,true);
            Text(paused?(App.Online?"Your companion's world keeps moving.":"Your solo adventure is paused."):"A little music. A little magic.",512,222,576,42,20,Muted);
            Text("Music",513,304,574,28,20,Ink,true);if(!capturing)App.MusicVolume=GUI.HorizontalSlider(new Rect(513,353,574,24),App.MusicVolume,0,.7f);else Bar(513,353,574,6,App.MusicVolume/.7f,Sage);
            Text("Sound effects",513,402,574,28,20,Ink,true);if(!capturing)App.Volume=GUI.HorizontalSlider(new Rect(513,451,574,24),App.Volume,0,1);else Bar(513,451,574,6,App.Volume,Sage);
            Text("Mouse sensitivity",513,498,574,28,20,Ink,true);if(!capturing)App.MouseSensitivity=GUI.HorizontalSlider(new Rect(513,547,574,24),App.MouseSensitivity,.5f,5);else Bar(513,547,574,6,App.MouseSensitivity/5,Sage);
            Text("Language",513,586,360,28,20,Ink,true);LanguageToggle(950,580);
            if(Button(paused?"Keep wandering":"Save & return",512,640,576,55,true)){App.SavePreferences();if(paused)App.Paused=false;else App.Page=MenuPage.Home;}
            if(paused&&Button("Return to main menu",512,719,576,48))App.ReturnToMenu();
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
        void OnDestroy(){capture?.Dispose();if(disc!=null)Destroy(disc);if(font!=null)Destroy(font);}
    }
}
