using UnityEngine;
using UnityEngine.Rendering;
namespace MinnanVillage {
 public sealed class MinnanDaylight:MonoBehaviour {
  public bool applySceneEnvironment=true;
  public Material skybox;
  public Light sun;
  void OnEnable(){if(!applySceneEnvironment)return;RenderSettings.skybox=skybox;RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.62f,.72f,.82f);RenderSettings.ambientEquatorColor=new Color(.48f,.49f,.46f);RenderSettings.ambientGroundColor=new Color(.3f,.27f,.23f);RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.6f;RenderSettings.fog=false;}
 }
}
