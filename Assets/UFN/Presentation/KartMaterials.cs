using System;
using UnityEngine;

namespace UFN.Presentation
{
    /// <summary>Resources assets are packaged explicitly, including their shader variants.
    /// Built-in shaders discovered only by Shader.Find can be stripped from a player.</summary>
    public static class KartMaterials
    {
        private static Shader lit, unlit;
        public static Material Create(Color color,bool emissive=false)
        {
            Shader shader;
            if(emissive){if(unlit==null)unlit=Resources.Load<Shader>("UFNKartUnlit");shader=unlit;}
            else{if(lit==null)lit=Resources.Load<Shader>("UFNKartLit");shader=lit;}
            if(shader==null)throw new InvalidOperationException("O shader incluído do kart não foi encontrado: "+(emissive?"UFNKartUnlit":"UFNKartLit"));
            var material=new Material(shader){color=color};
            if(!emissive){material.SetFloat("_Glossiness",.22f);material.SetFloat("_Metallic",.08f);}
            return material;
        }
    }
}
