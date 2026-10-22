using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace UFN.Presentation
{
    /// <summary>Reads the original atlases without re-encoding, resampling or cutting limbs.</summary>
    public sealed class AtlasLibrary : IDisposable
    {
        public sealed class Atlas
        {
            public Texture2D Texture;
            public readonly Dictionary<string, Sprite> Frames = new Dictionary<string, Sprite>();
            public readonly Dictionary<string, string[]> Animations = new Dictionary<string, string[]>();
            public Sprite Frame(string name) => Frames.TryGetValue(name ?? "idle0", out var sprite) ? sprite : Frames["idle0"];
            public Sprite Sample(string animation, float phase)
            {
                if (!Animations.TryGetValue(animation ?? "idle", out var frames)) frames = Animations["idle"];
                return Frame(frames[Mathf.Clamp((int)(Mathf.Clamp01(phase) * frames.Length), 0, frames.Length - 1)]);
            }
        }
        readonly string root;
        readonly Dictionary<string, Atlas> atlases = new Dictionary<string, Atlas>();
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, Sprite> auxiliary = new Dictionary<string, Sprite>();
        public int LoadedAtlasCount => atlases.Count;
        public AtlasLibrary(string contentPath) { root = contentPath; }
        public Atlas Get(string key)
        {
            if (atlases.TryGetValue(key, out var atlas)) return atlas;
            string folder = Path.Combine(root, "sprites", key);
            var data = (Dictionary<string, object>)AtlasJson.Parse(File.ReadAllText(Path.Combine(folder, "frames.json")));
            atlas = new Atlas { Texture = Texture(Path.Combine(folder, "atlas.png")) };
            Vector2 anchor = new Vector2(192, 304);
            if (data.TryGetValue("anchor", out var av)) anchor = Point((List<object>)av);
            var frames = (Dictionary<string, object>)data["frames"];
            var clips = data.TryGetValue("frame_clips", out var cv) ? (Dictionary<string, object>)cv : null;
            foreach (var pair in frames)
            {
                var a = (List<object>)pair.Value;
                var rect = new Rect(Number(a[0]), Number(a[1]), Number(a[2]), Number(a[3]));
                List<object> clip = null;
                if (clips != null && clips.TryGetValue(pair.Key, out var polygon)) clip = polygon as List<object>;
                atlas.Frames.Add(pair.Key, MakeSprite(atlas.Texture, rect, anchor, clip, key + "/" + pair.Key));
            }
            foreach (var pair in (Dictionary<string, object>)data["animations"])
            {
                var list = (List<object>)pair.Value; var names = new string[list.Count];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = (string)list[i];
                    if (!atlas.Frames.ContainsKey(names[i])) throw new InvalidDataException(key + ": missing animation frame " + names[i]);
                }
                if (names.Length == 0) throw new InvalidDataException(key + ": empty animation " + pair.Key);
                atlas.Animations.Add(pair.Key, names);
            }
            atlases.Add(key, atlas); return atlas;
        }
        public Texture2D Texture(string path)
        {
            if (textures.TryGetValue(path, out var texture)) return texture;
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = Path.GetFileName(path), filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), true)) throw new InvalidDataException("Invalid PNG: " + path);
            textures.Add(path, texture); return texture;
        }
        public Sprite Stage(string key)
        {
            string id = "stage/" + key;
            if (auxiliary.TryGetValue(id, out var cached)) return cached;
            var tex = Texture(Path.Combine(root, "arenas", key + ".png"));
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = id; auxiliary.Add(id, sprite); return sprite;
        }
        public Sprite Portrait(string key)
        {
            string id = "portrait/" + key;
            if (auxiliary.TryGetValue(id, out var cached)) return cached;
            var tex = Texture(Path.Combine(root, "sprites", key, "portrait.png"));
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100);
            auxiliary.Add(id, sprite); return sprite;
        }
        public Sprite Bear(int frame)
        {
            var bounds = new[] { new Rect(76,18,265,401),new Rect(521,25,301,396),new Rect(968,29,313,388),new Rect(1340,66,433,355),new Rect(48,426,322,436),new Rect(490,546,427,285),new Rect(929,470,395,386),new Rect(1382,501,352,343) };
            float[] feet = {210,666,1093,1488,224,674,1116,1574}; frame = Mathf.Clamp(frame,0,7);
            string id="bear/"+frame;
            if (auxiliary.TryGetValue(id,out var cached)) return cached;
            var rect=bounds[frame]; var sprite=MakeSprite(Texture(Path.Combine(root,"sprites","urso.png")),rect,new Vector2(feet[frame]-rect.x,rect.height),null,id);
            auxiliary.Add(id,sprite);return sprite;
        }
        public Sprite Ally(string frame)
        {
            string id="ally/"+frame;if(auxiliary.TryGetValue(id,out var cached))return cached;
            Rect rect;Vector2 anchor;List<object> clip=null;
            switch(frame)
            {
                case "run0":rect=new Rect(376,0,392,512);anchor=new Vector2(181,495);break;
                case "run1":rect=new Rect(770,0,394,512);anchor=new Vector2(222,494);break;
                case "windup":rect=new Rect(1176,0,360,512);anchor=new Vector2(183,499);break;
                case "punch0":rect=new Rect(0,512,422,512);anchor=new Vector2(189,487);break;
                case "punch1":rect=new Rect(420,512,350,512);anchor=new Vector2(180,487);clip=Polygon(0,0,331,0,331,150,350,200,350,512,0,512);break;
                case "kick":rect=new Rect(750,512,490,512);anchor=new Vector2(194,487);clip=Polygon(0,0,490,0,490,225,270,285,270,512,115,512,100,320,0,210);break;
                default:rect=new Rect(0,0,360,512);anchor=new Vector2(175,499);break;
            }
            var sprite=MakeSprite(Texture(Path.Combine(root,"sprites","vitor_ally_blonde","atlas.png")),rect,anchor,clip,id);
            auxiliary.Add(id,sprite);return sprite;
        }
        static List<object> Polygon(params float[] xy)
        {
            var list=new List<object>();for(int i=0;i<xy.Length;i+=2)list.Add(new List<object>{(double)xy[i],(double)xy[i+1]});return list;
        }
        static Sprite MakeSprite(Texture2D texture,Rect topRect,Vector2 anchor,List<object> clip,string name)
        {
            if(topRect.x<0||topRect.y<0||topRect.xMax>texture.width||topRect.yMax>texture.height)throw new InvalidDataException("Atlas bounds: "+name);
            var rect=new Rect(topRect.x,texture.height-topRect.yMax,topRect.width,topRect.height);
            var sprite=Sprite.Create(texture,rect,new Vector2(anchor.x/rect.width,1-anchor.y/rect.height),100,0,SpriteMeshType.FullRect);
            sprite.name=name;
            if(clip!=null&&clip.Count>2)
            {
                var vertices=new Vector2[clip.Count];
                // OverrideGeometry takes pixels in Sprite.rect space. Unity applies
                // pivot and pixels-per-unit itself (unlike Sprite.vertices).
                for(int i=0;i<vertices.Length;i++){Vector2 p=Point((List<object>)clip[i]);vertices[i]=new Vector2(p.x,rect.height-p.y);}
                sprite.OverrideGeometry(vertices,Triangulate(vertices));
            }
            return sprite;
        }
        static ushort[] Triangulate(Vector2[] p)
        {
            var remaining=new List<int>();float area=0;
            for(int i=0;i<p.Length;i++){remaining.Add(i);var b=p[(i+1)%p.Length];area+=p[i].x*b.y-b.x*p[i].y;}
            if(area<0)remaining.Reverse();var result=new List<ushort>();int safety=p.Length*p.Length;
            while(remaining.Count>2&&safety-->0)
            {
                bool removed=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
                    if(Cross(p[b]-p[a],p[c]-p[b])<=0.000001f)continue;
                    bool occupied=false;
                    foreach(int v in remaining)if(v!=a&&v!=b&&v!=c&&Cross(p[b]-p[a],p[v]-p[a])>=0&&Cross(p[c]-p[b],p[v]-p[b])>=0&&Cross(p[a]-p[c],p[v]-p[c])>=0){occupied=true;break;}
                    if(occupied)continue;result.Add((ushort)a);result.Add((ushort)b);result.Add((ushort)c);remaining.RemoveAt(i);removed=true;break;
                }
                if(!removed)throw new InvalidDataException("Invalid authored atlas contour");
            }
            return result.ToArray();
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static Vector2 Point(List<object> v)=>new Vector2(Number(v[0]),Number(v[1]));
        static float Number(object o)=>Convert.ToSingle(o,CultureInfo.InvariantCulture);
        public void Dispose()
        {
            foreach(var atlas in atlases.Values)foreach(var sprite in atlas.Frames.Values)UnityEngine.Object.Destroy(sprite);
            foreach(var sprite in auxiliary.Values)UnityEngine.Object.Destroy(sprite);
            foreach(var texture in textures.Values)UnityEngine.Object.Destroy(texture);
            atlases.Clear();auxiliary.Clear();textures.Clear();
        }
    }

    /// <summary>Small strict reader for the existing JSON dictionaries. No runtime package download.</summary>
    internal sealed class AtlasJson
    {
        readonly string text;int cursor;AtlasJson(string text){this.text=text;}
        public static object Parse(string text){var reader=new AtlasJson(text);var value=reader.Value();reader.Space();if(reader.cursor!=text.Length)throw new InvalidDataException("Trailing JSON data");return value;}
        void Space(){while(cursor<text.Length&&char.IsWhiteSpace(text[cursor]))cursor++;}
        char Take(){if(cursor>=text.Length)throw new InvalidDataException("Incomplete atlas JSON");return text[cursor++];}
        void Require(char c){Space();if(Take()!=c)throw new InvalidDataException("Invalid atlas JSON at "+cursor);}
        object Value()
        {
            Space();if(cursor>=text.Length)throw new InvalidDataException("Missing JSON value");char c=text[cursor];
            if(c=='"')return String();
            if(c=='{'){cursor++;var d=new Dictionary<string,object>();Space();if(text[cursor]=='}'){cursor++;return d;}while(true){string key=String();Require(':');d.Add(key,Value());Space();c=Take();if(c=='}')return d;if(c!=',')throw new InvalidDataException("Object separator");Space();}}
            if(c=='['){cursor++;var a=new List<object>();Space();if(text[cursor]==']'){cursor++;return a;}while(true){a.Add(Value());Space();c=Take();if(c==']')return a;if(c!=',')throw new InvalidDataException("Array separator");}}
            int start=cursor;while(cursor<text.Length&&!char.IsWhiteSpace(text[cursor])&&",]}".IndexOf(text[cursor])<0)cursor++;
            string token=text.Substring(start,cursor-start);if(token=="true")return true;if(token=="false")return false;if(token=="null")return null;
            return double.Parse(token,NumberStyles.Float,CultureInfo.InvariantCulture);
        }
        string String()
        {
            Require('"');var b=new StringBuilder();while(true){char c=Take();if(c=='"')return b.ToString();if(c!='\\'){b.Append(c);continue;}c=Take();switch(c){case '"':case '\\':case '/':b.Append(c);break;case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;case 'b':b.Append('\b');break;case 'f':b.Append('\f');break;case 'u':b.Append((char)int.Parse(text.Substring(cursor,4),NumberStyles.HexNumber));cursor+=4;break;default:throw new InvalidDataException("JSON escape");}}
        }
    }
}
