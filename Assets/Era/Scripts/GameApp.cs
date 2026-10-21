using System;
using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance {get;private set;}
        public WorldState State {get;private set;}
        public Simulation Simulation {get;private set;}
        public bool IsHost {get;private set;}
        public bool Online {get;private set;}
        public bool Connecting {get;private set;}
        public bool Automated {get;private set;}
        public bool Paused,Inventory;
        public int LocalId {get;private set;}
        public string Status="",PlayerName="Wanderer",Address="127.0.0.1";
        public Hero SelectedHero;
        public Relic SelectedRelic;
        public MenuPage Page;
        public SessionMode Mode;
        public string HostAddresses {get;private set;}
        public ArenaView View {get;private set;}
        public float MouseSensitivity=2.2f,CameraYaw=30,CameraPitch=25,Volume=0.5f,MusicVolume=0.30f;
        public PlayerState LocalPlayer=>State?.Player(LocalId);
        public InputFrame LocalInput=>local;
        public bool InRun=>State!=null && State.phase!=RunPhase.Lobby;
        public bool UpgradeSelection=>LocalPlayer!=null&&LocalPlayer.pendingLevels>0&&!LocalPlayer.downed&&!LocalPlayer.eliminated;
        public bool WorldPaused=>!Online&&(Paused||(Simulation?.UpgradePaused??false));
        public bool CapturingMouse=>InRun && State.phase==RunPhase.Playing && !Paused && !Inventory && !(UpgradeSelection&&!Online) && !Input.GetKey(KeyCode.LeftAlt);
        CoopTransport transport;
        readonly Dictionary<int,InputFrame> inputs=new Dictionary<int,InputFrame>();
        InputFrame local=new InputFrame();
        float accumulator,networkTimer,lastSnapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Instance==null)new GameObject("ERA / Game systems").AddComponent<GameApp>();}
        void Awake()
        {
            Instance=this;Time.timeScale=1;I18n.Load();
            var args=Environment.GetCommandLineArgs();
            Automated=Array.Exists(args,a=>a.StartsWith("--smoke-") || a.StartsWith("--capture-"));
            Application.targetFrameRate=90;Application.runInBackground=true;
            QualitySettings.vSyncCount=1;QualitySettings.antiAliasing=4;QualitySettings.shadowDistance=110;QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowCascades=4;QualitySettings.anisotropicFiltering=AnisotropicFiltering.Enable;
            PlayerName=PlayerPrefs.GetString("era.name","Wanderer");if(PlayerName=="Странник")PlayerName="Wanderer";
            Address=PlayerPrefs.GetString("era.address","127.0.0.1");
            SelectedHero=(Hero)Mathf.Clamp(PlayerPrefs.GetInt("era.hero",0),0,1);
            SelectedRelic=(Relic)Mathf.Clamp(PlayerPrefs.GetInt("era.relic",0),0,2);
            MouseSensitivity=PlayerPrefs.GetFloat("era.sensitivity",2.2f);Volume=PlayerPrefs.GetFloat("era.volume",0.5f);MusicVolume=PlayerPrefs.GetFloat("era.music",0.30f);
            HostAddresses=CoopTransport.LocalAddresses();
            View=gameObject.AddComponent<ArenaView>();gameObject.AddComponent<GameHud>();gameObject.AddComponent<SynthAudio>();
        }
        void Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(Automated){if(Array.IndexOf(args,"--smoke-art")>=0)gameObject.AddComponent<ArtDiagnostics>();else if(Array.IndexOf(args,"--smoke-solo")>=0)gameObject.AddComponent<SoloDiagnostics>();else gameObject.AddComponent<RuntimeDiagnostics>();return;}
            if(Array.IndexOf(args,"--solo")>=0)Solo();
            else if(Array.IndexOf(args,"--host")>=0)Host();
            else if(Array.IndexOf(args,"--join")>=0)Join();
        }
        public void BeginSelection(SessionMode mode){Mode=mode;Page=MenuPage.Characters;Status="";}
        public void ConfirmCharacter()
        {
            SavePreferences();
            if(State!=null && State.phase==RunPhase.Lobby)
            {
                if(IsHost)Simulation.SetLoadout(LocalId,SelectedHero,SelectedRelic,PlayerName);
                else SendLoadout("loadout");
                Page=MenuPage.Home;return;
            }
            if(Mode==SessionMode.Solo)Solo();else if(Mode==SessionMode.Host)Host();else Join();
        }
        public void Solo()
        {
            ResetSession();IsHost=true;LocalId=0;
            Simulation=new Simulation(Environment.TickCount&int.MaxValue);Simulation.AddPlayer(0,PlayerName,SelectedHero,SelectedRelic);
            State=Simulation.State;Simulation.Start();SavePreferences();
        }
        public void Host()
        {
            ResetSession();
            try
            {
                transport=new CoopTransport();transport.Host(GameData.Port);IsHost=true;Online=true;LocalId=0;
                Simulation=new Simulation(Environment.TickCount&int.MaxValue,true);Simulation.AddPlayer(0,PlayerName,SelectedHero,SelectedRelic);State=Simulation.State;
                Status="Share your local IP with your companion.";SavePreferences();
            }
            catch(Exception){ResetSession();Status="Port 7777 is in use. Close the other lobby and try again.";Page=MenuPage.HostLobby;}
        }
        public void Join()
        {
            ResetSession();
            if(string.IsNullOrWhiteSpace(Address)){Status="Enter the host computer's IP address.";Page=MenuPage.JoinLobby;return;}
            transport=new CoopTransport();Online=true;Connecting=true;LocalId=1;
            Status="Finding your companion...";lastSnapshot=Time.realtimeSinceStartup;transport.Join(Address.Trim(),GameData.Port);SavePreferences();
        }
        public void ToggleReady()
        {
            if(LocalPlayer==null || State.phase!=RunPhase.Lobby)return;
            bool ready=!LocalPlayer.ready;
            if(IsHost)Simulation.SetReady(LocalId,ready);else transport?.Send(JsonUtility.ToJson(new WireMessage{kind="ready",ready=ready}));
            SynthAudio.Instance?.Play(12);
        }
        public void Choose(int slot){if(LocalPlayer?.pendingLevels>0)local.choice=slot;}
        public void ReturnToMenu(){ResetSession();Status="";Page=MenuPage.Home;}
        void ResetSession()
        {
            transport?.Dispose();transport=null;State=null;Simulation=null;Online=Connecting=IsHost=Paused=Inventory=false;
            inputs.Clear();local=new InputFrame();accumulator=networkTimer=0;View?.ClearActors();Time.timeScale=1;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void Update()
        {
            PumpNetwork();bool running=State?.phase==RunPhase.Playing;Time.timeScale=WorldPaused?0:1;
            if(running && Input.GetKeyDown(KeyCode.Escape)){Paused=!Paused;Inventory=false;}
            if(running && Input.GetKeyDown(KeyCode.Tab)){Inventory=!Inventory;Paused=false;}
            if(CapturingMouse && !Automated)
            {CameraYaw+=Input.GetAxisRaw("Mouse X")*MouseSensitivity;CameraPitch=Mathf.Clamp(CameraPitch-Input.GetAxisRaw("Mouse Y")*MouseSensitivity,8,65);}
            bool capture=CapturingMouse&&!Automated;
            Cursor.lockState=capture?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!capture;
            if(!Automated)ReadInput(running);
            if(IsHost && Simulation!=null && !(Paused&&!Online))
            {
                accumulator+=Mathf.Min(Time.unscaledDeltaTime,0.15f);inputs[0]=local;
                while(accumulator>=1f/30)
                {Simulation.Step(1f/30,inputs);foreach(var frame in inputs.Values)frame.ConsumeEdges();accumulator-=1f/30;}
            }
            networkTimer+=Time.unscaledDeltaTime;
            if(Online && transport!=null && networkTimer>=1f/20)
            {
                networkTimer=0;
                if(IsHost && transport.Connected && State!=null)Send("world",JsonUtility.ToJson(State),true);
                else if(!IsHost && transport.Connected){Send("input",JsonUtility.ToJson(local));local.ConsumeEdges();}
            }
            if(Online && !IsHost && Time.realtimeSinceStartup-lastSnapshot>12)FailConnection("The host is not responding. Connection closed.");
        }
        void ReadInput(bool running)
        {
            if(running&&UpgradeSelection&&!Paused&&!Inventory)
            {if(Input.GetKeyDown(KeyCode.Alpha1))Choose(0);if(Input.GetKeyDown(KeyCode.Alpha2))Choose(1);if(Input.GetKeyDown(KeyCode.Alpha3))Choose(2);}
            if(!running || Paused || Inventory || (UpgradeSelection&&!Online))
            {int choice=local.choice;local.x=local.z=0;local.crouch=local.jumpHeld=false;local.revive=running&&UpgradeSelection&&!Paused&&!Inventory&&Input.GetKey(KeyCode.F);local.ConsumeEdges();if(running&&!Paused&&!Inventory)local.choice=choice;return;}
            var raw=new Vector3(Input.GetKey(KeyCode.D)?1:Input.GetKey(KeyCode.A)?-1:0,0,Input.GetKey(KeyCode.W)?1:Input.GetKey(KeyCode.S)?-1:0);
            var move=Quaternion.Euler(0,CameraYaw,0)*raw.normalized;local.x=move.x;local.z=move.z;local.yaw=CameraYaw;
            local.jump|=Input.GetKeyDown(KeyCode.Space);local.jumpHeld=Input.GetKey(KeyCode.Space);local.crouch=Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.C);
            local.dash|=Input.GetKeyDown(KeyCode.LeftShift);local.primary|=Input.GetKeyDown(KeyCode.Q);local.secondary|=Input.GetKeyDown(KeyCode.E);local.relic|=Input.GetKeyDown(KeyCode.R);
            local.revive=Input.GetKey(KeyCode.F);local.interact|=Input.GetKeyDown(KeyCode.F);
            if(Input.GetKeyDown(KeyCode.Alpha1))Choose(0);if(Input.GetKeyDown(KeyCode.Alpha2))Choose(1);if(Input.GetKeyDown(KeyCode.Alpha3))Choose(2);
        }
        void Send(string kind,string payload="",bool snapshot=false)=>transport?.Send(JsonUtility.ToJson(new WireMessage{kind=kind,payload=payload}),snapshot);
        void SendLoadout(string kind)=>transport?.Send(JsonUtility.ToJson(new WireMessage{kind=kind,name=PlayerName,hero=SelectedHero,relic=SelectedRelic}));
        void PumpNetwork()
        {
            if(transport==null)return;int budget=64;
            while(transport!=null && budget-->0 && transport.TryDequeue(out var evt))
            {
                if(evt.kind=="connected"){if(!IsHost)SendLoadout("hello");continue;}
                if(evt.kind=="disconnected" || evt.kind=="error")
                {
                    if(IsHost && State!=null){State.players.RemoveAll(p=>p.id==1);inputs.Remove(1);Simulation.ResetReadiness();Status=InRun?"Your companion disconnected. You can finish this run alone.":"Waiting for another companion.";}
                    else FailConnection(evt.text);continue;
                }
                try
                {
                    var message=JsonUtility.FromJson<WireMessage>(evt.text);
                    if(message==null || message.version!=GameData.Protocol)
                    {if(IsHost){Send("reject","Game versions do not match.");}else FailConnection("Game versions do not match.");continue;}
                    switch(message.kind)
                    {
                        case "hello" when IsHost:
                            if(State.phase!=RunPhase.Lobby){Send("reject","The run has already begun. Join the next lobby.");break;}
                            if(State.Player(1)==null)Simulation.AddPlayer(1,message.name,message.hero==Hero.Echo?Hero.Echo:Hero.Ember,(Relic)Mathf.Clamp((int)message.relic,0,2));
                            Status="Both companions must press Ready.";break;
                        case "ready" when IsHost:Simulation.SetReady(1,message.ready);break;
                        case "loadout" when IsHost:Simulation.SetLoadout(1,message.hero,message.relic,message.name);break;
                        case "input" when IsHost:
                            if(State.Player(1)==null)break;var next=JsonUtility.FromJson<InputFrame>(message.payload);if(next==null)break;
                            if(inputs.TryGetValue(1,out var old))
                            {next.jump|=old.jump;next.dash|=old.dash;next.primary|=old.primary;next.secondary|=old.secondary;next.relic|=old.relic;next.interact|=old.interact;if(next.choice<0)next.choice=old.choice;}
                            inputs[1]=next;break;
                        case "world" when !IsHost:
                            State=JsonUtility.FromJson<WorldState>(message.payload);
                            if(State?.Player(1)!=null){Connecting=false;Status=State.phase==RunPhase.Lobby?"Both companions must press Ready.":"";}
                            lastSnapshot=Time.realtimeSinceStartup;break;
                        case "reject" when !IsHost:FailConnection(message.payload);break;
                    }
                }
                catch(Exception e)when(e is ArgumentException || e is NullReferenceException){FailConnection("Invalid game data. Use the same game version on both computers.");}
            }
            if(State?.phase==RunPhase.Playing && Status=="Both companions must press Ready.")Status="";
        }
        void FailConnection(string reason){ResetSession();Status=reason;Page=MenuPage.JoinLobby;}
        public void SavePreferences()
        {
            if(Automated)return;
            PlayerPrefs.SetInt("era.language",(int)I18n.Language);PlayerPrefs.SetString("era.name",PlayerName);PlayerPrefs.SetString("era.address",Address);PlayerPrefs.SetInt("era.hero",(int)SelectedHero);PlayerPrefs.SetInt("era.relic",(int)SelectedRelic);
            PlayerPrefs.SetFloat("era.sensitivity",MouseSensitivity);PlayerPrefs.SetFloat("era.volume",Volume);PlayerPrefs.SetFloat("era.music",MusicVolume);PlayerPrefs.Save();
        }
        void OnApplicationQuit(){SavePreferences();transport?.Dispose();}
        public void SetLanguage(GameLanguage language){I18n.Language=language;SavePreferences();}
        void OnDestroy(){Time.timeScale=1;transport?.Dispose();if(Instance==this)Instance=null;}
    }
}
