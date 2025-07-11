using System.Collections.Generic;
using L2Toolkit.DataMap;

namespace L2Toolkit.ProcessData;

public class StatusItems
{
    public static CompleteStatusItems GetStausByLine(string line)
    {
        var parts = line.Split('\t');
        var data = new Dictionary<string, string>();
        
        // Parse cada parte da linha
        foreach (var part in parts)
        {
            if (!part.Contains('=')) continue;
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                data[keyValue[0]] = keyValue[1];
            }
        }
        
        return new CompleteStatusItems(
            Id: data.GetValueOrDefault("object_id", "0"),
            PDefense: data.GetValueOrDefault("pDefense", "0"),
            MDefense: data.GetValueOrDefault("mDefense", "0"),
            PAttack: data.GetValueOrDefault("pAttack", "0"),
            MAttack: data.GetValueOrDefault("mAttack", "0"),
            PAttackSpeed: data.GetValueOrDefault("pAttackSpeed", "0"),
            PHit: data.GetValueOrDefault("pHit", "0.0"),
            MHit: data.GetValueOrDefault("mHit", "0.0"),
            PCritical: data.GetValueOrDefault("pCritical", "0.0"),
            MCritical: data.GetValueOrDefault("mCritical", "0.0"),
            Speed: data.GetValueOrDefault("speed", "0"),
            ShieldDefense: data.GetValueOrDefault("ShieldDefense", "0"),
            ShieldDefenseRate: data.GetValueOrDefault("ShieldDefenseRate", "0"),
            PAvoid: data.GetValueOrDefault("pavoid", "0.0"),
            MAvoid: data.GetValueOrDefault("mavoid", "0.0"),
            PropertyParams: data.GetValueOrDefault("property_params", "0")
        );
    }
}