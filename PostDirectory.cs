using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.Converters;
using YamlDotNet.Serialization.NamingConventions;

namespace SeBlog
{
    public class PostDirectory
    {
        private readonly ILogger<PostDirectory> _logger;

        public PostDirectory(HttpClient httpClient, ILocalStorageService localStorage, ILogger<PostDirectory> logger)
        {
            HttpClient = httpClient;
            LocalStorage = localStorage;
            _logger = logger;
        }

        private List<Post> Posts { get; } = new List<Post>();
        private HttpClient HttpClient { get; }
        private ILocalStorageService LocalStorage { get; }

        public async Task<List<Post>> GetPosts()
        {
            if (Posts.Count > 0) return Posts.ToList();
            await InitializePosts();

            return Posts.ToList();
        }

        private async Task InitializePosts()
        {
            // checkout https://developer.github.com/v3/repos/contents/#custom-media-types with strategy to replace it
            var orderedPosts = PostLists.TitleToFile.OrderByDescending(title => title.Key);
            foreach (var (postTitle, fileUrl) in orderedPosts) Posts.Add(await LoadPostByTitle(postTitle, fileUrl));
        }

        private async Task<Post> LoadPostByTitle(string postKey, string fileUrl)
        {
#if DEBUG
            // disable client side caching while developing/writing
            return await DownloadPost(postKey, fileUrl);
            
#elif !DEBUG

            if (await LocalStorage.ContainKeyAsync(postKey))
            {
                _logger.LogInformation("Loading cached post {postKey}", postKey);
                var post = await LocalStorage.GetItemAsync<Post>(postKey);
                return post;
            }

            return await DownloadPost(postKey, fileUrl);
#endif

        }

        private async Task<Post> DownloadPost(string postKey, string fileUrl)
        {
            _logger.LogInformation("Downloading post {postKey}", postKey);
            var content = await GetContentFromUrl(fileUrl);
            var post = ParseYamlFront(content);
            _logger.LogDebug("Parsed content {markdown}", JsonSerializer.Serialize(post));
            await LocalStorage.SetItemAsync(postKey, post);
            return post;
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
            return (httpResponse.IsSuccessStatusCode
                ? await httpResponse.Content.ReadAsStringAsync()
                : httpResponse.ReasonPhrase)!;
        }

        public async Task<Post?> ByKey(string key)
        {
            _logger.LogInformation("trying to get post {postKey}", key);
            if (!PostLists.TitleToFile.ContainsKey(key)) return null;

            return await LoadPostByTitle(key, PostLists.TitleToFile[key]);
        }
    }
}