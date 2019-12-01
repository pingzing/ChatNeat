using ChatNeat.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;

namespace ChatNeat.ClientApp
{
    public class ChatServiceClient
    {
        private HubConnection _connection;
        private readonly HttpClient _httpClient;

        public Guid UserId { get; set; }
        public string Username { get; set; }

        public event EventHandler<Message> MessageReceived;

        public ChatServiceClient(string baseAddress)
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(baseAddress);
            Username = GetRandomUsername();
            UserId = Guid.NewGuid();
        }

        public async Task Initialize()
        {
            if (_connection != null)
            {
                await _connection?.StopAsync();
            }

            _connection = new HubConnectionBuilder()
               .WithUrl($"{_httpClient.BaseAddress}api/messaging", options =>
               {
                   options.Headers.Add("X-User-Id", UserId.ToString("N"));
               })
               .Build();

            _connection.On<Message>(SignalRMessages.NewMessage, OnMessageReceived);
            _connection.Closed += Connection_Closed;

            await Connect();
        }

        private async Task Connect()
        {
            try
            {
                await _connection.StartAsync();
                // Reconnect this user to their signalr users:
                bool reconnectSuccess = await Reconnect(UserId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Connect failed. Exception details:" + ex);
            }
        }

        private Task Connection_Closed(Exception arg)
        {
            Debug.WriteLine($"SignalR connection closed: {arg}. Reconnecting...");
            _connection.Remove(SignalRMessages.NewMessage);
            return Connect();
        }

        private void OnMessageReceived(Message message)
        {
            MessageReceived?.Invoke(this, message);
        }

        public async Task<IEnumerable<Group>> GetGroups()
        {
            var response = await _httpClient.GetAsync("api/groups");
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from GET api/groups. Got {response.StatusCode}");
                return null;
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Group[]>(responseJson);
        }

        public async Task<Group> CreateGroup(string groupName)
        {
            var response = await PostAsJsonAsync("api/group", new CreateGroupRequest { Name = groupName });
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from POST api/group. Got {response.StatusCode}");
                return null;
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Group>(responseJson);
        }

        public async Task<bool> DeleteGroup(Guid groupId)
        {
            var response = await _httpClient.DeleteAsync($"api/group/{groupId}");
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from DELETE api/group/{groupId}. Got {response.StatusCode}");
                return false;
            }

            return true;
        }

        public async Task<bool> JoinGroup(User user, Guid groupId)
        {
            var response = await PostAsJsonAsync($"api/group/{groupId}/join", user);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from POST api/group/{groupId}. Got {response.StatusCode}");
                return false;
            }

            return true;
        }

        public async Task<bool> LeaveGroup(Guid groupId, Guid userId)
        {
            var response = await _httpClient.PostAsync($"api/group/{groupId}/leave/{userId}", new StringContent(""));
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from POST api/group/{groupId}/leave/{userId}. Got {response.StatusCode}");
                return false;
            }

            return true;
        }

        public async Task<IEnumerable<User>> GetUsers(Guid groupId)
        {
            var response = await _httpClient.GetAsync($"api/group/{groupId}/users");
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from GET api/group/{groupId}/users. Got {response.StatusCode}");
                return null;
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<User[]>(jsonResponse);
        }

        public async Task<Message> SendMessage(Message message)
        {
            var response = await PostAsJsonAsync($"api/group/{message.GroupId}/message", message);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from POST api/group/{message.GroupId}/message. Got {response.StatusCode}");
                return null;
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Message>(jsonResponse);
        }

        public async Task<IEnumerable<Message>> GetMessages(Guid groupId)
        {
            var response = await _httpClient.GetAsync($"api/group/{groupId}/messages");
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from GET api/group/{groupId}/message. Got {response.StatusCode}");
                return null;
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<Message>>(jsonResponse);
        }

        public async Task<IEnumerable<Group>> GetUserGroups(Guid userId)
        {
            var response = await _httpClient.GetAsync($"api/user/{userId}/groups");
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from GET api/user/{userId}/groups. Got {response.StatusCode}");
                return null;
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IEnumerable<Group>>(jsonResponse);
        }

        public async Task<bool> Reconnect(Guid userId)
        {
            var response = await _httpClient.PostAsync($"api/messaging/reconnect/{userId}", new StringContent(""));
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Got failure from POST api/messaging/reconnect/{userId}. Got {response.StatusCode}");
                return false;
            }

            return true;
        }

        private async Task<HttpResponseMessage> PostAsJsonAsync<T>(string requestUri, T value)
        {
            var content = new ObjectContent<T>(value, new JsonMediaTypeFormatter());
            string contentString = await content.ReadAsStringAsync();
            return await _httpClient.PostAsync(requestUri, new StringContent(contentString, Encoding.UTF8, "application/json"));
        }

        Random rand = new Random();
        private string GetRandomUsername()
        {
            return $"Scrub{rand.Next(0, 9001)}";
        }
    }
}
