using L2Toolkit.Utilities;

namespace L2Toolkit.Parse;

public record ItemName(string Id, string Name, string AdditionalName);

public class ParseName
{
    public static ItemName GetNameByLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var id = Parser.GetValue(line, "id=", "\t");
        var name = Parser.GetValue(line, "name=[", "]");
        var additionalName = Parser.GetValue(line, "additionalname=[", "]");
        return new ItemName(id, name, additionalName);
    }
}