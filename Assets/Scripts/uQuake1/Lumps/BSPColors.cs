using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.IO;

public class BSPColors
{
    public Color32[] colors = new Color32[256];
    private BinaryReader colorLump;
    public Texture2D debugTex;

    public BSPColors(bool transparent)
    {
		// This won't work unless the palette from Quake is at this location.
		// The palette/colors aren't generated, they're taken straight from that palette.
        colorLump = new BinaryReader(File.Open("Assets/Resources/id1/palette.lmp", FileMode.Open));
        RipColors(transparent);
        colorLump.BaseStream.Dispose();
        //DebugTex();
    }

    private void RipColors(bool transparent)
    {
        if (transparent) {
            // Special transparent pixel...
            colors[0] = new Color32(colorLump.ReadByte(), colorLump.ReadByte(), colorLump.ReadByte(), (byte)0.0f);
            for (int i = 1; i < 256; i++) {
                colors[i] = new Color32(colorLump.ReadByte(), colorLump.ReadByte(), colorLump.ReadByte(), (byte)255.0f);
            }
        } else {
            for (int i = 0; i < 256; i++) {
                colors[i] = new Color32(colorLump.ReadByte(), colorLump.ReadByte(), colorLump.ReadByte(), (byte)255.0f);
            }
        }
    }

    private void DebugTex()
    {
        debugTex = new Texture2D(16, 16);
        debugTex.SetPixels32(colors);
        debugTex.Apply();
    }
}

