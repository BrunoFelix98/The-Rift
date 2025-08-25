using System;

[Serializable]
public class NebulaDTO
{
    public string nebulaName;
    public float[] nebulaColor; // Represented as float array [r, g, b, a]
    public float density;
    public float[] size; // float array [x, y, z]
    public string description;
}