// STUB for offline type-checking only (tools/check.sh). Not the real API; never ship or reference.
using System;
using System.Collections;
namespace UnityEngine {
public class Object { public string name; public HideFlags hideFlags; public int GetInstanceID(){return 0;}
 public static T Instantiate<T>(T o) where T:Object{return o;} public static T Instantiate<T>(T o, Transform p, bool w) where T:Object{return o;}
 public static void DontDestroyOnLoad(Object o){} public static void Destroy(Object o){}
 public static implicit operator bool(Object o){return o!=null;} }
public enum HideFlags{None}
public class ScriptableObject:Object{}
public class Component:Object{ public Transform transform; public GameObject gameObject; public T GetComponent<T>(){return default(T);} public T GetComponentInParent<T>(){return default(T);} public T GetComponentInChildren<T>(bool b){return default(T);} public T[] GetComponentsInChildren<T>(bool b){return null;}}
public class Behaviour:Component{public bool enabled;}
public class MonoBehaviour:Behaviour{ public Coroutine StartCoroutine(IEnumerator e){return null;}}
public class Coroutine{}
public class GameObject:Object{ public GameObject(string n){} public Transform transform; public int layer; public void SetActive(bool b){} public T GetComponent<T>(){return default(T);} public T AddComponent<T>() where T:Component{return default(T);} public T GetComponentInChildren<T>(bool b){return default(T);} public T[] GetComponentsInChildren<T>(bool b){return null;}}
public class Transform:Component{ public Vector3 position, localPosition, forward, up; public Quaternion localRotation, rotation; public Transform parent; public Transform Find(string n){return null;} public void SetParent(Transform p,bool w){} public bool IsChildOf(Transform t){return false;} public int GetSiblingIndex(){return 0;}}
public struct Vector3{ public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;}
 public static Vector3 zero{get{return new Vector3(0,0,0);}} public static Vector3 up{get{return new Vector3(0,1,0);}} public static Vector3 down{get{return new Vector3(0,-1,0);}}
 public static Vector3 right{get{return new Vector3(1,0,0);}} public static Vector3 forward{get{return new Vector3(0,0,1);}} public static Vector3 one{get{return new Vector3(1,1,1);}}
 public float sqrMagnitude{get{return x*x+y*y+z*z;}} public float magnitude{get{return (float)Math.Sqrt(sqrMagnitude);}}
 public Vector3 normalized{get{float m=magnitude; return m>1e-5f? this/m : zero;}}
 public static Vector3 operator+(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);} public static Vector3 operator-(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);} public static Vector3 operator-(Vector3 a){return new Vector3(-a.x,-a.y,-a.z);}
 public static Vector3 operator*(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);} public static Vector3 operator*(float b,Vector3 a){return a*b;} public static Vector3 operator/(Vector3 a,float b){return new Vector3(a.x/b,a.y/b,a.z/b);}
 public static float Dot(Vector3 a,Vector3 b){return a.x*b.x+a.y*b.y+a.z*b.z;} public static Vector3 Cross(Vector3 a,Vector3 b){return new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
 public static float Distance(Vector3 a,Vector3 b){return (a-b).magnitude;} public static float Angle(Vector3 a,Vector3 b){float d=Dot(a.normalized,b.normalized); return (float)(Math.Acos(Math.Max(-1,Math.Min(1,d)))*57.29578);}
 public static Vector3 Project(Vector3 a,Vector3 b){float m=Dot(b,b); return m<1e-8f? zero : b*(Dot(a,b)/m);}
 public static Vector3 Lerp(Vector3 a,Vector3 b,float t){return a+(b-a)*t;}
 public static Vector3 RotateTowards(Vector3 a,Vector3 b,float c,float d){return b;}
 public static Vector3 Scale(Vector3 a,Vector3 b){return new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);}}
public struct Vector2{ public float x,y; public Vector2(float a,float b){x=a;y=b;} public static Vector2 right{get{return new Vector2(1,0);}} public static Vector2 zero{get{return new Vector2(0,0);}}
 public float sqrMagnitude{get{return x*x+y*y;}} public float magnitude{get{return (float)Math.Sqrt(sqrMagnitude);}} public Vector2 normalized{get{float m=magnitude; return m>1e-5f? this/m : zero;}}
 public static Vector2 operator+(Vector2 a,Vector2 b){return new Vector2(a.x+b.x,a.y+b.y);} public static Vector2 operator-(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);} public static Vector2 operator*(Vector2 a,float b){return new Vector2(a.x*b,a.y*b);} public static Vector2 operator/(Vector2 a,float b){return new Vector2(a.x/b,a.y/b);}}
public struct Quaternion{ public float x,y,z,w; public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}
 public static Quaternion identity{get{return new Quaternion(0,0,0,1);}}
 public static Quaternion Euler(float a,float b,float c){return identity;} public static Quaternion LookRotation(Vector3 f){return identity;} public static Quaternion LookRotation(Vector3 f,Vector3 u){return identity;}
 public static Vector3 operator*(Quaternion q,Vector3 v){Vector3 u=new Vector3(q.x,q.y,q.z); Vector3 t=Vector3.Cross(u,v)*2f; return v+t*q.w+Vector3.Cross(u,t);}}
public static class Mathf{ public const float PI=3.14159265f, Deg2Rad=PI/180f, Rad2Deg=180f/PI;
 public static float Max(float a,float b){return a>b?a:b;} public static int Max(int a,int b){return a>b?a:b;} public static float Min(float a,float b){return a<b?a:b;} public static int Min(int a,int b){return a<b?a:b;}
 public static float Clamp(float a,float b,float c){return a<b?b:(a>c?c:a);} public static float Clamp01(float a){return Clamp(a,0,1);} public static float Abs(float a){return a<0?-a:a;}
 public static float Sqrt(float a){return (float)Math.Sqrt(a);} public static float Pow(float a,float b){return (float)Math.Pow(a,b);} public static float Log10(float a){return (float)Math.Log10(a);} public static float Log(float a,float b){return (float)Math.Log(a,b);}
 public static float Exp(float a){return (float)Math.Exp(a);} public static float Atan2(float a,float b){return (float)Math.Atan2(a,b);} public static float Sin(float a){return (float)Math.Sin(a);} public static float Cos(float a){return (float)Math.Cos(a);}
 public static float DeltaAngle(float a,float b){float d=(b-a)%360f; if(d>180)d-=360; if(d<-180)d+=360; return d;} public static float Lerp(float a,float b,float t){return a+(b-a)*Clamp01(t);}
 public static float Repeat(float t,float l){return t-(float)Math.Floor(t/l)*l;} public static float MoveTowards(float a,float b,float d){return Abs(b-a)<=d?b:a+Math.Sign(b-a)*d;}}
public class Rigidbody:Component{ public Vector3 velocity, angularVelocity; public bool isKinematic; public void AddForce(Vector3 f,ForceMode m){} public void AddTorque(Vector3 f,ForceMode m){}}
public enum ForceMode{Force,Acceleration}
public enum QueryTriggerInteraction{Ignore}
public struct RaycastHit{ public Vector3 point; public Collider collider; public float distance;}
public class Collider:Component{ public Vector3 ClosestPoint(Vector3 p){return p;}}
public static class Physics{ public static bool Raycast(Vector3 o,Vector3 d,out RaycastHit h,float m,int mask,QueryTriggerInteraction q){h=default(RaycastHit);return false;} public static int OverlapSphereNonAlloc(Vector3 p,float r,Collider[] b,int m,QueryTriggerInteraction q){return 0;}}
public struct LayerMask{ public int value;}
public static class Resources{ public static T[] FindObjectsOfTypeAll<T>() where T:Object{return null;} public static Object[] FindObjectsOfTypeAll(Type t){return null;}}
public static class Time{ public static float time, deltaTime, fixedDeltaTime, unscaledTime;}
public static class Random{ public static float Range(float a,float b){return a;} public static Vector2 insideUnitCircle;}
public static class Input{ public static bool GetKeyDown(KeyCode k){return false;} public static bool GetKey(KeyCode k){return false;}}
public enum KeyCode{F8,F9,PageDown,PageUp,Insert,Delete,End,Home,Pause,LeftControl,LeftShift,Backslash}
public struct Rect{ public float x,y,width,height; public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}}
public struct Color{ public float r,g,b,a; public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;a=1;} public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white, magenta, black;}
public class Texture{} public class Texture2D:Texture{ public Texture2D(int w,int h){} public void SetPixel(int x,int y,Color c){} public void Apply(){}}
public class Mesh:Object{ public int subMeshCount; public void SetVertices(System.Collections.Generic.List<Vector3> v){} public void SetTriangles(System.Collections.Generic.List<int> t,int s){} public void RecalculateNormals(){} public void RecalculateBounds(){}}
public class Shader:Object{ public static Shader Find(string n){return null;}}
public class Material:Object{ public Material(Material m){} public Material(Shader s){} public Shader shader; public bool HasProperty(string n){return false;} public void SetTexture(string n,Texture t){} public void SetColor(string n,Color c){} public void SetFloat(string n,float f){} public void DisableKeyword(string k){}}
public class Renderer:Component{ public bool enabled; public Material sharedMaterial; public Material[] sharedMaterials;}
public class MeshRenderer:Renderer{} public class MeshFilter:Component{ public Mesh sharedMesh;}
public class LODGroup:Component{ public bool enabled;} public class Light:Behaviour{}
public class ParticleSystem:Component{ public struct EmissionModule{ public bool enabled{get{return false;}set{}}} public EmissionModule emission;}
public static class Screen{ public static int width,height;}
public static class GUI{ public static Color color; public static void DrawTexture(Rect r,Texture t){} public static void Label(Rect r,string s){}}
}
