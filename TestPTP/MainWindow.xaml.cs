using GPeerToPeer.Client;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TestPTP
{
    public partial class MainWindow : Window
    {
        private readonly IPTPClient client;
        public MainWindow()
        {
            InitializeComponent();
            client = new PTPClient("194.61.3.168", 22345, 22345);
            client.ReceiveMessageFrom += Client_ReceiveMessageFrom;
            HistoryTB.Text += client.selfNode.Key + "\n";
            Closed += MainWindow_Closed;
            Task.Run(client.Work);
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            client.Close();
        }

        private void WriteToEndHistory(string str)
        {
            HistoryTB.Text += str + "\n";
            HistoryTB.ScrollToEnd();
        }

        private void Client_ReceiveMessageFrom(byte[] message, PTPNode node)
        {
            HistoryTB.Dispatcher.Invoke(() => {
                WriteToEndHistory(string.Format("{0}: {1}", node.Key, Encoding.UTF8.GetString(message)));
            });
        }

        private async void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
                try
                {
                    PTPNode node = new PTPNode(NodeKeyTB.Text);
                    string message = MessageTB.Text;
                    if (await client.SendMessageToAsync(node, Encoding.UTF8.GetBytes(message)))
                    {
                        WriteToEndHistory(string.Format("Отправлено: {0}", message));
                        MessageTB.Text = "";
                    }
                    else
                        WriteToEndHistory(string.Format("Ошибка отправки сообщения узлу: {0}", node.Key));
                }
                catch (Exception ex)
                {
                    WriteToEndHistory(string.Format("Ошибка: {0}", ex));
                }
                button.IsEnabled = true;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
                try
                {
                    string nodeKey = NodeKeyTB.Text;
                    bool isConnect = await client.ReachConnectionAsync(nodeKey);
                    if (isConnect) WriteToEndHistory(string.Format("Подключение к {0} успешно", nodeKey));
                    else WriteToEndHistory(string.Format("Ошибка подключения к {0}", nodeKey));
                }
                catch(Exception ex)
                {
                    WriteToEndHistory(string.Format("Ошибка: {0}", ex));
                }
                button.IsEnabled = true;
            }
        }
    }
}
