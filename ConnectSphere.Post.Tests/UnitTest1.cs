using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MassTransit;
using ConnectSphere.Post.Services;
using ConnectSphere.Post.Data;
using ConnectSphere.Post.DTOs;

namespace ConnectSphere.Post.Tests
{
    [TestFixture]
    public class PostServiceTests
    {
        private SqliteConnection _connection;
        private PostDbContext _db;
        private Mock<IPublishEndpoint> _busMock;
        private Mock<ILogger<PostService>> _loggerMock;
        private PostService _service;

        [SetUp]
        public void Setup()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<PostDbContext>()
                .UseSqlite(_connection)
                .Options;

            _db = new PostDbContext(options);
            _db.Database.EnsureCreated();

            _busMock = new Mock<IPublishEndpoint>();
            _loggerMock = new Mock<ILogger<PostService>>();

            _service = new PostService(
                _db,
                _busMock.Object,
                _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _db.Dispose();
            _connection.Close();
        }

        // ==========================================
        // Create Post
        // ==========================================
      
        [Test]
        public async Task CreatePostAsync_ShouldCreatePost()
        {
            var result = await _service.CreatePostAsync(1,
                new CreatePostRequest
                {
                    Content = " Hello ",
                    Visibility = "public",
                    Hashtags = "dotnet,csharp"
                });

            Assert.That(result.PostId, Is.GreaterThan(0));
            Assert.That(result.Content, Is.EqualTo("Hello"));
            Assert.That(result.Visibility, Is.EqualTo("PUBLIC"));
        }

        

        // ==========================================
        // Get By Id
        // ==========================================

        [Test]
        public async Task GetPostByIdAsync_ShouldReturnPost()
        {
            var post = new Models.Post
            {
                UserId = 1,
                Content = "Hello",
                Visibility = "PUBLIC"
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            var result = await _service.GetPostByIdAsync(post.PostId);

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task GetPostByIdAsync_ShouldReturnNull_WhenPrivate()
        {
            var post = new Models.Post
            {
                UserId = 1,
                Content = "Hello",
                Visibility = "PRIVATE"
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            var result = await _service.GetPostByIdAsync(post.PostId, 2);

            Assert.That(result, Is.Null);
        }

        // ==========================================
        // Update
        // ==========================================

        [Test]
        public async Task UpdatePostAsync_ShouldUpdate()
        {
            var post = new Models.Post
            {
                UserId = 1,
                Content = "Old",
                Visibility = "PUBLIC"
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            var result = await _service.UpdatePostAsync(post.PostId, 1,
                new UpdatePostRequest
                {
                    Content = "New Text"
                });

            Assert.That(result.Content, Is.EqualTo("New Text"));
        }

        [Test]
        public void UpdatePostAsync_ShouldThrow_WhenWrongUser()
        {
            var post = new Models.Post
            {
                UserId = 1,
                Content = "Old",
                Visibility = "PUBLIC"
            };

            _db.Posts.Add(post);
            _db.SaveChanges();

            Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _service.UpdatePostAsync(
                    post.PostId,
                    2,
                    new UpdatePostRequest()));
        }

        // ==========================================
        // Delete
        // ==========================================

        [Test]
        public async Task DeletePostAsync_ShouldSoftDelete()
        {
            var post = new Models.Post
            {
                UserId = 1,
                Content = "Delete",
                Visibility = "PUBLIC"
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            await _service.DeletePostAsync(post.PostId, 1);

            _db.ChangeTracker.Clear();

            var updated = await _db.Posts.FirstAsync();

            Assert.That(updated.IsDeleted, Is.True);
        }

        // ==========================================
        // Search
        // ==========================================

        [Test]
        public async Task SearchPostsAsync_ShouldReturnMatch()
        {
            _db.Posts.Add(new Models.Post
            {
                UserId = 1,
                Content = "I love dotnet",
                Visibility = "PUBLIC"
            });

            await _db.SaveChangesAsync();

            var result = await _service.SearchPostsAsync("dotnet");

            Assert.That(result.Count, Is.EqualTo(1));
        }

        // ==========================================
        // Hashtag
        // ==========================================

        [Test]
        public async Task GetByHashtagAsync_ShouldReturnMatch()
        {
            _db.Posts.Add(new Models.Post
            {
                UserId = 1,
                Content = "Hello",
                Visibility = "PUBLIC",
                Hashtags = "#dotnet,#csharp"
            });

            await _db.SaveChangesAsync();

            var result = await _service.GetByHashtagAsync("dotnet");

            Assert.That(result.Count, Is.EqualTo(1));
        }

        // ==========================================
        // Trending
        // ==========================================

        [Test]
        public async Task GetTrendingPostsAsync_ShouldReturnPosts()
        {
            _db.Posts.Add(new Models.Post
            {
                UserId = 1,
                Content = "Trend",
                Visibility = "PUBLIC",
                LikeCount = 5,
                CommentCount = 2,
                ShareCount = 1,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            var result = await _service.GetTrendingPostsAsync();

            Assert.That(result.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetTrendingHashtagsAsync_ShouldReturnTags()
        {
            _db.Posts.Add(new Models.Post
            {
                UserId = 1,
                Content = "Tags",
                Visibility = "PUBLIC",
                Hashtags = "#dotnet,#dotnet,#csharp",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            var result = await _service.GetTrendingHashtagsAsync();

            Assert.That(result.Count, Is.GreaterThan(0));
        }

        // ==========================================
        // Counters
        // ==========================================

        [Test]
        public async Task IncrementLikeCountAsync_ShouldIncrease()
        {
            var post = new Models.Post
            {
                UserId = 1,
                Content = "Test",
                Visibility = "PUBLIC"
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            await _service.IncrementLikeCountAsync(post.PostId);

            _db.ChangeTracker.Clear();

            var updated = await _db.Posts.FirstAsync();

            Assert.That(updated.LikeCount, Is.EqualTo(1));
        }
    }
}