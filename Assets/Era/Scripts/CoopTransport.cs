using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Era
{
    public sealed class TransportEvent
    {
        public string kind, text;
        public int peer;
        public TransportEvent(string kind, string text = "", int peer = 0) { this.kind = kind; this.text = text; this.peer=peer; }
    }

    // Three-player, host-authoritative LAN transport. Every event retains its connection ID.
    // All socket I/O runs off the Unity thread; snapshots use a single replaceable send slot.
    public sealed class CoopTransport : IDisposable
    {
        const int MaxFrame = 512 * 1024;
        readonly ConcurrentQueue<TransportEvent> events = new ConcurrentQueue<TransportEvent>();
        readonly object gate = new object();
        volatile bool stopped;
        TcpListener listener;
        readonly Dictionary<int,Connection> peers=new Dictionary<int,Connection>();
        int nextPeer=1;
        public int PeerCount { get { lock(gate){int count=0;foreach(var peer in peers.Values)if(!peer.Closed)count++;return count;} } }
        public bool Connected => PeerCount>0;
        public bool TryDequeue(out TransportEvent evt) => events.TryDequeue(out evt);
        void Emit(string kind, string text = "", int peer = 0)
        {
            if (events.Count > 160) { Dispose(); return; }
            if (!stopped) events.Enqueue(new TransportEvent(kind, text, peer));
        }
        public void Host(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            try { listener.Start(GameData.MaxPlayers); }
            catch (SocketException) { listener.Stop(); listener = null; throw; }
            new Thread(() =>
            {
                while (!stopped)
                {
                    try
                    {
                        var client = listener.AcceptTcpClient();
                        lock (gate)
                        {
                            if (stopped || PeerCount>=GameData.MaxPlayers-1) { client.Close(); continue; }
                            Attach(client,nextPeer++);
                        }
                    }
                    catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
                    { if (!stopped) Emit("error", "Could not accept the connection."); break; }
                }
            }) { IsBackground = true, Name = "ERA accept" }.Start();
        }
        public void Join(string address, int port)
        {
            new Thread(() =>
            {
                var client = new TcpClient();
                try
                {
                    var connect = client.ConnectAsync(address, port);
                    if (!connect.Wait(5000)) throw new TimeoutException();
                    lock (gate)
                    {
                        if (stopped) { client.Close(); return; }
                        Attach(client,0);
                    }
                }
                catch (Exception)
                { client.Close(); Emit("error", "Host unavailable. Check the IP, TCP port 7777 and your firewall."); }
            }) { IsBackground = true, Name = "ERA connect" }.Start();
        }
        void Attach(TcpClient client,int id)
        {
            client.NoDelay = true;
            client.ReceiveTimeout = 12000;
            client.SendTimeout = 4000;
            var stale=new List<int>();foreach(var pair in peers)if(pair.Value.Closed)stale.Add(pair.Key);foreach(int key in stale){peers[key].Dispose();peers.Remove(key);}
            var peer = new Connection(client,(kind,text)=>Emit(kind,text,id));peers[id]=peer;
            Emit("connected","",id);
            peer.Start();
        }
        public void Send(string json, bool snapshot = false)
        {
            lock (gate) { foreach(var peer in peers.Values)if(!peer.Closed)peer.Send(json, snapshot); }
        }
        public void SendTo(int id,string json,bool snapshot=false)
        {
            lock(gate)if(peers.TryGetValue(id,out var peer)&&!peer.Closed)peer.Send(json,snapshot);
        }
        public void DropPeer(int id=-1)
        {
            lock (gate)
            {
                if(id>=0){if(peers.TryGetValue(id,out var peer)){peer.Dispose();peers.Remove(id);}return;}
                foreach(var peer in peers.Values)peer.Dispose();peers.Clear();
            }
        }
        public void Dispose()
        {
            stopped = true;
            listener?.Stop();
            DropPeer();
        }
        public static string LocalAddresses()
        {
            try
            {
                var addresses = Dns.GetHostAddresses(Dns.GetHostName());
                var list = new System.Collections.Generic.List<string>();
                foreach (var a in addresses) if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) list.Add(a.ToString());
                return list.Count == 0 ? "127.0.0.1" : string.Join(" / ", list);
            }
            catch { return "127.0.0.1"; }
        }
        sealed class Connection : IDisposable
        {
            readonly TcpClient client;
            readonly NetworkStream stream;
            readonly Action<string, string> emit;
            readonly ConcurrentQueue<byte[]> outgoing = new ConcurrentQueue<byte[]>();
            readonly AutoResetEvent wake = new AutoResetEvent(false);
            byte[] latestSnapshot;
            int closed;
            public bool Closed => Volatile.Read(ref closed) != 0;
            public Connection(TcpClient client, Action<string, string> emit)
            { this.client = client; this.emit = emit; stream = client.GetStream(); }
            public void Start()
            {
                new Thread(Read) { IsBackground = true, Name = "ERA receive" }.Start();
                new Thread(Write) { IsBackground = true, Name = "ERA send" }.Start();
            }
            public void Send(string json, bool snapshot)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                if (bytes.Length > MaxFrame) { Fail(); return; }
                if (snapshot) Interlocked.Exchange(ref latestSnapshot, bytes);
                else
                {
                    if (outgoing.Count > 32) { Fail(); return; }
                    outgoing.Enqueue(bytes);
                }
                wake.Set();
            }
            void Read()
            {
                try
                {
                    var header = new byte[4];
                    while (!Closed)
                    {
                        ReadExact(header);
                        int size = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));
                        if (size < 1 || size > MaxFrame) throw new IOException("Invalid frame");
                        var bytes = new byte[size]; ReadExact(bytes);
                        emit("message", Encoding.UTF8.GetString(bytes));
                    }
                }
                catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException) { Fail(); }
            }
            void ReadExact(byte[] bytes)
            {
                int offset = 0;
                while (offset < bytes.Length)
                { int count = stream.Read(bytes, offset, bytes.Length - offset); if (count == 0) throw new IOException("Closed"); offset += count; }
            }
            void Write()
            {
                try
                {
                    while (!Closed)
                    {
                        if (!outgoing.TryDequeue(out var bytes)) bytes = Interlocked.Exchange(ref latestSnapshot, null);
                        if (bytes == null) { wake.WaitOne(250); continue; }
                        var header = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));
                        stream.Write(header, 0, 4); stream.Write(bytes, 0, bytes.Length);
                    }
                }
                catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException) { Fail(); }
            }
            void Fail() { if (Interlocked.Exchange(ref closed, 1) != 0) return; client.Close(); wake.Set(); emit("disconnected", "Connection to your companion was lost."); }
            public void Dispose() { Interlocked.Exchange(ref closed, 1); client.Close(); wake.Set(); }
        }
    }
    [Serializable]
    public sealed class WireMessage
    {
        public int version = GameData.Protocol;
        public string kind, payload, name;
        public Hero hero;
        public Relic relic;
        public bool ready;
        public int playerId = -1;
    }
}
