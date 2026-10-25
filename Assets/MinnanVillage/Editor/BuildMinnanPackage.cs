using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MinnanVillage;

public static class BuildMinnanPackage {
 const string A="Assets/MinnanVillage";
 [Serializable] public class MatDef {public string name;public float[] a,b,mortar,brick,emission;public float mode,metallic,smoothness,strength;}
 [Serializable] public class MatList {public MatDef[] items;}
 [Serializable] public class Anchor {public string name;public float[] pos;}
 [Serializable] public class Source {public Anchor[] anchors;}
 static GameObject root,geo;static int sx=1,sz=1;
 static Vector3 V(float[] a){return new Vector3(a[0],a[1],a[2]);}
 static Color C(float[] a){return new Color(a[0],a[1],a[2],a.Length>3?a[3]:1);}
 static Vector3 P(float x,float y,float z){return new Vector3(sx*x,z,sz*y);}
 static Transform Find(string n){return geo.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==n);}
 static GameObject Child(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
 [MenuItem("Minnan Village/Build standalone package")]
 public static void Build(){try{Run();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static void Run(){
 foreach(var dir in new[]{"Materials","Prefabs","Scenes","Textures","Documentation"})Directory.CreateDirectory(A+"/"+dir);
 AssetDatabase.Refresh();
 var importer=(ModelImporter)AssetImporter.GetAtPath(A+"/Models/Minnan_Village.fbx");
 importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.isReadable=true;importer.importNormals=ModelImporterNormals.Import;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
 var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 root=new GameObject("MinnanVillage_Complete");
 geo=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(A+"/Models/Minnan_Village.fbx"));
 PrefabUtility.UnpackPrefabInstance(geo,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);geo.name="Geometry";geo.transform.SetParent(root.transform,false);
 var src=JsonUtility.FromJson<Source>(File.ReadAllText(A+"/Data/blender_source.json"));
 float best=float.MaxValue;
 foreach(int xx in new[]{-1,1})foreach(int zz in new[]{-1,1}){float e=0;foreach(var q in src.anchors){var t=Find(q.name);if(t)e+=Vector3.Distance(t.position,new Vector3(xx*q.pos[0],q.pos[2],zz*q.pos[1]));}if(e<best){best=e;sx=xx;sz=zz;}}
 if(best>.01f)throw new Exception("FBX axis/scale anchor mismatch: "+best);
 Debug.Log("AXIS: "+sx+",Z,"+sz+"Y error "+best);
 var shader=Shader.Find("MinnanVillage/Stylized Surface");if(!shader)throw new Exception("Surface shader missing");
 var defs=JsonUtility.FromJson<MatList>(File.ReadAllText(A+"/Data/materials_unity.json"));var mats=new Dictionary<string,Material>();
 foreach(var d in defs.items){var m=new Material(shader);m.name=d.name;m.SetColor("_ColorA",C(d.a));m.SetColor("_ColorB",C(d.b));m.SetColor("_Mortar",C(d.mortar));m.SetVector("_Brick",new Vector4(d.brick[0],d.brick[1],d.brick[2],0));m.SetFloat("_Mode",d.mode);m.SetFloat("_Metallic",d.metallic);m.SetFloat("_Smoothness",d.mode==4?.65f:d.smoothness);m.SetColor("_Glow",C(d.emission)*d.strength);AssetDatabase.CreateAsset(m,A+"/Materials/"+d.name+".mat");mats[d.name]=m;}
 int unmatched=0;
 foreach(var r in geo.GetComponentsInChildren<Renderer>(true)){if(r.name.StartsWith("Smoke_Rising_Card_")){r.gameObject.SetActive(false);continue;}var mm=r.sharedMaterials;for(int i=0;i<mm.Length;i++){string n=mm[i]?mm[i].name:"";if(mats.TryGetValue(n,out var m))mm[i]=m;else{Debug.LogWarning("Unmatched material "+n+" on "+r.name);unmatched++;}}r.sharedMaterials=mm;r.receiveShadows=true;}
 if(unmatched>0)throw new Exception("Material mappings missing: "+unmatched);
 // Environment colliders deliberately omit roofs, leaves and fine trim.
 var cg=Child("Collision",root.transform);
 int colCount=0;
 foreach(var f in geo.GetComponentsInChildren<MeshFilter>(true)){
 string n=f.name;
 bool ground=n.StartsWith("PATH_")&&(n.Contains("Walkable")||n.Contains("Road_Continuation")||n.Contains("Edge_Fill"));
 bool solid=(n.StartsWith("BLD_")&&!n.Contains("Gable"))||(n.StartsWith("OPEN_")&&(n.Contains("Threshold")||n.Contains("Step")))||(n.StartsWith("TRUCK_")&&(n.EndsWith("Cabin")||n.EndsWith("Cargo_Box")||n.EndsWith("Chassis")))||n=="Burner_Open_Metal_Body";
 if(ground){var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=true;colCount++;}
 else if(solid){var c=f.gameObject.AddComponent<BoxCollider>();c.center=f.sharedMesh.bounds.center;c.size=f.sharedMesh.bounds.size;colCount++;}
 }
 foreach(float y in new[]{-4.44f,-11.66f}){var g=Child("Canal_Continuous_Barrier",cg.transform);g.transform.position=P(-13,y,.6f);var b=g.AddComponent<BoxCollider>();b.size=new Vector3(30.2f,1.2f,.22f);colCount++;}
 {var g=Child("Canal_End_Barrier",cg.transform);g.transform.position=P(2.1f,-8.05f,.6f);g.AddComponent<BoxCollider>().size=new Vector3(.22f,1.2f,7.2f);colCount++;}
 // Plant pots/jar/stool collision in world bounds, no per-leaf colliders.
 foreach(var r in geo.GetComponentsInChildren<Renderer>()){if(r.name.StartsWith("PROP_")&&(r.name.EndsWith("Terracotta")||r.name.Contains("Water_Jar")||r.name.Contains("Stool_Seat"))){var g=Child("COL_"+r.name,cg.transform);g.transform.position=r.bounds.center;var c=g.AddComponent<BoxCollider>();c.size=r.bounds.size;colCount++;}}
 var fx=Child("Furnace_Unity_Effects",root.transform);var burner=Find("PROP_Active_Joss_Burner");fx.transform.position=burner.position;
 var flames=geo.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Fire_Flame_")).ToArray();
 foreach(var t in geo.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Smoke_Rising_Card_")))t.gameObject.SetActive(false);
 var flicker=Child("Warm_Flicker_Light",fx.transform);flicker.transform.position=burner.position+Vector3.up*.9f;var fireLight=flicker.AddComponent<Light>();fireLight.type=LightType.Point;fireLight.range=2.5f;fireLight.color=new Color(1,.32f,.06f);fireLight.intensity=1.2f;fireLight.shadows=LightShadows.None;
 var anim=fx.AddComponent<MinnanFurnaceFX>();anim.flames=flames;anim.fireLight=fireLight;
 var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=(x-31.5f)/31.5f,dy=(y-31.5f)/31.5f;float alpha=Mathf.Pow(Mathf.Clamp01(1-dx*dx-dy*dy),2);tex.SetPixel(x,y,new Color(1,1,1,alpha));}tex.Apply();File.WriteAllBytes(A+"/Textures/SmokeSoft.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.Refresh();
 var smokeMat=new Material(Shader.Find("MinnanVillage/Soft Smoke"));smokeMat.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(A+"/Textures/SmokeSoft.png"));smokeMat.SetColor("_Color",new Color(.7f,.73f,.76f,.3f));AssetDatabase.CreateAsset(smokeMat,A+"/Materials/Smoke_Unity.mat");
 var smoke=Child("Gentle_Rising_Smoke",fx.transform);smoke.transform.localPosition=Vector3.up*1.02f;var ps=smoke.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.loop=true;main.duration=4;main.startLifetime=3.5f;main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.22f,.38f);main.maxParticles=28;main.simulationSpace=ParticleSystemSimulationSpace.World;main.prewarm=true;var emission=ps.emission;emission.rateOverTime=4;var shape=ps.shape;shape.enabled=false;var vel=ps.velocityOverLifetime;vel.enabled=true;vel.space=ParticleSystemSimulationSpace.World;vel.y=.30f;vel.x=.025f;
 var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.7f),new Keyframe(1,2.0f)));var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.2f),new GradientAlphaKey(0,1)});color.color=gradient;ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=smokeMat;
 // Optional complete daylight rig; environment settings can be disabled on component.
 var rig=Child("Daylight_Optional",root.transform);var sunObj=Child("Warm_Sun",rig.transform);var sun=sunObj.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.94f,.84f);sun.intensity=1.25f;sun.shadows=LightShadows.Soft;sun.shadowBias=.025f;sunObj.transform.rotation=Quaternion.LookRotation(P(-.65f,.32f,-1).normalized);
 var fillObj=Child("Soft_Sky_Fill",rig.transform);fillObj.transform.rotation=Quaternion.LookRotation(P(.65f,-.32f,-.7f));var fill=fillObj.AddComponent<Light>();fill.type=LightType.Directional;fill.color=new Color(.88f,.94f,1);fill.intensity=.65f;fill.shadows=LightShadows.None;
 var topObj=Child("Soft_Overhead_Fill",rig.transform);topObj.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);var topLight=topObj.AddComponent<Light>();topLight.type=LightType.Directional;topLight.color=new Color(.95f,.97f,1);topLight.intensity=.22f;topLight.shadows=LightShadows.None;
 var probeObj=Child("Village_Sky_Reflection",rig.transform);probeObj.transform.position=P(-7,1,2);var probe=probeObj.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;probe.resolution=128;probe.size=new Vector3(45,15,45);probe.clearFlags=ReflectionProbeClearFlags.Skybox;
 var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_SunSize",.04f);sky.SetFloat("_AtmosphereThickness",.85f);sky.SetColor("_SkyTint",new Color(.48f,.64f,.8f));sky.SetColor("_GroundColor",new Color(.5f,.46f,.4f));AssetDatabase.CreateAsset(sky,A+"/Materials/Daylight_Sky.mat");
 var day=rig.AddComponent<MinnanDaylight>();day.skybox=sky;day.sun=sun;RenderSettings.skybox=sky;RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.62f,.72f,.82f);RenderSettings.ambientEquatorColor=new Color(.48f,.49f,.46f);RenderSettings.ambientGroundColor=new Color(.3f,.27f,.23f);
 QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=70;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.antiAliasing=4;PlayerSettings.colorSpace=ColorSpace.Linear;
 var cams=Child("Review_Cameras_Disabled",root.transform);
 MakeCamera(cams.transform,"Start_Seated",P(0,14.1f,1.2f),P(0,3,1.2f),60);
 MakeCamera(cams.transform,"Furnace_Alley",P(.7f,.45f,1.2f),P(-.55f,5.6f,.9f),68);
 MakeCamera(cams.transform,"Truck_Canal",P(-16.5f,-1.75f,1.25f),P(-12,-5.8f,.1f),68);
 var overview=MakeCamera(cams.transform,"Overview",P(-19,-15,14),P(-4,3,0),65);
 var spawn=Child("Wheelchair_Start_Anchor",root.transform);spawn.transform.position=P(0,14.1f,0);spawn.transform.rotation=Quaternion.LookRotation(P(0,-1,0));
 Physics.SyncTransforms();
 var validation=ValidateRoute();
 var prefab=PrefabUtility.SaveAsPrefabAsset(root,A+"/Prefabs/MinnanVillage_Complete.prefab");if(!prefab)throw new Exception("Prefab save failed");
 var camObject=UnityEngine.Object.Instantiate(overview.gameObject);camObject.name="Demo_Overview_Camera";camObject.GetComponent<Camera>().enabled=true;camObject.AddComponent<AudioListener>();
 EditorSceneManager.SaveScene(scene,A+"/Scenes/MinnanVillage_Demo.unity");
 Directory.CreateDirectory("../Unity_Export/Prefab_Package");
 foreach(var cam in root.GetComponentsInChildren<Camera>(true)){Render(cam,"../Unity_Export/Prefab_Package/"+cam.name+".png");}
 string report="Unity 2022.3.9f1 Built-in. Renderers="+geo.GetComponentsInChildren<Renderer>(true).Length+"; colliders="+colCount+"; axis anchor error="+best+" m.\n"+validation+"\nProcedural materials recreated in Unity, not pixel-identical Blender bakes. Source smoke cards disabled, replaced by lightweight particles. Flame meshes and ember materials retained; gentle flicker/light recreated. Includes optional daylight rig and disabled review cameras. No player controller. Entrance steps remain non-step-free.\n";
 File.WriteAllText(A+"/Documentation/Validation.txt",report);File.WriteAllText("../Unity_Export/Prefab_Package/Validation.txt",report);
 File.WriteAllText(A+"/Documentation/README_VI.txt","Goi doc lap cho Unity 2022.3 Built-in (khong phai URP/HDRP).\nImport MinnanVillage_Complete.unitypackage. Keo Prefabs/MinnanVillage_Complete.prefab vao scene hoac mo Scenes/MinnanVillage_Demo.unity. Nhan Play de xem lua/khoi/nuoc dong.\nTat Daylight_Optional neu du an da co anh sang. MinnanDaylight ap dung sky/ambient khi bat; bo applySceneEnvironment neu muon giu moi truong hien co. Camera trong prefab mac dinh tat. Diem Wheelchair_Start_Anchor danh dau vi tri bat dau. Chua kem bo dieu khien nhan vat.\nVat lieu duoc tai tao trong Unity, khong dam bao giong tung pixel Blender. Khong doi bo cuc, xe tai hay kich thuoc kenh.\n");
 AssetDatabase.SaveAssets();AssetDatabase.Refresh();
 AssetDatabase.ExportPackage(A,"../Unity_Export/Prefab_Package/MinnanVillage_Complete.unitypackage",ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies);
 Debug.Log("MINNAN_PACKAGE_SUCCESS\n"+report);
 }
 static Camera MakeCamera(Transform p,string name,Vector3 pos,Vector3 target,float fov){var g=Child(name,p);g.transform.position=pos;g.transform.rotation=Quaternion.LookRotation(target-pos,Vector3.up);var c=g.AddComponent<Camera>();c.fieldOfView=fov;c.nearClipPlane=.05f;c.farClipPlane=150;c.enabled=false;c.clearFlags=CameraClearFlags.Skybox;return c;}
 static void Render(Camera c,string path){var rt=new RenderTexture(1024,768,24);c.targetTexture=rt;c.Render();var prev=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1024,768,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1024,768),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());RenderTexture.active=prev;c.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
 static string ValidateRoute(){
 var pts=new List<Vector3>();Action<Vector3,Vector3> line=(a,b)=>{int count=Mathf.CeilToInt(Vector3.Distance(a,b)/.08f);for(int i=0;i<count;i++)pts.Add(Vector3.Lerp(a,b,(float)i/count));};
 line(P(0,14.1f,0),P(0,-.5f,0));for(int i=0;i<49;i++){float a=Mathf.PI*.5f*i/48;pts.Add(P(-1.5f+1.5f*Mathf.Cos(a),-.5f-1.5f*Mathf.Sin(a),0));}line(P(-1.5f,-2,0),P(-7.8f,-2,0));line(P(-7.8f,-2,0),P(-8.8f,-1.75f,0));line(P(-8.8f,-1.75f,0),P(-15,-1.75f,0));line(P(-15,-1.75f,0),P(-22,-2,0));
 var hits=new HashSet<string>();int gaps=0;for(int i=0;i<pts.Count-1;i++){var dir=pts[i+1]-pts[i];if(dir.sqrMagnitude<.00001f)continue;var rot=Quaternion.LookRotation(dir);foreach(var c in Physics.OverlapBox(pts[i]+Vector3.up*.9f,new Vector3(.4f,.86f,.6f),rot))hits.Add(c.name);if(!Physics.Raycast(pts[i]+Vector3.up*.08f,Vector3.down,.2f))gaps++;}
 string msg="Unity physics route: "+pts.Count+" samples, collisions="+string.Join(",",hits)+", floor gaps="+gaps+". 0.80 x 1.20 m chair; 1.10 m truck bypass.";
 if(hits.Count>0||gaps>0){foreach(var c in UnityEngine.Object.FindObjectsOfType<Collider>())if(c.name.StartsWith("PATH_"))Debug.Log("FLOOR "+c.name+" "+c.bounds);throw new Exception(msg);}return msg;
 }
}
