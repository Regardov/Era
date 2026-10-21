using System.Collections.Generic;
using UnityEngine;
namespace Era
{
    public static class IconLibrary
    {
        static readonly Dictionary<string,Texture2D> Cache=new Dictionary<string,Texture2D>();
        public static Texture2D Get(string name){if(!Cache.TryGetValue(name,out var texture)){texture=Resources.Load<Texture2D>("Icons/"+name);Cache[name]=texture;}return texture;}
    }
}
