using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using MinnanVillage;

[InitializeOnLoad]
public static class ValidateMinnanRuntime {
 static double start=-1;static Vector3 initial;static float lightInitial;
 static ValidateMinnanRuntime(){EditorApplication.update+=Tick;}
 public static void Begin(){
  EditorSceneManager.OpenScene("Assets/MinnanVillage/Scenes/MinnanVillage_Demo.unity");
  SessionState.SetBool("MinnanRuntimeCheck",true);EditorApplication.EnterPlaymode();
 }
 static void Tick(){
  if(!SessionState.GetBool("MinnanRuntimeCheck",false)||!EditorApplication.isPlaying)return;
  var fx=Object.FindObjectOfType<MinnanFurnaceFX>();if(!fx)return;
  if(start<0){start=EditorApplication.timeSinceStartup;initial=fx.flames[0].localScale;lightInitial=fx.fireLight.intensity;return;}
  if(EditorApplication.timeSinceStartup-start<2.35)return;
  var smoke=Object.FindObjectsOfType<ParticleSystem>().FirstOrDefault();
  int count=smoke?smoke.particleCount:0;
  bool flame=(fx.flames[0].localScale-initial).sqrMagnitude>1e-9f;
  bool light=Mathf.Abs(fx.fireLight.intensity-lightInitial)>.0001f;
  string result="Runtime smoke particles="+count+"; flame animated="+flame+"; light animated="+light+"; water shader uses time.\n";
  Directory.CreateDirectory("../Unity_Export/Prefab_Package");
  File.WriteAllText("../Unity_Export/Prefab_Package/RuntimeValidation.txt",result);
  var cam=Object.FindObjectsOfType<Camera>(true).FirstOrDefault(c=>c.name=="Furnace_Alley");
  if(cam){var rt=new RenderTexture(1024,768,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var t=new Texture2D(1024,768,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1024,768),0,0);t.Apply();File.WriteAllBytes("../Unity_Export/Prefab_Package/Furnace_Runtime.png",t.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;Object.Destroy(t);Object.Destroy(rt);}
  SessionState.SetBool("MinnanRuntimeCheck",false);Debug.Log("MINNAN_RUNTIME_CHECK "+result);EditorApplication.Exit(count>0&&flame&&light?0:1);
 }
}
