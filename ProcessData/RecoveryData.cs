using System;
using System.Collections.Generic;
using L2Toolkit.DataMap;

namespace L2Toolkit.ProcessData;

public static class RecoveryData
{
    public static WeaponsInfo GetWeaponsData(string line)
    {
        var parts = line.Split('\t');
        var data = new Dictionary<string, string>();
        
        foreach (var part in parts)
        {
            if (!part.Contains('=')) continue;
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                data[keyValue[0]] = keyValue[1];
            }
        }
        
        var iconValue = data.GetValueOrDefault("icon", "");
        var extractedIcon = "";
        
        if (iconValue.Contains("{[") && iconValue.Contains("];"))
        {
            var startIndex = iconValue.IndexOf("{[", StringComparison.Ordinal) + 2;
            var endIndex = iconValue.IndexOf("];", startIndex, StringComparison.Ordinal);
            if (endIndex > startIndex)
            {
                extractedIcon = iconValue.Substring(startIndex, endIndex - startIndex);
            }
        }
        
        return new WeaponsInfo(
            ObjectId: data.GetValueOrDefault("object_id", "0"),
            Icon: extractedIcon,
            Weight: data.GetValueOrDefault("weight", "0"),
            MaterialType: data.GetValueOrDefault("material_type", ""),
            Crystallizable: data.GetValueOrDefault("crystallizable", "0"),
            BodyPart: data.GetValueOrDefault("body_part", ""),
            RandomDamage: data.GetValueOrDefault("random_damage", "0"),
            WeaponType: data.GetValueOrDefault("weapon_type", ""),
            CrystalType: data.GetValueOrDefault("crystal_type", ""),
            MpConsume: data.GetValueOrDefault("mp_consume", "0"),
            SoulshotCount: data.GetValueOrDefault("soulshot_count", "0"),
            SpiritshotCount: data.GetValueOrDefault("spiritshot_count", "0"),
            IsMagicWeapon: data.GetValueOrDefault("is_magic_weapon", "0")
        );
    }
}