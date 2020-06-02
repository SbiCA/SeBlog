using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using SeBlog.Posts;

namespace SeBlog
{
    public class PostDirectory
    {
        public PostDirectory(HttpClient httpClient, ILocalStorageService localStorage)
        {
            HttpClient = httpClient;
            LocalStorage = localStorage;
        }

        private List<Post> Posts { get; } = new List<Post>();
        private HttpClient HttpClient { get; }
        private ILocalStorageService LocalStorage { get; }

        public async Task<List<Post>> GetPosts()
        {
            // TODO use local storage https://github.com/Blazored/LocalStorage
            if (Posts.Count > 0) return Posts;
            await InitializePosts();

            return Posts;
        }

        private async Task InitializePosts()
        {
            foreach (var postTitle in PostLists.List)
            {
                if (await LocalStorage.ContainKeyAsync(postTitle))
                {
                    Console.WriteLine($"Loading cached post {postTitle}");
                    var post = await LocalStorage.GetItemAsync<Post>(postTitle);
                    Posts.Add(post);
                }
                else
                {
                    Console.WriteLine($"Downloading post {postTitle}");
                    var content = await GetContentFromUrl(postTitle);
                    // TODO yaml parse
                    var yamlFront = content.Split(Environment.NewLine).Skip(1).Take(3).ToArray();
                    var post = new Post
                    {
                        // TODO store and cache locally
                        Content = content,
                        Date = DateTime.UtcNow,
                        Title = yamlFront[0],
                        ShortDescription = yamlFront[2]
                    };
                    await LocalStorage.SetItemAsync(postTitle, post);
                    Posts.Add(post);
                }
            }
        }

        private async Task<string> GetContentFromUrl(string path)
        {
            var httpResponse = await HttpClient.GetAsync(path);
            return httpResponse.IsSuccessStatusCode
                ? await httpResponse.Content.ReadAsStringAsync()
                : httpResponse.ReasonPhrase;
        }

        public async Task<Post> ByTitle(string title)
        {
            if (Posts.Count == 0)
            {
                await InitializePosts();
            }
            // var escapedTitle = Uri.EscapeUriString(title);
            // Console.WriteLine($"escaped : {escapedTitle}");
            Console.WriteLine($"{JsonSerializer.Serialize(Posts)}");
            var firstOrDefault = Posts.FirstOrDefault(p => p.Title == title);
            Console.WriteLine($"post found {JsonSerializer.Serialize(firstOrDefault)}");
            return firstOrDefault;
        }
    }
}