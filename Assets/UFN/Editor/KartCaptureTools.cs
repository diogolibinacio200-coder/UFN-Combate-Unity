#if UNITY_EDITOR
using System.IO;
using UFN.Core;
using UFN.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UFN.Editor
{
    public static class KartCaptureTools
    {
        public static void Capture()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../docs/screens/kart"));Directory.CreateDirectory(output);
            RenderSettings.ambientLight=new Color(.57f,.62f,.68f);
            var sun=new GameObject("Light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.15f;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            var camera=new GameObject("Capture camera").AddComponent<Camera>();camera.backgroundColor=new Color(.075f,.105f,.145f);camera.clearFlags=CameraClearFlags.SolidColor;camera.fieldOfView=36;camera.nearClipPlane=.05f;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Display platform";ground.transform.position=new Vector3(0,-.1f,0);ground.transform.localScale=new Vector3(20,.15f,20);var groundMat=KartMaterials.Create(new Color(.14f,.18f,.22f));ground.GetComponent<Renderer>().sharedMaterial=groundMat;
            var gallery=new Texture2D(1920,960,TextureFormat.RGB24,false);
            for(int n=0;n<15;n++)
            {
                int id=n==14?3:n,skin=n==14?1:0;var model=KartModel.Build(null,id,skin);
                string key=KartAppearance.Get(id,skin).Reference;
                var text=new GameObject("Label").AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;text.text=n==14?"DIOGO · CARECA":KartContent.Names[id];text.fontSize=48;text.characterSize=Mathf.Min(.14f,1.45f/text.text.Length);text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;text.transform.position=new Vector3(0,2.8f,0);
                for(int angle=0;angle<3;angle++)
                {
                    camera.transform.position=angle==0?new Vector3(4,3.3f,5.3f):angle==1?new Vector3(-.01f,2.2f,-7):new Vector3(7,2.3f,.01f);camera.transform.LookAt(new Vector3(0,1.1f,0));text.transform.rotation=camera.transform.rotation;
                    Texture2D capture=Render(camera,384,320);File.WriteAllBytes(Path.Combine(output,key+"-"+new[]{"front","back","side"}[angle]+".png"),capture.EncodeToPNG());
                    if(angle==0)gallery.SetPixels((n%5)*384,(2-n/5)*320,384,320,capture.GetPixels());
                    Object.DestroyImmediate(capture);
                }
                Object.DestroyImmediate(text.gameObject);Object.DestroyImmediate(model.gameObject);
            }
            gallery.Apply();File.WriteAllBytes(Path.Combine(output,"all-15-drivers.png"),gallery.EncodeToPNG());Object.DestroyImmediate(gallery);Object.DestroyImmediate(ground.gameObject);Object.DestroyImmediate(groundMat);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(sun.gameObject);
            for(int track=0;track<5;track++)
            {
                var race=new KartRace(812,3,track,KartRaceMode.Quick,2,1);var view=new GameObject("Track "+track).AddComponent<KartPresentation>();view.Bind(race);
                var cameras=view.GetComponentsInChildren<Camera>();foreach(var c in cameras)c.enabled=false;
                var overview=new GameObject("Overview").AddComponent<Camera>();overview.backgroundColor=new Color(.15f,.23f,.3f);overview.clearFlags=CameraClearFlags.SolidColor;overview.orthographic=true;overview.orthographicSize=126;overview.transform.position=new Vector3(0,230,0);overview.transform.rotation=Quaternion.Euler(90,0,0);
                var shot=Render(overview,1280,960);File.WriteAllBytes(Path.Combine(output,"track-"+track+"-overview.png"),shot.EncodeToPNG());Object.DestroyImmediate(shot);Object.DestroyImmediate(overview.gameObject);
                if(track==4){foreach(var c in cameras)c.enabled=true;var combined=new Texture2D(1280,720,TextureFormat.RGB24,false);for(int i=0;i<cameras.Length;i++){Rect old=cameras[i].rect;cameras[i].rect=new Rect(0,0,1,1);var half=Render(cameras[i],640,720);combined.SetPixels(i*640,0,640,720,half.GetPixels());Object.DestroyImmediate(half);cameras[i].rect=old;}combined.Apply();File.WriteAllBytes(Path.Combine(output,"andradras-split-screen.png"),combined.EncodeToPNG());Object.DestroyImmediate(combined);}
                Object.DestroyImmediate(view.gameObject);
            }
            Debug.Log("UFN_KART_CAPTURE_COMPLETE: 15 driver variants, front/back/side, 5 track overviews, split screen.");
        }
        private static Texture2D Render(Camera camera,int width,int height)
        {
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();RenderTexture previous=RenderTexture.active;RenderTexture.active=rt;var result=new Texture2D(width,height,TextureFormat.RGB24,false);result.ReadPixels(new Rect(0,0,width,height),0,0);result.Apply();RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);return result;
        }
    }
}
#endif
