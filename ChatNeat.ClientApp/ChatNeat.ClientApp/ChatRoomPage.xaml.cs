using ChatNeat.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ChatNeat.ClientApp
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ChatRoomPage : ContentPage
    {
        private readonly ChatServiceClient _chatService;
        private Group _group;

        private ObservableCollection<Message> _messages;

        public ChatRoomPage(Group group)
        {
            InitializeComponent();

            _group = group;
            _chatService = ((App)Application.Current).ChatService;
            Title = $"{_group.Name}:{_group.Id}";
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _chatService.MessageReceived += ChatService_MessageReceived;

            // Get messages history
            var messages = await _chatService.GetMessages(_group.Id);
            _messages = new ObservableCollection<Message>(messages.OrderBy(x => x.Timestamp));
            MessagesList.ItemsSource = _messages;

            // Get users
            var users = await _chatService.GetUsers(_group.Id);
            UsersList.ItemsSource = users.ToList();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _chatService.MessageReceived -= ChatService_MessageReceived;
        }

        private async void SubmitButton_Clicked(object sender, EventArgs e)
        {
            string message = Editor.Text;
            Editor.Text = "";
            await SendMessage(message);
        }

        private async Task SendMessage(string text)
        {
            await _chatService.SendMessage(new Message
            {
                Contents = text,
                GroupId = _group.Id,
                SenderId = _chatService.UserId,
                SenderName = _chatService.Username
            });
        }

        private void ChatService_MessageReceived(object sender, Message e)
        {
            if (e.GroupId == _group.Id)
            {
                MainThread.BeginInvokeOnMainThread(() => _messages.Add(e));
            }
        }

        private async void UserKick_Clicked(object sender, EventArgs e)
        {
            MenuItem item = sender as MenuItem;
            User user = item.BindingContext as User;
            if (user != null)
            {
                await _chatService.LeaveGroup(_group.Id, user.Id);
                var users = await _chatService.GetUsers(_group.Id);
                UsersList.ItemsSource = users.ToList();
            }
        }
    }
}