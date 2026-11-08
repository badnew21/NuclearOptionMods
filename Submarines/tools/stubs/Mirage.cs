// STUB for offline type-checking only (tools/check.sh). Not the real API; never ship or reference.
namespace Mirage { public class NetworkIdentity:UnityEngine.MonoBehaviour{ public int PrefabHash; public bool SpawnedFromInstantiate{get;private set;} public void ClearSceneId(){}} public enum Channel{Reliable,Unreliable}
 public interface INetworkPlayer{ void Send<T>(T msg, Channel c); bool IsHost{get;}}
 public delegate void MessageDelegateWithPlayer<T>(INetworkPlayer p, T m);
 public class MessageHandler{ public void RegisterHandler<T>(MessageDelegateWithPlayer<T> h,bool b){}}
 public class Ev{ public void AddListener(System.Action a){}} public class Ev<T>{ public void AddListener(System.Action<T> a){}}
 public enum ClientStoppedReason{None}
 public class NetworkServer{ public bool Active; public Ev Started, Stopped; public Ev<INetworkPlayer> Disconnected; public MessageHandler MessageHandler;}
 public class NetworkClient{ public bool Active; public Ev Started; public Ev<INetworkPlayer> Authenticated; public Ev<ClientStoppedReason> Disconnected; public MessageHandler MessageHandler; public INetworkPlayer Player;}}
namespace Mirage.Serialization{ public class NetworkWriter{ public void WriteByte(byte b){} public void WriteUInt32(uint u){} public void WriteSingle(float f){} public void WriteBoolean(bool b){}}
 public class NetworkReader{ public byte ReadByte(){return 0;} public uint ReadUInt32(){return 0;} public float ReadSingle(){return 0;} public bool ReadBoolean(){return false;}}
 public static class Writer<T>{ public static System.Action<NetworkWriter,T> Write;} public static class Reader<T>{ public static System.Func<NetworkReader,T> Read;}}
