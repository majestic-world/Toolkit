namespace L2Toolkit.DataMap
{
    public class ItemsNameModel
    {
        public string ItemName { get; set; }
        public string AdditionalName { get; set; }

        public ItemsNameModel(string itemName, string additionalName)
        {
            ItemName = itemName;
            AdditionalName = additionalName;
        }
    }
}