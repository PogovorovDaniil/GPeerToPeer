using GPeerToPeer;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TestPTP
{
    public partial class MainWindow : Window
    {
        IPTPClient client;
        public MainWindow()
        {
            InitializeComponent();
            client = new PTPClient("194.61.3.168", 22345, 22345);
            client.ReceiveMessageFrom += Client_ReceiveMessageFrom;
            HistoryTB.Text += client.selfNode.Key + "\n";

            Task.Run(client.Work);
        }

        private void Client_ReceiveMessageFrom(byte[] message, PTPNode node)
        {
            HistoryTB.Dispatcher.Invoke(() => {
                HistoryTB.Text += string.Format("{0}: {1}\n", node.Key, Encoding.UTF8.GetString(message));
            });
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
                try
                {
                    PTPNode node = new PTPNode(NodeKeyTB.Text);
                    string message = MessageTB.Text;
                    if (await client.SendMessageToAsync(node, Encoding.UTF8.GetBytes(message))) 
                        HistoryTB.Text += string.Format("{0}: {1}\n", "Me", message);
                    else 
                        HistoryTB.Text += string.Format("Ошибка отправки сообщения узлу: {0}\n", node.Key);
                }
                catch (Exception ex)
                {
                    HistoryTB.Text += string.Format("Ошибка: {0}\n", ex);
                }
                button.IsEnabled = true;
            }
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
                try
                {
                    string nodeKey = NodeKeyTB.Text;
                    bool isConnect = await client.ReachConnectionAsync(nodeKey);
                    if (isConnect) HistoryTB.Text += string.Format("Подключение к {0} успешно\n", nodeKey);
                    else HistoryTB.Text += string.Format("Ошибка подключения к {0}\n", nodeKey);
                }
                catch(Exception ex)
                {
                    HistoryTB.Text += string.Format("Ошибка: {0}\n", ex);
                }
                button.IsEnabled = true;
            }
        }
    }
}
