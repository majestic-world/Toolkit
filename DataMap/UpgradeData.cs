using System.Collections.Generic;

namespace L2Toolkit.DataMap
{
    public class UpgradeData(string upgradeId, string upgradeItem, List<string> materials, string commission, string resultItem)
    {
        public string UpgradeId { get; set; } = upgradeId;
        public string UpgradeItem { get; set; } = upgradeItem;
        public List<string> Materials { get; set; } = materials;
        public string Commission { get; set; } = commission;
        public string ResultItem { get; set; } = resultItem;
    }
}