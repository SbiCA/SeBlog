using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using SeBlog.Posts;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;
using YamlDotNet.Serialization.NamingConventions;

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
            foreach (var (postTitle, fileUrl) in PostLists.TitleToFile)
                Posts.Add(await LoadPostByTitle(postTitle, fileUrl));
        }

        private async Task<Post> LoadPostByTitle(string postKey, string fileUrl)
        {
            if (await LocalStorage.ContainKeyAsync(postKey))
            {
                Console.WriteLine($"Loading cached post {postKey}");
                var post = await LocalStorage.GetItemAsync<Post>(postKey);
                return post;
            }
            else
            {
                Console.WriteLine($"Downloading post {postKey}");
                var content = await GetContentFromUrl(fileUrl);
                // TODO yaml front parse
                // var yamlFront = content.Split(Environment.NewLine).Skip(1).Take(3).ToArray();
                // var post = new Post
                // {
                //     // TODO store and cache locally
                //     Content = content,
                //     Date = DateTime.UtcNow,
                //     Title = yamlFront[0],
                //     ShortDescription = yamlFront[2]
                // };
                var post = ParseYamlFront(content);
                Console.WriteLine($"Parsed content {JsonSerializer.Serialize(post)}");
                await LocalStorage.SetItemAsync(postKey, post);
                return post;
            }
        }

        private static Post ParseYamlFront(string markdown)
        {
            // Thanks https://markheath.net/post/markdown-html-yaml-front-matter
            var yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithTypeConverter(new DateTimeConverter())
                .Build();

            try
            {
                Post? post = null;
                using var input = new StringReader(markdown);
                var parser = new Parser(input);
                parser.Consume<StreamStart>();
                parser.Consume<DocumentStart>();
                post = yamlDeserializer.Deserialize<Post>(parser);
                parser.Consume<DocumentEnd>();
                // assign completed markdown
                post.Content = markdown;
                return post;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        private async Task<string> GetContentFromUrl(string path)
        {
            var httpResponse = await HttpClient.GetAsync(path);
            return httpResponse.IsSuccessStatusCode
                ? await httpResponse.Content.ReadAsStringAsync()
                : httpResponse.ReasonPhrase;
        }

        public async Task<Post?> ByKey(string key)
        {
            Console.WriteLine($"trying to get {key}");
            if (!PostLists.TitleToFile.ContainsKey(key)) return null;

            return await LoadPostByTitle(key, PostLists.TitleToFile[key]);
        }
    }
}