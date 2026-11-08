// STUB for offline type-checking only (tools/check.sh). Not the real API; never ship or reference.
using System.Collections.Generic; using UnityEngine;
public interface INetworkDefinition{ int LookupIndex{get;set;}}
public class UnitDefinition:ScriptableObject,INetworkDefinition{ public string jsonKey,unitName,description,code; public float value,length,radarSize; public GameObject unitPrefab; public void CacheMass(){} public int LookupIndex{get;set;}}
public class ShipDefinition:UnitDefinition{} public class MissileDefinition:UnitDefinition{} public class BuildingDefinition:UnitDefinition{}
public class Encyclopedia:ScriptableObject{ public static Encyclopedia i; public static Dictionary<string,UnitDefinition> Lookup; public List<ShipDefinition> ships; public List<MissileDefinition> missiles; public List<INetworkDefinition> IndexLookup; void AfterLoad(){}}
public struct PersistentID{ public static PersistentID None;}
public struct GlobalPosition{ public float x,y,z; public Vector3 AsVector3(){return default(Vector3);}}
public class FactionHQ:MonoBehaviour{ public void RpcUpdateTrackingInfo(PersistentID id){} public bool TryGetKnownPosition(Unit u,out GlobalPosition p){p=default(GlobalPosition);return false;}}
public class Unit:MonoBehaviour{ public UnitDefinition definition; public bool disabled; public FactionHQ NetworkHQ; public PersistentID persistentID; public string unitName; public GlobalPosition GlobalPosition(){return default(GlobalPosition);} public void InitializeUnit(){}}
public class Ship:Unit{ public Rigidbody rb;}
public class Missile:Unit{ public class Warhead{ public bool Armed;} public void Detonate(Vector3 n,bool a,bool b){}}
public static class UnitRegistry{ public static List<Unit> allUnits;}
public interface IDamageable{ Unit GetUnit(); void TakeDamage(float a,float b,float c,float d,float e,PersistentID id);}
public static class DamageEffects{ public static void BlastFrag(float y,Vector3 p,PersistentID a,PersistentID b){}}
public class Spawner:MonoBehaviour{ public Missile SpawnMissile(MissileDefinition d,Vector3 p,Quaternion r,Vector3 v,Unit t,Unit o){return null;}}
public class NetworkSceneSingleton<T>{ public static T i;}
public static class Datum{ public static float LocalSeaY; public static Transform origin;}
public static class PhysicsLayers{ public static LayerMask StaticsMask;}
public class TargetDetector{} public class Turret:MonoBehaviour{ void Turret_OnDetectTarget(Unit unit){}}
public enum GameState{Encyclopedia,SinglePlayer}
public static class GameManager{ public static GameState gameState; public static bool GetLocalPlayer(out NuclearOption.Networking.Player p){p=null;return false;}}
namespace NuclearOption.Networking{ public class NetworkManagerNuclearOption{ public static NetworkManagerNuclearOption i; public Mirage.NetworkServer Server; void RegisterPrefabs(){}} public class Player{ public FactionHQ HQ;}}
namespace NuclearOption.Jobs{ public class DetectorManager{ void RequestRadarCheck(TargetDetector detector,Unit target){} void RequestLoSCheck(TargetDetector detector,Unit target){}}}
