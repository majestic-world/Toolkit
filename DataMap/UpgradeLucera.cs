namespace L2Toolkit.DataMap
{
    public class UpgradeLucera(string upgradeId, string upgradeItem, string materials, string commission, string resultItem)
    {
        public string UpgradeId { get; set; } = upgradeId;
        public string UpgradeItem { get; set; } = upgradeItem;
        public string Materials { get; set; } = materials;
        public string Commission { get; set; } = commission;
        public string ResultItem { get; set; } = resultItem;
    }
}