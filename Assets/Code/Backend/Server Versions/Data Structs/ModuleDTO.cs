using System;
using System.Collections.Generic;

[Serializable]
public class ModuleDTO
{
    public string moduleName;
    public List<string> compatibleSlots = new List<string>();
    public string blueprintId;
    public ModuleCategory moduleCategory;
}