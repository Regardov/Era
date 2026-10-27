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
        readonly Dictionary<int,int> peerPlayers=new Dictionary<int,int>();
        InputFrame local=new InputFrame();
        float accumulator,networkTimer,lastSnapshot;
        const float SimulationStep=1f/30;
        PlayerState visualSamplePlayer;
        Vector3 visualPrevious,visualCurrent;
        int visualSampleStage;

        // Follow the host simulation one fixed tick behind. The remainder is a
        // render-time fraction, not a second simulation or a velocity prediction.
        // Teleports, new sessions and externally driven diagnostic poses snap.
        public Vector3 LocalCameraPosition(PlayerState player)
        {
            if(!IsHost||!ReferenceEquals(player,visualSamplePlayer)||State.stageSerial!=visualSampleStage||
                player.position!=visualCurrent||(visualCurrent-visualPrevious).sqrMagnitude>9)
                return player.position;
            return Vector3.Lerp(visualPrevious,visualCurrent,Mathf.Clamp01(accumulator/SimulationStep));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Instance==null)new GameObject("ERA / Game systems").AddComponent<GameApp>();}
        void Awake()
        {
            Instance=this;Time.timeScale=1;I18n.Load();
            var args=Environment.GetCommandLineArgs();
            Automated=Array.Exists(args,a=>a.StartsWith("--smoke-") || a.StartsWith("--capture-"));
            Application.targetFrameRate=Automated?20:60;Application.runInBackground=true;
            QualitySettings.vSyncCount=1;QualitySettings.antiAliasing=4;QualitySettings.shadowDistance=110;QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowCascades=4;QualitySettings.anisotropicFiltering=AnisotropicFiltering.Enable;
            if(Automated){QualitySettings.vSyncCount=0;QualitySettings.antiAliasing=0;QualitySettings.shadowDistance=35;QualitySettings.shadowResolution=ShadowResolution.Low;QualitySettings.shadowCascades=0;}
            PlayerName=PlayerPrefs.GetString("era.name","Wanderer");if(PlayerName=="Странник")PlayerName="Wanderer";
            Address=PlayerPrefs.GetString("era.address","127.0.0.1");
            SelectedHero=(Hero)Mathf.Clamp(PlayerPrefs.GetInt("era.hero",0),0,2);
            SelectedRelic=(Relic)Mathf.Clamp(PlayerPrefs.GetInt("era.relic",0),0,2);
            if(!Automated)SetFullscreen(PlayerPrefs.GetInt("era.fullscreen",0)==1);
            MouseSensitivity=PlayerPrefs.GetFloat("era.sensitivity",2.2f);Volume=PlayerPrefs.GetFloat("era.volume",0.5f);MusicVolume=PlayerPrefs.GetFloat("era.music",0.30f);
            HostAddresses=CoopTransport.LocalAddresses();
            View=gameObject.AddComponent<ArenaView>();gameObject.AddComponent<GameHud>();gameObject.AddComponent<SynthAudio>();
        }
        void Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(Automated&&Array.IndexOf(args,"--smoke-camera-motion")>=0){gameObject.AddComponent<CameraMotionDiagnostics>();return;}
            if(Automated){if(Array.IndexOf(args,"--smoke-ensemble")>=0)gameObject.AddComponent<EnsembleAnimationDiagnostics>();else if(Array.IndexOf(args,"--smoke-archer-refinement")>=0)gameObject.AddComponent<ArcherRefinementDiagnostics>();else if(Array.IndexOf(args,"--smoke-archer-motion")>=0)gameObject.AddComponent<ArcherMotionDiagnostics>();else if(Array.IndexOf(args,"--smoke-archer")>=0)gameObject.AddComponent<ArcherRuntimeDiagnostics>();else if(Array.IndexOf(args,"--smoke-feedback")>=0)gameObject.AddComponent<FeedbackDiagnostics>();else if(Array.IndexOf(args,"--smoke-music")>=0)gameObject.AddComponent<MusicDiagnostics>();else if(Array.IndexOf(args,"--smoke-environment")>=0)gameObject.AddComponent<EnvironmentDiagnostics>();else if(Array.IndexOf(args,"--smoke-rig")>=0)gameObject.AddComponent<RigDiagnostics>();else if(Array.IndexOf(args,"--smoke-animation")>=0)gameObject.AddComponent<AnimationDiagnostics>();else if(Array.IndexOf(args,"--smoke-characters")>=0)gameObject.AddComponent<CharacterDiagnostics>();else if(Array.IndexOf(args,"--smoke-meadow")>=0)gameObject.AddComponent<MeadowDiagnostics>();else if(Array.IndexOf(args,"--smoke-balance")>=0)gameObject.AddComponent<BalanceDiagnostics>();else if(Array.IndexOf(args,"--smoke-roguelike")>=0)gameObject.AddComponent<RoguelikeDiagnostics>();else if(Array.IndexOf(args,"--smoke-update")>=0)gameObject.AddComponent<UpdateDiagnostics>();else if(Array.IndexOf(args,"--smoke-chests")>=0)gameObject.AddComponent<ChestDiagnostics>();else if(Array.IndexOf(args,"--smoke-campaign")>=0)gameObject.AddComponent<CampaignDiagnostics>();else if(Array.IndexOf(args,"--smoke-art")>=0)gameObject.AddComponent<ArtDiagnostics>();else if(Array.IndexOf(args,"--smoke-solo")>=0)gameObject.AddComponent<SoloDiagnostics>();else gameObject.AddComponent<RuntimeDiagnostics>();return;}
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
            transport=new CoopTransport();Online=true;Connecting=true;LocalId=-1;
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
            visualSamplePlayer=null;
            transport?.Dispose();transport=null;State=null;Simulation=null;Online=Connecting=IsHost=Paused=Inventory=false;
            inputs.Clear();peerPlayers.Clear();local=new InputFrame();accumulator=networkTimer=0;View?.ClearActors();Time.timeScale=1;
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
                while(accumulator>=SimulationStep)
                {
                    var player=LocalPlayer;var before=player!=null?player.position:Vector3.zero;int stage=State.stageSerial;
                    Simulation.Step(SimulationStep,inputs);
                    visualSamplePlayer=ReferenceEquals(player,LocalPlayer)&&stage==State.stageSerial?player:null;
                    visualPrevious=before;visualCurrent=LocalPlayer!=null?LocalPlayer.position:Vector3.zero;visualSampleStage=State.stageSerial;
                    foreach(var frame in inputs.Values)frame.ConsumeEdges();accumulator-=SimulationStep;
                }
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
            {for(int i=0;i<LocalPlayer.offers.Length&&i<8;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))Choose(i);}
            if(!running || Paused || Inventory || (UpgradeSelection&&!Online))
            {int choice=local.choice;local.x=local.z=0;local.crouch=local.jumpHeld=false;local.revive=running&&UpgradeSelection&&!Paused&&!Inventory&&Input.GetKey(KeyCode.F);local.ConsumeEdges();if(running&&!Paused&&!Inventory)local.choice=choice;return;}
            var raw=new Vector3(Input.GetKey(KeyCode.D)?1:Input.GetKey(KeyCode.A)?-1:0,0,Input.GetKey(KeyCode.W)?1:Input.GetKey(KeyCode.S)?-1:0);
            var move=Quaternion.Euler(0,CameraYaw,0)*raw.normalized;local.x=move.x;local.z=move.z;local.yaw=CameraYaw;
            local.jump|=Input.GetKeyDown(KeyCode.Space);local.jumpHeld=Input.GetKey(KeyCode.Space);local.crouch=Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.C);
            local.dash|=Input.GetKeyDown(KeyCode.LeftShift);local.primary|=Input.GetKeyDown(KeyCode.Q);local.secondary|=Input.GetKeyDown(KeyCode.E);local.relic|=Input.GetKeyDown(KeyCode.R);
            local.revive=Input.GetKey(KeyCode.F);local.interact|=Input.GetKeyDown(KeyCode.F);
            if(Input.GetMouseButtonDown(0)&&LocalPlayer!=null&&Physics.Raycast(View.Camera.ScreenPointToRay(Input.mousePosition),out var hit,20,~((1<<ActorBodyCollider.PlayerLayer)|(1<<ActorBodyCollider.EnemyLayer))))
            {var target=hit.collider.GetComponentInParent<ClickInteractable>();if(target!=null&&Vector3.Distance(LocalPlayer.position,target.transform.position)<target.radius)local.interact=true;}
            if(Input.GetKeyDown(KeyCode.Alpha1))Choose(0);if(Input.GetKeyDown(KeyCode.Alpha2))Choose(1);if(Input.GetKeyDown(KeyCode.Alpha3))Choose(2);
        }
        void Send(string kind,string payload="",bool snapshot=false)=>transport?.Send(JsonUtility.ToJson(new WireMessage{kind=kind,payload=payload}),snapshot);
        void SendTo(int peer,string kind,string payload="",int playerId=-1)=>transport?.SendTo(peer,JsonUtility.ToJson(new WireMessage{kind=kind,payload=payload,playerId=playerId}));
        void SendLoadout(string kind)=>transport?.Send(JsonUtility.ToJson(new WireMessage{kind=kind,name=PlayerName,hero=SelectedHero,relic=SelectedRelic}));
        void PumpNetwork()
        {
            if(transport==null)return;int budget=64;
            while(transport!=null && budget-->0 && transport.TryDequeue(out var evt))
            {
                if(evt.kind=="connected"){if(!IsHost)SendLoadout("hello");continue;}
                if(evt.kind=="disconnected" || evt.kind=="error")
                {
                    if(IsHost && State!=null)
                    {
                        if(peerPlayers.TryGetValue(evt.peer,out int departed)){State.players.RemoveAll(p=>p.id==departed);inputs.Remove(departed);peerPlayers.Remove(evt.peer);Simulation.ResetReadiness();Simulation.RefreshAuras();}
                        Status=I18n.P("A companion disconnected. The remaining party can continue.","Напарник отключился. Остальные могут продолжить.");
                    }
                    else FailConnection(evt.text);continue;
                }
                try
                {
                    var message=JsonUtility.FromJson<WireMessage>(evt.text);
                    if(message==null || message.version!=GameData.Protocol)
                    {if(IsHost){SendTo(evt.peer,"reject","Game versions do not match.");}else FailConnection("Game versions do not match.");continue;}
                    int sender=peerPlayers.TryGetValue(evt.peer,out int assigned)?assigned:-1;
                    switch(message.kind)
                    {
                        case "hello" when IsHost:
                            if(State.phase!=RunPhase.Lobby){SendTo(evt.peer,"reject","The run has already begun. Join the next lobby.");break;}
                            if(sender<0)
                            {
                                for(int id=1;id<GameData.MaxPlayers;id++)if(State.Player(id)==null)
                                {Simulation.AddPlayer(id,message.name,(Hero)Mathf.Clamp((int)message.hero,0,2),(Relic)Mathf.Clamp((int)message.relic,0,2));sender=id;peerPlayers[evt.peer]=id;break;}
                            }
                            if(sender<0){SendTo(evt.peer,"reject",I18n.P("The lobby is full (3 players).","Лобби заполнено (3 игрока)."));break;}
                            SendTo(evt.peer,"welcome","",sender);Status=I18n.P("Every connected player must press Ready.","Все подключённые игроки должны нажать «Готов».");break;
                        case "welcome" when !IsHost:LocalId=message.playerId;break;
                        case "ready" when IsHost:if(sender>=0)Simulation.SetReady(sender,message.ready);break;
                        case "loadout" when IsHost:if(sender>=0)Simulation.SetLoadout(sender,message.hero,message.relic,message.name);break;
                        case "input" when IsHost:
                            if(sender<0||State.Player(sender)==null)break;var next=JsonUtility.FromJson<InputFrame>(message.payload);if(next==null)break;
                            if(inputs.TryGetValue(sender,out var old))
                            {next.jump|=old.jump;next.dash|=old.dash;next.primary|=old.primary;next.secondary|=old.secondary;next.relic|=old.relic;next.interact|=old.interact;if(next.choice<0)next.choice=old.choice;}
                            inputs[sender]=next;break;
                        case "world" when !IsHost:
                            State=JsonUtility.FromJson<WorldState>(message.payload);
                            if(State!=null)WorldMap.SetStage(State.stage);
                            if(State?.Player(LocalId)!=null){Connecting=false;Status=State.phase==RunPhase.Lobby?I18n.P("Every connected player must press Ready.","Все подключённые игроки должны нажать «Готов»."):"";}
                            lastSnapshot=Time.realtimeSinceStartup;break;
                        case "reject" when !IsHost:FailConnection(message.payload);break;
                    }
                }
                catch(Exception e)when(e is ArgumentException || e is NullReferenceException)
                {if(IsHost){transport?.DropPeer(evt.peer);if(peerPlayers.TryGetValue(evt.peer,out int id)){State.players.RemoveAll(p=>p.id==id);inputs.Remove(id);peerPlayers.Remove(evt.peer);Simulation.ResetReadiness();Simulation.RefreshAuras();}}else FailConnection("Invalid game data. Use the same game version on both computers.");}
            }
            if(State?.phase==RunPhase.Playing && State.players.Count>=2)Status="";
        }
        void FailConnection(string reason){ResetSession();Status=reason;Page=MenuPage.JoinLobby;}
        public void SavePreferences()
        {
            if(Automated)return;
            PlayerPrefs.SetInt("era.fullscreen",Screen.fullScreen?1:0);
            PlayerPrefs.SetInt("era.language",(int)I18n.Language);PlayerPrefs.SetString("era.name",PlayerName);PlayerPrefs.SetString("era.address",Address);PlayerPrefs.SetInt("era.hero",(int)SelectedHero);PlayerPrefs.SetInt("era.relic",(int)SelectedRelic);
            PlayerPrefs.SetFloat("era.sensitivity",MouseSensitivity);PlayerPrefs.SetFloat("era.volume",Volume);PlayerPrefs.SetFloat("era.music",MusicVolume);PlayerPrefs.Save();
        }
        void OnApplicationQuit(){SavePreferences();transport?.Dispose();}
        public void SetLanguage(GameLanguage language){I18n.Language=language;SavePreferences();}
        public void SetFullscreen(bool enabled)
        {
            if(enabled)Screen.SetResolution(Display.main.systemWidth,Display.main.systemHeight,FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(1280,720,FullScreenMode.Windowed);
        }
        void OnDestroy(){Time.timeScale=1;transport?.Dispose();if(Instance==this)Instance=null;}
    }
}
