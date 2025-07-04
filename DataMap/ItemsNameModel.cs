namespace L2Toolkit.DataMap
{
    public class ItemsNameModel(string itemName, string additionalName)
    {
        public string ItemName { get; set; } = itemName;
        public string AdditionalName { get; set; } = additionalName;
    }
}