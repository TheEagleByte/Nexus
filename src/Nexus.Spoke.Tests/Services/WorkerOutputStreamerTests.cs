using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class WorkerOutputStreamerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IHubConnectionService> _hubConnectionMock;
    private readonly Mock<IJobArtifactService> _jobArtifactsMock;
    private readonly WorkerOutputStreamer _sut;
    private readonly Guid _spokeId = Guid.NewGuid();

    public WorkerOutputStreamerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _hubConnectionMock = new Mock<IHubConnectionService>();
        _jobArtifactsMock = new Mock<IJobArtifactService>();
        _hubConnectionMock.Setup(h => h.SpokeId).Returns(_spokeId);

        _sut = new WorkerOutputStreamer(
            _dockerServiceMock.Object,
            _hubConnectionMock.Object,
            _jobArtifactsMock.Object,
            NullLogger<WorkerOutputStreamer>.Instance);
    }

    private static async IAsyncEnumerable<(string Content, string StreamType)> CreateChunks(
        params (string Content, string StreamType)[] chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return chunk;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task StreamAsync_ForwardsChunksToHub()
    {
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var containerId = "abc123def456";

        _dockerServiceMock.Setup(m => m.StreamOutputAsync(containerId, It.IsAny<CancellationToken>()))
            .Returns(CreateChunks(("line 1\n", "stdout"), ("line 2\n", "stderr")));

        await _sut.StreamAsync(jobId, projectId, "TEST-1", containerId);

        _hubConnectionMock.Verify(m => m.SendAsync(
            "StreamJobOutput",
            It.Is<JobOutputChunk>(c => c.JobId == jobId && c.Sequence == 1 && c.Content == "line 1\n" && c.StreamType == "stdout"),
            It.IsAny<CancellationToken>()), Times.Once);

        _hubConnectionMock.Verify(m => m.SendAsync(
            "StreamJobOutput",
            It.Is<JobOutputChunk>(c => c.JobId == jobId && c.Sequence == 2 && c.Content == "line 2\n" && c.StreamType == "stderr"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StreamAsync_PersistsOutputLocally()
    {
        var jobId = Guid.NewGuid();
        var containerId = "abc123def456";

        _dockerServiceMock.Setup(m => m.StreamOutputAsync(containerId, It.IsAny<CancellationToken>()))
            .Returns(CreateChunks(("output data\n", "stdout")));

        await _sut.StreamAsync(jobId, Guid.NewGuid(), "TEST-1", containerId);

        _jobArtifactsMock.Verify(m => m.AppendOutputAsync("TEST-1", jobId, "output data\n"), Times.Once);
    }

    [Fact]
    public async Task StreamAsync_IncrementsSequenceNumber()
    {
        var jobId = Guid.NewGuid();
        var containerId = "abc123def456";

        _dockerServiceMock.Setup(m => m.StreamOutputAsync(containerId, It.IsAny<CancellationToken>()))
            .Returns(CreateChunks(("a\n", "stdout"), ("b\n", "stdout"), ("c\n", "stdout")));

        await _sut.StreamAsync(jobId, Guid.NewGuid(), "TEST-1", containerId);

        _hubConnectionMock.Verify(m => m.SendAsync(
            "StreamJobOutput",
            It.Is<JobOutputChunk>(c => c.Sequence == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        _hubConnectionMock.Verify(m => m.SendAsync(
            "StreamJobOutput",
            It.Is<JobOutputChunk>(c => c.Sequence == 2),
            It.IsAny<CancellationToken>()), Times.Once);
        _hubConnectionMock.Verify(m => m.SendAsync(
            "StreamJobOutput",
            It.Is<JobOutputChunk>(c => c.Sequence == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StreamAsync_HandlesEmptyStream()
    {
        var containerId = "abc123def456";

        _dockerServiceMock.Setup(m => m.StreamOutputAsync(containerId, It.IsAny<CancellationToken>()))
            .Returns(CreateChunks());

        await _sut.StreamAsync(Guid.NewGuid(), Guid.NewGuid(), "TEST-1", containerId);

        _hubConnectionMock.Verify(m => m.SendAsync(
            "StreamJobOutput",
            It.IsAny<JobOutputChunk>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StreamAsync_ContinuesWhenSingleChunkFails()
    {
        var jobId = Guid.NewGuid();
        var containerId = "abc123def456";

        _dockerServiceMock.Setup(m => m.StreamOutputAsync(containerId, It.IsAny<CancellationToken>()))
            .Returns(CreateChunks(("a\n", "stdout"), ("b\n", "stdout")));

        // First send fails, second succeeds
        var callCount = 0;
        _hubConnectionMock.Setup(m => m.SendAsync(
                "StreamJobOutput", It.IsAny<JobOutputChunk>(), It.IsAny<CancellationToken>()))
            .Returns((string _, JobOutputChunk _, CancellationToken _) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("Send failed");
                return Task.CompletedTask;
            });

        // Should not throw
        await _sut.StreamAsync(jobId, Guid.NewGuid(), "TEST-1", containerId);

        // Both chunks should still be persisted locally
        _jobArtifactsMock.Verify(m => m.AppendOutputAsync("TEST-1", jobId, It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task StreamAsync_IncludesSpokeIdInChunks()
    {
        var containerId = "abc123def456";

        _dockerServiceMock.Setup(m => m.StreamOutputAsync(containerId, It.IsAny<CancellationToken>()))
            .Returns(CreateChunks(("data\n", "stdout")));

        await _sut.StreamAsync(Guid.NewGuid(), Guid.NewGuid(), "TEST-1", containerId);

        _hubConnectionMock.Verify(m => m.SendAsync(
            "StreamJobOutput",
            It.Is<JobOutputChunk>(c => c.SpokeId == _spokeId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
