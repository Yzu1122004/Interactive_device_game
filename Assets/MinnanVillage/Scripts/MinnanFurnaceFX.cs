using UnityEngine;
namespace MinnanVillage {
 public sealed class MinnanFurnaceFX:MonoBehaviour {
  public Transform[] flames;
  public Light fireLight;
  Vector3[] original,axes;
  void Awake(){original=new Vector3[flames.Length];axes=new Vector3[flames.Length];for(int i=0;i<flames.Length;i++)if(flames[i]){original[i]=flames[i].localScale;Vector3 v=flames[i].InverseTransformDirection(Vector3.up);axes[i]=new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));}}
  void Update(){float t=Time.time;for(int i=0;i<flames.Length;i++){if(!flames[i])continue;float f=.10f*Mathf.Sin(t*6.283185f+i*1.7f)+.035f*Mathf.Sin(t*12.56637f+i);flames[i].localScale=Vector3.Scale(original[i],Vector3.one+axes[i]*f);}if(fireLight)fireLight.intensity=1.2f+.18f*Mathf.Sin(t*6.283185f)+.08f*Mathf.Sin(t*12.56637f);}
  void OnDisable(){if(original==null)return;for(int i=0;i<flames.Length;i++)if(flames[i])flames[i].localScale=original[i];}
 }
}
