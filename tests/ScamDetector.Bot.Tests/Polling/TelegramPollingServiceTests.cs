using Microsoft.Extensions.Logging.Abstractions;
using ScamDetector.Bot.Messaging;
using ScamDetector.Bot.Polling;
using ScamDetector.Bot.Telegram;
using ScamDetector.Bot.Tests.Fakes;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;

namespace ScamDetector.Bot.Tests.Polling;

public sealed class TelegramPollingServiceTests
{
    private static TelegramUpdate TextUpdate(long updateId, string? text, string? caption = null) =>
        new(updateId, new TelegramMessage(updateId * 10, new TelegramChat(42), text, caption));

    private static TelegramPollingService CreateService(FakeTelegramClient client)
    {
        var classifier = new FakeMessageClassifier(Result.Success(new ClassificationResult(MessageLabel.Normal, 0.9, [])));
        return new TelegramPollingService(
            client,
            new BotReplyBuilder(classifier),
            new TelegramOptions { BotToken = "fake-token" },
            NullLogger<TelegramPollingService>.Instance);
    }

    [Fact]
    public async Task PollOnceAsync_MessageUpdates_RepliesToEachMessage()
    {
        var client = new FakeTelegramClient([TextUpdate(1, "mot"), TextUpdate(2, "hai")]);
        var service = CreateService(client);

        await service.PollOnceAsync(CancellationToken.None);

        Assert.Equal(2, client.SentReplies.Count);
        Assert.Equal(10, client.SentReplies[0].Message.MessageId);
        Assert.Equal(20, client.SentReplies[1].Message.MessageId);
        Assert.All(client.SentReplies, sent => Assert.StartsWith(BotReplies.NormalVerdict, sent.Text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PollOnceAsync_ReplyToOneChatFails_StillRepliesToFollowingMessages()
    {
        const long blockedChatId = 7;
        var blockedUpdate = new TelegramUpdate(1, new TelegramMessage(10, new TelegramChat(blockedChatId), "mot", null));
        var client = new FakeTelegramClient([blockedUpdate, TextUpdate(2, "hai")]);
        client.FailingChatIds.Add(blockedChatId);
        var service = CreateService(client);

        await service.PollOnceAsync(CancellationToken.None);
        await service.PollOnceAsync(CancellationToken.None);

        Assert.Equal(20, Assert.Single(client.SentReplies).Message.MessageId);
        Assert.Equal([0L, 3L], client.RequestedOffsets);
    }

    [Fact]
    public async Task PollOnceAsync_UpdateWithoutMessage_SkipsReply()
    {
        var client = new FakeTelegramClient([new TelegramUpdate(1, null), TextUpdate(2, "hai")]);
        var service = CreateService(client);

        await service.PollOnceAsync(CancellationToken.None);

        var sent = Assert.Single(client.SentReplies);
        Assert.Equal(20, sent.Message.MessageId);
    }

    [Fact]
    public async Task PollOnceAsync_SecondCall_UsesMaxUpdateIdPlusOneAsOffset()
    {
        var client = new FakeTelegramClient([TextUpdate(5, "a"), TextUpdate(9, "b"), TextUpdate(7, "c")]);
        var service = CreateService(client);

        await service.PollOnceAsync(CancellationToken.None);
        await service.PollOnceAsync(CancellationToken.None);

        Assert.Equal([0L, 10L], client.RequestedOffsets);
    }

    [Fact]
    public async Task PollOnceAsync_OnlyUpdatesWithoutMessage_StillAdvancesOffset()
    {
        var client = new FakeTelegramClient([new TelegramUpdate(3, null)]);
        var service = CreateService(client);

        await service.PollOnceAsync(CancellationToken.None);
        await service.PollOnceAsync(CancellationToken.None);

        Assert.Equal([0L, 4L], client.RequestedOffsets);
    }

    [Fact]
    public async Task PollOnceAsync_EmptyBatchAfterUpdates_KeepsOffset()
    {
        var client = new FakeTelegramClient([TextUpdate(5, "a")], []);
        var service = CreateService(client);

        await service.PollOnceAsync(CancellationToken.None);
        await service.PollOnceAsync(CancellationToken.None);
        await service.PollOnceAsync(CancellationToken.None);

        Assert.Equal([0L, 6L, 6L], client.RequestedOffsets);
    }

    [Fact]
    public async Task PollOnceAsync_StickerWithoutTextOrCaption_DoesNotReply()
    {
        var client = new FakeTelegramClient([TextUpdate(1, null)]);
        var service = CreateService(client);

        await service.PollOnceAsync(CancellationToken.None);

        Assert.Empty(client.SentReplies);
    }

    [Fact]
    public async Task PollOnceAsync_StartCommand_RepliesWelcome()
    {
        var client = new FakeTelegramClient([TextUpdate(1, "/start")]);
        var service = CreateService(client);

        await service.PollOnceAsync(CancellationToken.None);

        var sent = Assert.Single(client.SentReplies);
        Assert.Equal(BotReplies.Welcome, sent.Text);
    }

    [Fact]
    public async Task PollOnceAsync_ClassifierThrowsUnexpectedException_ContinuesWithNextUpdate()
    {
        var client = new FakeTelegramClient([TextUpdate(1, "mot"), TextUpdate(2, "/start")]);
        var classifier = new FakeMessageClassifier(new InvalidOperationException("model crashed"));
        var service = new TelegramPollingService(
            client,
            new BotReplyBuilder(classifier),
            new TelegramOptions { BotToken = "fake-token" },
            NullLogger<TelegramPollingService>.Instance);

        await service.PollOnceAsync(CancellationToken.None);

        var reply = Assert.Single(client.SentReplies);
        Assert.Equal(BotReplies.Welcome, reply.Text);
    }
}
