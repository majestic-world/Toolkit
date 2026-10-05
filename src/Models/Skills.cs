using System.Collections.Generic;

namespace L2Toolkit.Models;

public record Skills(
    string Id,
    string Name,
    string Levels,
    List<EnchantData> Enchants
);