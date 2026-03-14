using System.Collections.Generic;

namespace L2Toolkit.DatReader;

/// <summary>
/// Enum-to-string mappings matching the Java L2 client tool output format.
/// Used during text export so that reimport by the Java app works correctly.
/// </summary>
public static class DatEnums
{
    public static readonly Dictionary<int, string> MaterialType = new()
    {
        [0] = "fish",
        [1] = "oriharukon",
        [2] = "mithril",
        [3] = "gold",
        [4] = "silver",
        [6] = "bronze",
        [8] = "steel",
        [13] = "wood",
        [14] = "bone",
        [17] = "cloth",
        [18] = "paper",
        [19] = "leather",
        [23] = "crystal",
        [33] = "cotton",
        [37] = "cobweb",
        [38] = "dyestuff",
        [46] = "scale_of_dragon",
        [47] = "adamantaite",
        [48] = "blood_steel",
        [49] = "chrysolite",
        [50] = "damascus",
        [51] = "fine_steel",
        [52] = "horn",
        [53] = "liquid"
    };

    public static readonly Dictionary<int, string> CrystalType = new()
    {
        [0] = "none",
        [1] = "d",
        [2] = "c",
        [3] = "b",
        [4] = "a",
        [5] = "s",
        [6] = "s80",
        [11] = "r110"
    };

    public static readonly Dictionary<int, string> InventoryType = new()
    {
        [1] = "equipment",
        [2] = "consumable",
        [3] = "material",
        [4] = "etc",
        [5] = "quest"
    };

    public static readonly Dictionary<int, string> ConsumeType = new()
    {
        [0] = "consume_type_normal",
        [2] = "consume_type_stackable",
        [3] = "consume_type_asset"
    };

    public static readonly Dictionary<int, string> EtcItemType = new()
    {
        [0] = "none",
        [1] = "scroll",
        [2] = "arrow",
        [3] = "potion",
        [5] = "recipe",
        [6] = "material",
        [7] = "pet_collar",
        [8] = "castle_guard",
        [9] = "dye",
        [10] = "seed",
        [11] = "seed2",
        [12] = "harvest",
        [13] = "lotto",
        [14] = "race_ticket",
        [15] = "ticket_of_lord",
        [16] = "lure",
        [17] = "crop",
        [18] = "maturecrop",
        [19] = "encht_wp",
        [20] = "encht_am",
        [21] = "bless_encht_wp",
        [22] = "bless_encht_am",
        [23] = "coupon",
        [24] = "elixir",
        [27] = "bolt",
        [29] = "encht_attr_inc_prop_encht_am",
        [32] = "encht_attr_ancient_crystal_enchant_am",
        [33] = "encht_attr_ancient_crystal_enchant_wp",
        [34] = "encht_attr_rune",
        [36] = "teleportbookmark",
        [38] = "soulshot",
        [39] = "shape_shifting_wp",
        [42] = "shape_shifting_am",
        [51] = "restore_shape_shifting_allitem",
        [53] = "bless_inc_prop_encht_am",
        [54] = "card_event",
        [56] = "multi_encht_wp",
        [57] = "multi_encht_am",
        [59] = "multi_inc_prob_encht_am",
        [60] = "ensoul_stone",
        [61] = "nick_color_old",
        [62] = "nick_color_new",
        [63] = "encht_ag",
        [65] = "multi_encht_ag",
        [66] = "ancient_crystal_enchant_ag",
        [70] = "lock_item",
        [71] = "unlock_item",
        [73] = "costume_book",
        [74] = "costume_book_rd_all",
        [75] = "costume_book_rd_part",
        [76] = "costume_book_1",
        [77] = "costume_book_2",
        [78] = "costume_book_3",
        [79] = "costume_book_4"
    };

    public static readonly Dictionary<int, string> WeaponType = new()
    {
        [0] = "fist",
        [1] = "sword",
        [2] = "twohandsword",
        [3] = "buster",
        [4] = "blunt",
        [5] = "twohandblunt",
        [6] = "staff",
        [7] = "twohandstaff",
        [8] = "dagger",
        [9] = "pole",
        [10] = "dualfist",
        [11] = "bow",
        [12] = "weapon_etc",
        [13] = "dual",
        [15] = "fishingrod",
        [16] = "rapier",
        [17] = "crossbow",
        [18] = "ancientsword",
        [20] = "dualdagger",
        [22] = "twohandcrossbow",
        [23] = "dualblunt"
    };

    public static readonly Dictionary<int, string> ArmorType = new()
    {
        [0] = "none",
        [1] = "light",
        [2] = "heavy",
        [3] = "magic",
        [4] = "sigil"
    };

    public static readonly Dictionary<int, string> BodyPart = new()
    {
        [0] = "underwear",
        [1] = "rear",
        [3] = "neck",
        [4] = "rfinger",
        [6] = "head",
        [7] = "lrhand",
        [8] = "onepiece",
        [9] = "alldress",
        [10] = "hairall",
        [12] = "lbracelet",
        [13] = "deco1",
        [19] = "waist",
        [21] = "jewel1",
        [27] = "agathion_main",
        [54] = "gloves",
        [55] = "chest",
        [56] = "legs",
        [57] = "feet",
        [58] = "back",
        [59] = "hair",
        [60] = "hair2",
        [61] = "rhand",
        [62] = "lhand"
    };

    /// <summary>
    /// Resolves an enum value to its string name.
    /// Returns the numeric value as string if not found in the mapping.
    /// </summary>
    public static string Resolve(Dictionary<int, string> map, int value)
    {
        return map.TryGetValue(value, out var name) ? name : value.ToString();
    }
}
