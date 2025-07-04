namespace L2Toolkit.DataMap
{
    public class IconModel(string id, string icon, string iconPanel)
    {
        public string Id { get; set; } = id;
        public string Icon { get; set; } = icon;
        public string IconPanel { get; set; } = iconPanel;
    }
}