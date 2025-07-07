using System.Collections.Generic;

namespace L2Toolkit.DataMap;

public record Skills(
    string Id,
    string Name,
    string Levels,
    List<EnchantData> Enchants
);