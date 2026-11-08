using System.Collections.Generic;
using Mirage;
using Mirage.Serialization;
using NuclearOption.Networking;
using Submarines.Bootstrap;
using Submarines.Runtime;
using UnityEngine;

namespace Submarines.Sync
{
    // ------------------------------------------------------------------ messages

    internal struct SubHello
    {
        public byte Version;
    }

    internal struct SubStateMsg
    {
        public uint Unit;
        public float Depth;
        public float Ordered;
        public byte Mode;
        public byte Torps;
        public byte Missiles;
        public byte Decoys;
        public float MimicLeft;
        public float MimicCooldown;
        public bool Pending;
    }

    internal struct TorpStateMsg
    {
        public uint Id;
        public uint Owner;
        public Vector3 Pos;
        public Vector3 Heading;
        public float Speed;
        public bool Asw;
    }

    internal struct TorpEndMsg
    {
        public uint Id;
    }

    internal struct DecoySpawnMsg
    {
        public uint Id;
        public byte Kind;
        public uint Parent;
        public Vector3 Pos;
        public Vector3 Drift;
        public Vector3 EchoDir;
        public float Life;
        public float EchoRange;
    }

    internal struct DecoyEndMsg
    {
        public uint Id;
    }

    internal struct SubOrderMsg
    {
        public uint Unit;
        public byte Order;
    }

    internal struct SubNoticeMsg
    {
        public byte Code;
        public float Value;
    }

    /// <summary>
    /// Multiplayer sync over Mirage custom messages. The subs themselves are normal networked
    /// units (spawned by the game from the registered prefab), and their model is built locally.
    /// This layer carries everything the game doesn't know about: depth orders and ammo, torpedoes,
    /// decoys, and player orders from clients. All positions are in global (floating-origin-free)
    /// coordinates.
    /// </summary>
    internal static class SubNet
    {
        internal const byte Version = 1;

        private static NetworkManagerNuclearOption _hooked;
        private static bool _serializers;
        private static bool _serverHandlers;
        private static bool _clientHandlers;
        private static readonly HashSet<INetworkPlayer> Clients = new HashSet<INetworkPlayer>();
        private static float _nextState;
        private static float _nextTorp;

        internal static void Tick()
        {
            NetworkManagerNuclearOption nm = NetworkManagerNuclearOption.i;
            if (nm == null)
                return;
            if (nm != _hooked)
                Hook(nm);
            if (Net.IsServer && Clients.Count > 0)
                ServerTick();
        }

        private static void Hook(NetworkManagerNuclearOption nm)
        {
            _hooked = nm;
            _serverHandlers = false;
            _clientHandlers = false;
            RegisterSerializers();
            nm.Server.Started.AddListener(RegisterServerHandlers);
            nm.Server.Stopped.AddListener(OnServerStopped);
            nm.Server.Disconnected.AddListener(OnServerDisconnected);
            nm.Client.Started.AddListener(OnClientStarted);
            nm.Client.Authenticated.AddListener(_ => SendHello());
            nm.Client.Disconnected.AddListener(_ => OnClientDisconnected());
            if (nm.Server.Active)
                RegisterServerHandlers();
            if (nm.Client.Active)
                OnClientStarted();
        }

        private static bool IsClientOnly
        {
            get { return _hooked != null && _hooked.Client != null && _hooked.Client.Active && !Net.IsServer; }
        }

        // ------------------------------------------------------------ server

        private static void RegisterServerHandlers()
        {
            if (_serverHandlers || _hooked == null)
                return;
            _serverHandlers = true;
            MessageHandler h = _hooked.Server.MessageHandler;
            h.RegisterHandler<SubHello>(OnHello, false);
            h.RegisterHandler<SubOrderMsg>(OnOrder, false);
        }

        private static void OnServerStopped()
        {
            Clients.Clear();
            _serverHandlers = false;
        }

        private static void OnServerDisconnected(INetworkPlayer conn)
        {
            Clients.Remove(conn);
        }

        private static void OnHello(INetworkPlayer conn, SubHello msg)
        {
            if (msg.Version != Version)
            {
                SubmarinesPlugin.Log.LogWarning("Player " + conn + " has Submarines network version " + msg.Version +
                                                ", host has " + Version + ". Submarine sync disabled for them.");
                return;
            }
            Clients.Add(conn);
            SubmarinesPlugin.Log.LogInfo("Player " + conn + " has the Submarines mod; syncing.");
            // Late joiner: give them the decoys already in the water. Torpedo and boat state follow
            // on the next periodic update.
            List<Decoy> decoys = UnderwaterWorld.Decoys;
            for (int i = 0; i < decoys.Count; i++)
            {
                if (decoys[i].Alive)
                    SendDecoySpawn(decoys[i], conn);
            }
        }

        private static void OnOrder(INetworkPlayer conn, SubOrderMsg msg)
        {
            if (!Clients.Contains(conn))
                return;
            Unit u = FindUnit(msg.Unit);
            Submarine s = u != null ? u.GetComponent<Submarine>() : null;
            if (s == null)
                return;
            Player player;
            SubNotice notice;
            float value = 0f;
            if (!conn.TryGetPlayer(out player) || player == null || player.HQ != u.NetworkHQ)
                notice = SubNotice.NotYourBoat;
            else
                notice = SubCommands.Execute(s, (SubOrder)msg.Order, out value);
            conn.Send(new SubNoticeMsg { Code = (byte)notice, Value = value }, Channel.Reliable);
        }

        private static void ServerTick()
        {
            float now = Time.time;
            if (now >= _nextState)
            {
                _nextState = now + 0.5f;
                List<Submarine> subs = UnderwaterWorld.Subs;
                for (int i = 0; i < subs.Count; i++)
                {
                    Submarine s = subs[i];
                    if (s == null || s.Ship == null || s.Ship.disabled)
                        continue;
                    Broadcast(new SubStateMsg
                    {
                        Unit = s.Ship.persistentID.Id,
                        Depth = s.Depth,
                        Ordered = s.OrderedDepth,
                        Mode = (byte)s.Mode,
                        Torps = (byte)Mathf.Clamp(s.Torpedoes, 0, 255),
                        Missiles = (byte)Mathf.Clamp(s.Missiles, 0, 255),
                        Decoys = (byte)Mathf.Clamp(s.DecoysLeft, 0, 255),
                        MimicLeft = s.MimicLeft,
                        MimicCooldown = s.MimicCooldownLeft,
                        Pending = s.PendingLaunch,
                    });
                }
            }

            if (now >= _nextTorp)
            {
                _nextTorp = now + 0.25f;
                List<Torpedo> torps = UnderwaterWorld.Torpedoes;
                for (int i = 0; i < torps.Count; i++)
                {
                    Torpedo t = torps[i];
                    if (t.Dead || t.Remote)
                        continue;
                    Broadcast(new TorpStateMsg
                    {
                        Id = (uint)t.Id,
                        Owner = t.Owner != null ? t.Owner.persistentID.Id : 0u,
                        Pos = t.Pos,
                        Heading = t.Heading,
                        Speed = t.Speed,
                        Asw = t.Asw,
                    });
                }
            }
        }

        private static void Broadcast<T>(T msg)
        {
            foreach (INetworkPlayer c in Clients)
            {
                if (c != null && !c.IsHost)
                    c.Send(msg, Channel.Reliable);
            }
        }

        internal static void SendTorpedoEnd(Torpedo t)
        {
            if (Clients.Count > 0 && t != null && !t.Remote)
                Broadcast(new TorpEndMsg { Id = (uint)t.Id });
        }

        internal static void SendDecoySpawn(Decoy d, INetworkPlayer only)
        {
            if (d == null || !Net.IsServer || (Clients.Count == 0 && only == null))
                return;
            DecoySpawnMsg msg = new DecoySpawnMsg
            {
                Id = (uint)d.Id,
                Kind = (byte)d.Kind,
                Parent = d.Parent != null ? d.Parent.persistentID.Id : 0u,
                Pos = d.Pos,
                Drift = d.Drift,
                EchoDir = d.EchoDir,
                Life = d.LifeLeft,
                EchoRange = d.EchoRange,
            };
            if (only != null)
                only.Send(msg, Channel.Reliable);
            else
                Broadcast(msg);
        }

        internal static void SendDecoyEnd(Decoy d)
        {
            if (Clients.Count > 0 && d != null)
                Broadcast(new DecoyEndMsg { Id = (uint)d.Id });
        }

        // ------------------------------------------------------------ client

        private static void OnClientStarted()
        {
            // The client must know the submarine prefab hashes before the host spawns one.
            SubRegistry.RegisterWithMirage();
            if (_clientHandlers || _hooked == null)
                return;
            _clientHandlers = true;
            MessageHandler h = _hooked.Client.MessageHandler;
            h.RegisterHandler<SubStateMsg>(OnSubState, false);
            h.RegisterHandler<TorpStateMsg>(OnTorpState, false);
            h.RegisterHandler<TorpEndMsg>(OnTorpEnd, false);
            h.RegisterHandler<DecoySpawnMsg>(OnDecoySpawn, false);
            h.RegisterHandler<DecoyEndMsg>(OnDecoyEnd, false);
            h.RegisterHandler<SubNoticeMsg>(OnNotice, false);
        }

        private static void OnClientDisconnected()
        {
            _clientHandlers = false;
            if (!Net.IsServer)
            {
                UnderwaterWorld.Torpedoes.Clear();
                UnderwaterWorld.Decoys.Clear();
            }
        }

        private static void SendHello()
        {
            if (!IsClientOnly)
                return;
            INetworkPlayer server = _hooked.Client.Player;
            if (server != null)
                server.Send(new SubHello { Version = Version }, Channel.Reliable);
        }

        internal static void SendOrder(Submarine s, SubOrder order)
        {
            if (!IsClientOnly || s == null || s.Ship == null)
                return;
            INetworkPlayer server = _hooked.Client.Player;
            if (server != null)
                server.Send(new SubOrderMsg { Unit = s.Ship.persistentID.Id, Order = (byte)order }, Channel.Reliable);
        }

        private static void OnSubState(INetworkPlayer conn, SubStateMsg m)
        {
            if (Net.IsServer)
                return;
            Unit u = FindUnit(m.Unit);
            Ship ship = u as Ship;
            if (ship == null)
                return;
            Submarine s = ship.GetComponent<Submarine>();
            if (s == null)
            {
                // InitializeUnit didn't attach on this client for some reason: do it now.
                SubSpec spec = SubRegistry.SpecOf(ship);
                if (spec == null)
                    return;
                s = Submarine.Attach(ship, spec);
            }
            s.ApplyState(m.Depth, m.Ordered, (SubMode)m.Mode, m.Torps, m.Missiles, m.Decoys, m.MimicLeft, m.MimicCooldown, m.Pending);
        }

        private static void OnTorpState(INetworkPlayer conn, TorpStateMsg m)
        {
            if (Net.IsServer)
                return;
            Torpedo t = UnderwaterWorld.FindTorpedo((int)m.Id);
            if (t == null)
            {
                UnderwaterWorld.Torpedoes.Add(new Torpedo((int)m.Id, FindUnit(m.Owner), m.Pos, m.Heading, m.Speed, m.Asw));
                return;
            }
            t.ApplyRemote(m.Pos, m.Heading);
        }

        private static void OnTorpEnd(INetworkPlayer conn, TorpEndMsg m)
        {
            Torpedo t = UnderwaterWorld.FindTorpedo((int)m.Id);
            if (t != null && t.Remote)
                t.Dead = true;
        }

        private static void OnDecoySpawn(INetworkPlayer conn, DecoySpawnMsg m)
        {
            if (Net.IsServer || UnderwaterWorld.FindDecoy((int)m.Id) != null)
                return;
            UnderwaterWorld.Decoys.Add(new Decoy((int)m.Id, (DecoyKind)m.Kind, FindUnit(m.Parent), m.Pos, m.Drift,
                                                 m.EchoDir, m.Life, m.EchoRange));
        }

        private static void OnDecoyEnd(INetworkPlayer conn, DecoyEndMsg m)
        {
            if (Net.IsServer)
                return;
            Decoy d = UnderwaterWorld.FindDecoy((int)m.Id);
            if (d != null)
                d.Dead = true;
        }

        private static void OnNotice(INetworkPlayer conn, SubNoticeMsg m)
        {
            UI.SonarDisplay.Say(SubCommands.Text((SubNotice)m.Code, m.Value));
        }

        internal static Unit FindUnit(uint id)
        {
            if (id == 0)
                return null;
            Unit u;
            return UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = id }), out u) ? u : null;
        }

        // ------------------------------------------------------------ serialization

        private static void RegisterSerializers()
        {
            if (_serializers)
                return;
            _serializers = true;

            Writer<SubHello>.Write = (w, m) => w.WriteByte(m.Version);
            Reader<SubHello>.Read = r => new SubHello { Version = r.ReadByte() };

            Writer<SubStateMsg>.Write = (w, m) =>
            {
                w.WriteUInt32(m.Unit);
                w.WriteSingle(m.Depth);
                w.WriteSingle(m.Ordered);
                w.WriteByte(m.Mode);
                w.WriteByte(m.Torps);
                w.WriteByte(m.Missiles);
                w.WriteByte(m.Decoys);
                w.WriteSingle(m.MimicLeft);
                w.WriteSingle(m.MimicCooldown);
                w.WriteBoolean(m.Pending);
            };
            Reader<SubStateMsg>.Read = r => new SubStateMsg
            {
                Unit = r.ReadUInt32(),
                Depth = r.ReadSingle(),
                Ordered = r.ReadSingle(),
                Mode = r.ReadByte(),
                Torps = r.ReadByte(),
                Missiles = r.ReadByte(),
                Decoys = r.ReadByte(),
                MimicLeft = r.ReadSingle(),
                MimicCooldown = r.ReadSingle(),
                Pending = r.ReadBoolean(),
            };

            Writer<TorpStateMsg>.Write = (w, m) =>
            {
                w.WriteUInt32(m.Id);
                w.WriteUInt32(m.Owner);
                WriteVec(w, m.Pos);
                WriteVec(w, m.Heading);
                w.WriteSingle(m.Speed);
                w.WriteBoolean(m.Asw);
            };
            Reader<TorpStateMsg>.Read = r => new TorpStateMsg
            {
                Id = r.ReadUInt32(),
                Owner = r.ReadUInt32(),
                Pos = ReadVec(r),
                Heading = ReadVec(r),
                Speed = r.ReadSingle(),
                Asw = r.ReadBoolean(),
            };

            Writer<TorpEndMsg>.Write = (w, m) => w.WriteUInt32(m.Id);
            Reader<TorpEndMsg>.Read = r => new TorpEndMsg { Id = r.ReadUInt32() };

            Writer<DecoySpawnMsg>.Write = (w, m) =>
            {
                w.WriteUInt32(m.Id);
                w.WriteByte(m.Kind);
                w.WriteUInt32(m.Parent);
                WriteVec(w, m.Pos);
                WriteVec(w, m.Drift);
                WriteVec(w, m.EchoDir);
                w.WriteSingle(m.Life);
                w.WriteSingle(m.EchoRange);
            };
            Reader<DecoySpawnMsg>.Read = r => new DecoySpawnMsg
            {
                Id = r.ReadUInt32(),
                Kind = r.ReadByte(),
                Parent = r.ReadUInt32(),
                Pos = ReadVec(r),
                Drift = ReadVec(r),
                EchoDir = ReadVec(r),
                Life = r.ReadSingle(),
                EchoRange = r.ReadSingle(),
            };

            Writer<DecoyEndMsg>.Write = (w, m) => w.WriteUInt32(m.Id);
            Reader<DecoyEndMsg>.Read = r => new DecoyEndMsg { Id = r.ReadUInt32() };

            Writer<SubOrderMsg>.Write = (w, m) =>
            {
                w.WriteUInt32(m.Unit);
                w.WriteByte(m.Order);
            };
            Reader<SubOrderMsg>.Read = r => new SubOrderMsg { Unit = r.ReadUInt32(), Order = r.ReadByte() };

            Writer<SubNoticeMsg>.Write = (w, m) =>
            {
                w.WriteByte(m.Code);
                w.WriteSingle(m.Value);
            };
            Reader<SubNoticeMsg>.Read = r => new SubNoticeMsg { Code = r.ReadByte(), Value = r.ReadSingle() };
        }

        private static void WriteVec(NetworkWriter w, Vector3 v)
        {
            w.WriteSingle(v.x);
            w.WriteSingle(v.y);
            w.WriteSingle(v.z);
        }

        private static Vector3 ReadVec(NetworkReader r)
        {
            float x = r.ReadSingle();
            float y = r.ReadSingle();
            float z = r.ReadSingle();
            return new Vector3(x, y, z);
        }
    }
}
