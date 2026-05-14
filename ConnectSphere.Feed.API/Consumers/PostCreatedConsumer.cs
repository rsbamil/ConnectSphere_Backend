using MassTransit;
using ConnectSphere.Feed.Interfaces;
using ConnectSphere.Shared.Events;

namespace ConnectSphere.Feed.Consumers;

/// <summary>
/// Consumes PostCreatedEvent published by the Post service.
/// Runs asynchronously — the Post service does not wait for this to complete.
///
/// Flow:
///   1. PostCreatedEvent arrives via RabbitMQ FeedFanoutQueue
///   2. FeedService.AddPostToFollowerFeedsAsync inserts FeedItem for each follower
///   3. Redis cache is invalidated for each affected user
/// </summary>
public class PostCreatedConsumer : IConsumer<PostCreatedEvent>
{
    private readonly IFeedService _feedService;
    private readonly ILogger<PostCreatedConsumer> _logger;

    public PostCreatedConsumer(IFeedService feedService, ILogger<PostCreatedConsumer> logger)
    {
        _feedService = feedService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PostCreatedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "PostCreatedConsumer received PostId={PostId} AuthorId={AuthorId}",
            evt.PostId, evt.AuthorId);

        await _feedService.AddPostToFollowerFeedsAsync(evt.PostId, evt.AuthorId);
    }
}