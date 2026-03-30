using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RustDesktop.Core.Models;

namespace RustDesktop.App.Views;

public partial class ServerSelectionDialog : Window
{
    public ServerInfo? SelectedServer { get; private set; }

    public ServerSelectionDialog(List<ServerInfo> servers)
    {
        InitializeComponent();
        
        if (servers == null || servers.Count == 0)
        {
            MessageBox.Show("No servers available to select.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            DialogResult = false;
            return;
        }

        ServerListBox.ItemsSource = servers;
        ServerListBox.SelectedIndex = 0; // Select first server by default
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedServer = ServerListBox.SelectedItem as ServerInfo;
        
        if (SelectedServer == null)
        {
            MessageBox.Show("Please select a server.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
