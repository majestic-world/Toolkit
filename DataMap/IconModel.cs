namespace L2Toolkit.DataMap
{
    public class IconModel
    {
        public string Id { get; set; }
        public string Icon { get; set; }
        public string IconPanel { get; set; }

        public IconModel(string id, string icon, string iconPanel)
        {
            Id = id;
            Icon = icon;
            IconPanel = iconPanel;
        }
    }
}