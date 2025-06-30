using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace L2Toolkit
{
    public partial class SpawnManager : UserControl
    {
        public SpawnManager()
        {
            InitializeComponent();
            
            CreateSpawnsButton.Click += CreateSpawnsButton_Click;
            CopyResultButton.Click += CopyResultButton_Click;
        }
        
        private void CreateSpawnsButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessSpawns();
        }
        
        private void CopyResultButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ResultTextBox.Text))
            {
                Clipboard.SetText(ResultTextBox.Text);
                MessageBox.Show("Resultado copiado para a área de transferência!", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void ProcessSpawns()
        {
            try
            {
                string idsText = NPCIdsTextBox.Text.Trim();
                if (string.IsNullOrEmpty(idsText))
                {
                    MessageBox.Show("Por favor, insira os IDs dos NPCs.", "Entrada inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                string[] npcIds = idsText.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (npcIds.Length == 0)
                {
                    MessageBox.Show("Nenhum ID válido encontrado.", "Entrada inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                string originalSpawns = OriginalSpawnTextBox.Text;
                if (string.IsNullOrEmpty(originalSpawns))
                {
                    MessageBox.Show("Por favor, insira os dados originais de spawn.", "Entrada inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                int idIndex = 0;
                string processedContent = ProcessNpcLines(originalSpawns, npcIds, ref idIndex);
                
                ResultTextBox.Text = processedContent;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro ao processar os spawns: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private string ProcessNpcLines(string content, string[] npcIds, ref int idIndex)
        {
            StringBuilder result = new StringBuilder();
            
            string[] lines = content.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                
                if (trimmedLine.Contains("<npc id="))
                {
                    int currentIndex = idIndex;
                    string newLine = Regex.Replace(line.Trim(), "id=\"([^\"]+)\"", match => $"id=\"{npcIds[currentIndex]}\"");
                    
                    result.AppendLine(newLine);
                    
                    idIndex = (idIndex + 1) % npcIds.Length;
                }
            }
            
            return result.ToString().TrimEnd();
        }
    }
} 