namespace L2Toolkit.Models
{
    public class ItemsNameModel(string itemName, string additionalName)
    {
        public string ItemName { get; set; } = itemName;
        public string AdditionalName { get; set; } = additionalName;
    }
}