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
public class Component:Object{ public Transform transform; public GameObject gameObject; public T GetComponent<T>(){return default(T);} public T GetComponentInParent<T>(){return default(T);} public T[] GetComponentsInChildren<T>(bool b){return null;}}
public class Behaviour:Component{public bool enabled;}
public class MonoBehaviour:Behaviour{ public Coroutine StartCoroutine(IEnumerator e){return null;}}
public class Coroutine{}
public class GameObject:Object{ public GameObject(string n){} public Transform transform; public void SetActive(bool b){} public T GetComponent<T>(){return default(T);} public T AddComponent<T>() where T:Component{return default(T);} public T[] GetComponentsInChildren<T>(bool b){return null;}}
public class Transform:Component{ public Vector3 position, localPosition, forward, up; public Quaternion localRotation; public bool IsChildOf(Transform t){return false;} public int GetSiblingIndex(){return 0;}}
public struct Vector3{ public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;}
 public static Vector3 zero, up, down, right, forward; public float magnitude{get{return 0;}} public float sqrMagnitude{get{return 0;}} public Vector3 normalized{get{return this;}}
 public static Vector3 operator+(Vector3 a,Vector3 b){return a;} public static Vector3 operator-(Vector3 a,Vector3 b){return a;} public static Vector3 operator-(Vector3 a){return a;}
 public static Vector3 operator*(Vector3 a,float b){return a;} public static Vector3 operator*(float b,Vector3 a){return a;} public static Vector3 operator/(Vector3 a,float b){return a;}
 public static float Distance(Vector3 a,Vector3 b){return 0;} public static float Angle(Vector3 a,Vector3 b){return 0;} public static Vector3 Cross(Vector3 a,Vector3 b){return a;}
 public static Vector3 Project(Vector3 a,Vector3 b){return a;} public static Vector3 RotateTowards(Vector3 a,Vector3 b,float c,float d){return a;}}
public struct Vector2{ public float x,y; public Vector2(float a,float b){x=a;y=b;} public static Vector2 right; public float magnitude{get{return 0;}} public float sqrMagnitude{get{return 0;}} public Vector2 normalized{get{return this;}}
 public static Vector2 operator+(Vector2 a,Vector2 b){return a;} public static Vector2 operator*(Vector2 a,float b){return a;} public static Vector2 operator/(Vector2 a,float b){return a;}}
public struct Quaternion{ public static Quaternion identity; public static Quaternion Euler(float a,float b,float c){return identity;} public static Quaternion LookRotation(Vector3 f){return identity;} public static Quaternion LookRotation(Vector3 f,Vector3 u){return identity;} public static Vector3 operator*(Quaternion q,Vector3 v){return v;}}
public static class Mathf{ public const float Deg2Rad=0.017f, Rad2Deg=57f; public static float Max(float a,float b){return a;} public static float Min(float a,float b){return a;} public static float Clamp(float a,float b,float c){return a;} public static float Clamp01(float a){return a;} public static float Abs(float a){return a;} public static float Sqrt(float a){return a;} public static float Pow(float a,float b){return a;} public static float Log10(float a){return a;} public static float Log(float a,float b){return a;} public static float Exp(float a){return a;} public static float Atan2(float a,float b){return a;} public static float Sin(float a){return a;} public static float Cos(float a){return a;} public static float DeltaAngle(float a,float b){return a;} public static float Lerp(float a,float b,float t){return a;}}
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
public enum KeyCode{F8,F9,PageDown,PageUp,Insert,Delete,End,Home,Pause,LeftControl,LeftShift}
public struct Rect{ public float x,y,width,height; public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}}
public struct Color{ public float r,g,b,a; public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;a=1;} public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white, magenta;}
public class Texture{} public class Texture2D:Texture{ public Texture2D(int w,int h){} public void SetPixel(int x,int y,Color c){} public void Apply(){}}
public static class Screen{ public static int width,height;}
public static class GUI{ public static Color color; public static void DrawTexture(Rect r,Texture t){} public static void Label(Rect r,string s){}}
}
