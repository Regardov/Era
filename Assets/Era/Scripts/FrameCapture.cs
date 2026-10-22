using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Era
{
    // Renders the actual camera and the same HUD drawing commands into a texture.
    // Useful on CI or with hidden windows, where the OS backbuffer can stay entirely black.
    public sealed class FrameCapture
    {
        readonly Material material;
        readonly Font font;
        public FrameCapture(Font font)
        { this.font = font; material = new Material(Shader.Find("Hidden/Era/Capture")); }
        public void Rect(Rect rect, Color color, Texture texture = null,Rect? textureUV=null)
        {
            var uv=textureUV??new Rect(0,0,1,1);
            material.mainTexture = texture ?? Texture2D.whiteTexture; material.SetFloat("_IsFont", 0); material.SetPass(0);
            GL.Begin(GL.QUADS); GL.Color(color.linear);
            GL.TexCoord2(uv.xMin, uv.yMax); GL.Vertex3(rect.xMin, rect.yMin, 0);
            GL.TexCoord2(uv.xMax, uv.yMax); GL.Vertex3(rect.xMax, rect.yMin, 0);
            GL.TexCoord2(uv.xMax, uv.yMin); GL.Vertex3(rect.xMax, rect.yMax, 0);
            GL.TexCoord2(uv.xMin, uv.yMin); GL.Vertex3(rect.xMin, rect.yMax, 0);
            GL.End();
        }
        float Width(string value, int size, FontStyle style)
        {
            float width = 0; foreach (char c in value) if (font.GetCharacterInfo(c, out var info, size, style)) width += info.advance;
            return width;
        }
        public void Text(string value, Rect rect, int size, Color color, bool bold, TextAnchor align)
        {
            if (string.IsNullOrEmpty(value)) return;
            var style = bold ? FontStyle.Bold : FontStyle.Normal;
            font.RequestCharactersInTexture(value, size, style);
            var block=HudTextLayout.Fit(font,value,size,rect.width,rect.height,bold);size=block.size;
            font.RequestCharactersInTexture(value,size,style);
            var lines=block.lines;float height=block.lineHeight;
            bool center = align == TextAnchor.UpperCenter || align == TextAnchor.MiddleCenter || align == TextAnchor.LowerCenter;
            bool right = align == TextAnchor.UpperRight || align == TextAnchor.MiddleRight || align == TextAnchor.LowerRight;
            float baseline=HudTextLayout.Top(rect,block,align)+size;
            foreach (var line in lines)
            {
                float width = Width(line, size, style);
                float x = rect.x + (center ? (rect.width - width) / 2 : right ? rect.width - width : 0);
                material.mainTexture = font.material.mainTexture; material.SetFloat("_IsFont", 1); material.SetPass(0);
                GL.Begin(GL.QUADS); GL.Color(color.linear);
                foreach (char c in line)
                {
                    if (!font.GetCharacterInfo(c, out var info, size, style)) continue;
                    GL.TexCoord(info.uvTopLeft); GL.Vertex3(x + info.minX, baseline - info.maxY, 0);
                    GL.TexCoord(info.uvTopRight); GL.Vertex3(x + info.maxX, baseline - info.maxY, 0);
                    GL.TexCoord(info.uvBottomRight); GL.Vertex3(x + info.maxX, baseline - info.minY, 0);
                    GL.TexCoord(info.uvBottomLeft); GL.Vertex3(x + info.minX, baseline - info.minY, 0);
                    x += info.advance;
                }
                GL.End(); baseline += height;
            }
        }
        public void Save(string path, Camera camera, System.Action draw)
        {
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target; camera.Render(); camera.targetTexture = previousTarget;
                RenderTexture.active = target;
                GL.PushMatrix(); GL.LoadPixelMatrix(0, 1600, 900, 0);
                try { draw(); } finally { GL.PopMatrix(); }
                var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
                Object.Destroy(image);
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        public void Dispose() { if (material != null) Object.Destroy(material); }
    }
}
