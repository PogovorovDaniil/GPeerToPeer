﻿using GPeerToPeer;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TestPTP
{
    public partial class MainWindow : Window
    {
        IPTPClient client;
        bool isActive = true;
        public MainWindow()
        {
            InitializeComponent();
            client = new PTPClient("194.61.3.168", 22345, 22345);
            client.ReceiveMessageFrom += Client_ReceiveMessageFrom;
            HistoryTB.Text += client.selfNode.Key + "\n";
            Closed += MainWindow_Closed;
            new Thread(() =>
            {
                while (isActive)
                {
                    client.Work();
                }
            }).Start();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            isActive = false;
        }

        private void Client_ReceiveMessageFrom(byte[] message, PTPNode node)
        {
            HistoryTB.Dispatcher.Invoke(() => {
                HistoryTB.Text += string.Format("{0}: {1}\n", node.Key, Encoding.UTF8.GetString(message));
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                PTPNode node = new PTPNode();
                string message = "";
                MessageTB.Dispatcher.Invoke(() =>
                {
                    node = new PTPNode(NodeKeyTB.Text);
                    message = MessageTB.Text;
                });
                if (client.SendMessageTo(node, Encoding.UTF8.GetBytes(message)))
                {
                    HistoryTB.Dispatcher.Invoke(() =>
                    {
                        HistoryTB.Text += string.Format("{0}: {1}\n", "Me", message);
                    });
                }
                else
                {
                    HistoryTB.Dispatcher.Invoke(() =>
                    {
                        HistoryTB.Text += string.Format("Ошибка отправки сообщения узлу: {0}\n", node.Key);
                    });
                }
            }).Start();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                string nodeKey = NodeKeyTB.Text;
                bool isConnect = client.ReachConnection(nodeKey);              
                HistoryTB.Dispatcher.Invoke(() =>
                {
                    if (isConnect) HistoryTB.Text += string.Format("Подключение к {0} успешно\n", nodeKey);
                    else HistoryTB.Text += string.Format("Ошибка подключения к {0}\n", nodeKey);
                });
            }).Start();
        }
    }
}
