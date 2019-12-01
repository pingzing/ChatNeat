using ChatNeat.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace ChatNeat.ClientApp
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        private HubConnection connection;
        private HttpClient _httpClient = new HttpClient();

        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:7071/api/", options =>
                {
                    options.Headers.Add("X-User-Id", "664e40d2-d0d8-448a-a59b-444225de3cba");
                })
                .Build();
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            connection.On<Message>(SignalRMessages.NewMessage, OnMessageReceived);

            try
            {
                await connection.StartAsync();
                // Reconnect this user to their signalr users:
                var response = await _httpClient.PostAsync("http://localhost:7071/api/reconnect", new StringContent("664e40d2-d0d8-448a-a59b-444225de3cba"));
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("Failed to reconnect SignalR groups.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void OnMessageReceived(Message arg1)
        {
            Debug.WriteLine($"{arg1}");
        }
    }
}
