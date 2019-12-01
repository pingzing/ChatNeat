using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ChatNeat.ClientApp
{
    public partial class App : Application
    {
        public ChatServiceClient ChatService { get; set; }
        public NavigationPage NavigationPage { get; private set; }

        public App()
        {
            InitializeComponent();
            ChatService = new ChatServiceClient("https://chatneat.azurewebsites.net/");
            NavigationPage = new NavigationPage(new MainPage());
            MainPage = NavigationPage;
        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
