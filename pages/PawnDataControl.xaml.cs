using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace L2Toolkit.pages
{
    public partial class PawnDataControl : UserControl
    {
        private readonly DispatcherTimer _dispatcherTimer = new();

        public PawnDataControl()
        {
            InitializeComponent();
            _dispatcherTimer.Interval = TimeSpan.FromSeconds(3);
            _dispatcherTimer.Tick += (s, e) =>
            {
                CopiadoTextBlock.Visibility = Visibility.Collapsed;
                _dispatcherTimer.Stop();
            };
            GenerateButton.Click += GenerateButton_Click;
            CopiarButton.Click += CopiarButton_Click;
        }

        private void CopiarButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(OutputTextBox.Text))
                return;

            Clipboard.SetText(OutputTextBox.Text);
            CopiadoTextBlock.Visibility = Visibility.Visible;
            _dispatcherTimer.Stop();
            _dispatcherTimer.Start();
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            GeneratePawnAnimData();
        }

        private void GeneratePawnAnimData()
        {
            string name = PawnNameTextBox.Text.Trim();
            List<(int index, int variant, string anim)> textureParams = new List<(int, int, string)>();

            CollectAnimationEntries(textureParams);

            string output = GeneratePawnAnimDataText(name, textureParams);

            OutputTextBox.Text = output;
        }

        private void CollectAnimationEntries(List<(int index, int variant, string anim)> textureParams)
        {
            int[] indices = { 1, 2, 8, 9, 20, 21, 22, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 60, 62, 92 };

            foreach (int idx in indices)
            {
                TextBox entry = FindName($"Entry_{idx}") as TextBox;
                if (entry != null)
                {
                    string animText = entry.Text.Trim();
                    if (!string.IsNullOrEmpty(animText))
                    {
                        string[] animations = animText.Split(';')
                            .Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a))
                            .ToArray();

                        for (int variant = 0; variant < animations.Length; variant++)
                        {
                            textureParams.Add((idx, variant, animations[variant]));
                        }
                    }
                }
            }
        }

        private string GeneratePawnAnimDataText(string name, List<(int index, int variant, string anim)> textureParams)
        {
            StringBuilder result = new StringBuilder();

            result.Append($"pawnanim_data_begin\tname=[{name}]");
            result.Append($"\ttexture_params={FormatList(textureParams)}");
            result.Append("\tint_params={}");
            result.Append("\tint_params2={}");
            result.Append("\tfloat_params={}");
            result.Append("\tpawnanim_data_end");

            return result.ToString();
        }

        private string FormatList(List<(int index, int variant, string anim)> parameters)
        {
            if (parameters.Count == 0)
            {
                return "{}";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append('{');

            for (int i = 0; i < parameters.Count; i++)
            {
                var (index, variant, anim) = parameters[i];
                sb.Append($"{{{index};{variant};[{anim}]}}");

                if (i < parameters.Count - 1)
                {
                    sb.Append(';');
                }
            }

            sb.Append('}');
            return sb.ToString();
        }
    }
} 