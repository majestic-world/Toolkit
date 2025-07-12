using System.Xml.Linq;

namespace L2Toolkit.ProcessData;

public static class XmlDataParse
{

    public static string GetWeaponType(string type)
    {
        return type switch
        {
            "twohandsword" => "BIGSWORD",
            "bow" => "BOW",
            "staff" => "BLUNT",
            "dual" => "DUAL",
            "sword" => "SWORD",
            "buster" => "BLUNT",
            "dagger" => "DAGGER",
            "dualfist" => "DUALFIST",
            "pole" => "POLE",
            "blunt" => "BLUNT",
            "twohandblunt" => "BLUNT",
            "twohandstaff" => "BIGBLUNT",
            "dualdagger" => "DUALDAGGER",
            "twohandcrossbow" => "CROSSBOW",
            "dualblunt" => "DUAL",
            "fist" => "NONE",
            _ => type
        };
    }

    public static string GetWeaponSlot(string type)
    {
        return type switch
        {
            "lrhand" => "LEFT_RIGHT_HAND",
            "rhand1" => "RIGHT_HAND",
            "rhand" => "LEFT_HAND",
            _ => type
        };
    }
    
    public static (string type, string crystal) GetCrystal(string crystal)
    {
        return crystal switch
        {
            "none" => ("NONE", ""),
            "d" => ("D", ""),
            "c" => ("C", ""),
            "b" => ("B", "1157"),
            "a" => ("A", "1464"),
            "s" => ("S", "2440"),
            "s80" => ("S80", "2440"),
            _ => (crystal.ToUpper(), "2440")
        };
    }
    
    public static (string type, string crystal) GetCrystalWithConversion(string crystal)
    {
        return crystal switch
        {
            "none" => ("NONE", ""),
            "d" => ("D", ""),
            "c" => ("C", ""),
            "b" => ("B", "1157"),
            "a" => ("A", "1464"),
            "s" => ("S", "2440"),
            "s80" => ("S", "2440"),
            "l" => ("S", "2440"),
            "r95" => ("S", "2440"),
            "r99" => ("S", "2440"),
            "r110" => ("S", "2440"),
            _ => (crystal.ToUpper(), "2440")
        };
    }

    public static XElement GetEnchantData(string partyName)
    {
        return partyName switch
        {
            "head" => new XElement("enchant", new XAttribute("stat", "pDef"), new XAttribute("order", "0x0C"), new XAttribute("value", "0")),
            "onepiece" => new XElement("enchant", new XAttribute("stat", "pDef"), new XAttribute("order", "0x0C"), new XAttribute("value", "0")),
            "chest" => new XElement("enchant", new XAttribute("stat", "pDef"), new XAttribute("order", "0x0C"), new XAttribute("value", "0")),
            "legs" => new XElement("enchant", new XAttribute("stat", "pDef"), new XAttribute("order", "0x0C"), new XAttribute("value", "0")),
            "gloves" => new XElement("enchant", new XAttribute("stat", "pDef"), new XAttribute("order", "0x0C"), new XAttribute("value", "0")),
            "feet" => new XElement("enchant", new XAttribute("stat", "pDef"), new XAttribute("order", "0x0C"), new XAttribute("value", "0")),
            "rfinger" => new XElement("enchant", new XAttribute("stat", "mDef"), new XAttribute("order", "0x0C"), new XAttribute("value", "0")),
            "rear" => new XElement("enchant", new XAttribute("stat", "mDef"), new XAttribute("order", "0x0C"), new XAttribute("value", "0")),
            "neck" => new XElement("enchant", new XAttribute("stat", "mDef"), new XAttribute("order", "0x0C"), new XAttribute("value", "0")),
            _ => null
        };
    }

    public static string GetPartyBody(string partyName)
    {
        return partyName switch
        {
            "head" => "HEAD",
            "onepiece" => "FULL_ARMOR",
            "chest" => "CHEST",
            "legs" => "LEGS",
            "gloves" => "GLOVES",
            "feet" => "FEET",
            "rfinger" => "LEFT_FINGER;RIGHT_FINGER",
            "rear" => "RIGHT_EAR;LEFT_EAR",
            "neck" => "NECKLACE",
            "waist" => "BELT",
            _ => "",
        };
    }
}