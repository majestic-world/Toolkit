namespace L2Toolkit.DataMap
{
    public class UpgradeLucera
    {
        public string UpgradeId { get; set; }
        public string UpgradeItem { get; set; }
        public string Materials { get; set; }
        public string Commission { get; set; }
        public string ResultItem { get; set; }

        public UpgradeLucera(string upgradeId, string upgradeItem, string materials, string commission, string resultItem)
        {
            UpgradeId = upgradeId;
            UpgradeItem = upgradeItem;
            Commission = commission;
            ResultItem = resultItem;
            Materials = materials;
        }
    }
}