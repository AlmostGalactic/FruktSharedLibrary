using System;
using System.IO;
using FruktSharedLibrary.Core;
using UnityEngine;

namespace FruktSharedLibrary.Utilities
{
    /// <summary>
    /// Loading and saving images. Unity's own ImageConversion methods are stripped from IL2CPP builds, so this
    /// goes through MelonLoader's image-conversion bridge, which works on this game's Unity 6 runtime.
    /// </summary>
    public static class Textures
    {
        /// <summary>Loads a PNG or JPG file into a new texture (null if it can't be read).</summary>
        public static Texture2D LoadFromFile(string path, FilterMode filter = FilterMode.Bilinear)
        {
            try
            {
                return LoadFromBytes(File.ReadAllBytes(path), filter, Path.GetFileNameWithoutExtension(path));
            }
            catch (Exception e)
            {
                FruktLog.Warning($"Loading texture '{path}' failed: {e.Message}");
                return null;
            }
        }

        /// <summary>Decodes PNG or JPG bytes into a new texture (null if they can't be decoded).</summary>
        public static Texture2D LoadFromBytes(byte[] data, FilterMode filter = FilterMode.Bilinear, string name = "FruktTexture")
        {
            if (data == null || data.Length == 0)
                return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = filter,
            };
            try
            {
                if (Il2CppImageConversionManager.LoadImage(texture, data, false))
                    return texture;
            }
            catch (Exception e)
            {
                FruktLog.Warning("Decoding an image failed: " + e.Message);
            }
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        /// <summary>Encodes a readable texture as PNG bytes.</summary>
        public static byte[] EncodeToPng(Texture2D texture)
        {
            if (texture == null)
                return null;
            try
            {
                return Il2CppImageConversionManager.EncodeToPNG(texture);
            }
            catch (Exception e)
            {
                FruktLog.Warning("Encoding a PNG failed: " + e.Message);
                return null;
            }
        }

        /// <summary>Saves a readable texture as a PNG file.</summary>
        public static bool SavePng(Texture2D texture, string path)
        {
            var bytes = EncodeToPng(texture);
            if (bytes == null)
                return false;
            File.WriteAllBytes(path, bytes);
            return true;
        }

        /// <summary>Wraps a whole texture in a sprite (for UI images).</summary>
        public static Sprite ToSprite(Texture2D texture, float pixelsPerUnit = 100f)
        {
            if (texture == null)
                return null;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>A solid-colour texture.</summary>
        public static Texture2D Solid(Color color, int width = 1, int height = 1)
        {
            var texture = new Texture2D(Math.Max(1, width), Math.Max(1, height), TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
