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
using Xamarin.Essentials;
using Xamarin.Forms;

namespace ChatNeat.ClientApp
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        private ChatServiceClient _chatService;

        public MainPage()
        {
            InitializeComponent();
            _chatService = ((App)Application.Current).ChatService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Guid userId = _chatService.UserId;
            Title = $"User ID: {userId.ToString("N")}";
            UsernameLabel.Text = $"Username: {_chatService.Username}";
            await _chatService.Initialize();
        }

        private async void GetGroups_Clicked(object sender, EventArgs e)
        {
            await UpdateGroups();
        }

        private async Task UpdateGroups()
        {
            var groups = await _chatService.GetGroups();
            GroupsList.ItemsSource = groups.ToList();
        }

        private void ChangeUsername_Clicked(object sender, EventArgs e)
        {
            void Submit_Clicked(object s, EventArgs args)
            {
                string text = InputPrompt.Text;
                _chatService.Username = text;
                UsernameLabel.Text = $"Username: {_chatService.Username}";
                InputPanel.IsVisible = false;
            }

            SubmitButton.Clicked += Submit_Clicked;
            InputPanel.IsVisible = true;
        }

        private void CreateGroup_Clicked(object sender, EventArgs e)
        {
            async void Submit_Clicked(object s, EventArgs args)
            {
                string text = InputPrompt.Text;
                InputPanel.IsVisible = false;
                Group newGroup = await _chatService.CreateGroup(text);
                if (newGroup != null)
                {
                    await UpdateGroups();
                }
            }

            SubmitButton.Clicked += Submit_Clicked;
            InputPanel.IsVisible = true;
        }

        private async void GroupsList_ItemsSelected(object sender, SelectedItemChangedEventArgs e)
        {
            Group selectedGroup = e.SelectedItem as Group;
            if (selectedGroup == null)
            {
                return;
            }

            bool success = await _chatService.JoinGroup(
                new User { Id = _chatService.UserId, Name = _chatService.Username },
                selectedGroup.Id);

            if (!success)
            {
                return;
            }

            await this.Navigation.PushAsync(new ChatRoomPage(selectedGroup));
        }

        private async void CopyUserId_Clicked(object sender, EventArgs e)
        {
            await Clipboard.SetTextAsync(_chatService.UserId.ToString("N"));
        }

        private async void GroupDelete_Clicked(object sender, EventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            Group group = menuItem?.BindingContext as Group;
            if (group != null)
            {
                bool success = await _chatService.DeleteGroup(group.Id);
                if (success)
                {
                    await UpdateGroups();
                }
            }
        }
    }
}
