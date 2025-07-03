using System.Collections.Generic;

namespace L2Toolkit.DataMap
{
    public class UpgradeData
    {
        public string UpgradeId { get; set; }
        public string UpgradeItem { get; set; }
        public List<string> Materials { get; set; }
        public string Commission { get; set; }
        public string ResultItem { get; set; }

        public UpgradeData(string upgradeId, string upgradeItem, List<string> materials, string commission, string resultItem)
        {
            UpgradeId = upgradeId;
            UpgradeItem = upgradeItem;
            Commission = commission;
            ResultItem = resultItem;
            Materials = materials;
        }
    }
}