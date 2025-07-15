namespace L2Toolkit.Utilities;

public static class MaterialType
{
    public static string GetMaterialType(string type)
    {
        return type switch
        {
            "gold" => "MONEY",
            "liquid" => "MATERIAL",
            "paper" => "SCROLL",
            _ => "NONE"
        };
    }
}