using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;

namespace L2Toolkit
{
    public partial class DoorGenerateControl : UserControl
    {
        public DoorGenerateControl()
        {
            InitializeComponent();
            
            ConvertButton.Click += ConvertButton_Click;
            CopyButton.Click += CopyButton_Click;
        }

        private void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            string content = InputTextBox.Text;
            string xmlOutput = ConvertToXml(content);
            OutputTextBox.Text = xmlOutput;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string content = OutputTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(content))
            {
                Clipboard.SetText(content);
                MessageBox.Show("XML copiado para a área de transferência!", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private string ConvertToXml(string text)
        {
            try
            {
                Match idMatch = Regex.Match(text, @"L2ServerObjectRealID=(\d+)");
                Match meshMatch = Regex.Match(text, @"StaticMesh=StaticMesh'([\w\.]+)'");
                Match basePosMatch = Regex.Match(text, @"BasePos=\(X=([-0-9.]+),Y=([-0-9.]+),Z=([-0-9.]+)\)");
                MatchCollection rangeDeltaMatches = Regex.Matches(text, @"RangeDelta\(\d\)=\(X=([-0-9.]+),Y=([-0-9.]+)");

                if (!idMatch.Success || !meshMatch.Success || !basePosMatch.Success || rangeDeltaMatches.Count != 4)
                {
                    return "Erro: Dados incompletos ou inválidos.";
                }

                string doorId = idMatch.Groups[1].Value;
                string meshName = meshMatch.Groups[1].Value;
                
                double baseX = double.Parse(basePosMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                double baseY = double.Parse(basePosMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                double baseZ = double.Parse(basePosMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                
                int minZ = -10170;
                int maxZ = (int)baseZ;

                List<(int x, int y)> vertices = new List<(int x, int y)>();
                
                for (int i = 0; i < 4; i++)
                {
                    double deltaX = double.Parse(rangeDeltaMatches[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    double deltaY = double.Parse(rangeDeltaMatches[i].Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    
                    int absX = (int)Math.Round(baseX + deltaX);
                    int absY = (int)Math.Round(baseY + deltaY);
                    
                    vertices.Add((absX, absY));
                }

                int ax = vertices[0].x;
                int ay = vertices[0].y;
                int bx = vertices[1].x;
                int by = vertices[1].y;
                int cx = vertices[2].x;
                int cy = vertices[2].y;
                int doorX = vertices[3].x;
                int doorY = vertices[3].y;

                string xml = $@"<door id=""{doorId}"" name=""{meshName}"" hp=""1"" pdef=""100000"" mdef=""1"">
    <pos x=""{(int)baseX}"" y=""{(int)baseY}"" z=""{(int)baseZ}""/>
    <shape ax=""{ax}"" ay=""{ay}"" bx=""{bx}"" by=""{by}"" cx=""{cx}"" cy=""{cy}"" dx=""{doorX}"" dy=""{doorY}"" minz=""{minZ}"" maxz=""{maxZ}""/>
    <set name=""opened"" value=""false""/>
    <set name=""unlockable"" value=""false""/>
    <set name=""key"" value=""0""/>
    <set name=""show_hp"" value=""false""/>
    <set name=""close_time"" value=""60""/>
</door>";

                return xml;
            }
            catch (Exception ex)
            {
                return $"Erro durante a conversão: {ex.Message}";
            }
        }
    }
} 