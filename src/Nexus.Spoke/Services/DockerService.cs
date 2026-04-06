using System.Runtime.CompilerServices;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;
using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public class DockerService : IDockerService
{
    private readonly DockerClient _client;
    private readonly SpokeConfiguration _config;
    private readonly ILogger<DockerService> _logger;
    private bool _imageVerified;

    public DockerService(IOptions<SpokeConfiguration> config, ILogger<DockerService> logger)
    {
        _config = config.Value;
        _logger = logger;
        _client = new DockerClientConfiguration().CreateClient();
    }

    public async Task EnsureImageAsync(CancellationToken cancellationToken = default)
    {
        if (_imageVerified)
            return;

        var imageName = _config.Docker.WorkerImage;
        _logger.LogInformation("Ensuring worker image {Image} is available", imageName);

        // Check if image exists locally
        var images = await _client.Images.ListImagesAsync(
            new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["reference"] = new Dictionary<string, bool> { [imageName] = true }
                }
            }, cancellationToken);

        if (images.Count > 0)
        {
            _logger.LogInformation("Worker image {Image} found locally", imageName);
            _imageVerified = true;
            return;
        }

        // Try pulling from registry if configured
        if (!string.IsNullOrEmpty(_config.Docker.Registry))
        {
            try
            {
                var fullImage = $"{_config.Docker.Registry}/{imageName}";
                _logger.LogInformation("Pulling worker image from {Registry}", fullImage);

                await _client.Images.CreateImageAsync(
                    new ImagesCreateParameters { FromImage = fullImage },
                    null,
                    new Progress<JSONMessage>(msg =>
                    {
                        if (!string.IsNullOrEmpty(msg.Status))
                            _logger.LogDebug("Pull: {Status}", msg.Status);
                    }),
                    cancellationToken);

                // Tag as local image name
                await _client.Images.TagImageAsync(fullImage,
                    new ImageTagParameters
                    {
                        RepositoryName = imageName.Split(':')[0],
                        Tag = imageName.Contains(':') ? imageName.Split(':')[1] : "latest"
                    }, cancellationToken);

                _imageVerified = true;
                _logger.LogInformation("Worker image pulled and tagged as {Image}", imageName);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to pull image from registry, falling back to local build");
            }
        }

        // Build from local Dockerfile
        await BuildImageLocallyAsync(cancellationToken);
        _imageVerified = true;
    }

    private async Task BuildImageLocallyAsync(CancellationToken cancellationToken)
    {
        var dockerfilePath = _config.Docker.WorkerDockerfilePath;
        var imageName = _config.Docker.WorkerImage;

        // Resolve the Dockerfile directory — the path is relative to the repo root
        var dockerfileDir = Path.GetDirectoryName(Path.GetFullPath(dockerfilePath))!;

        if (!File.Exists(Path.GetFullPath(dockerfilePath)))
        {
            throw new FileNotFoundException(
                $"Worker Dockerfile not found at {Path.GetFullPath(dockerfilePath)}. " +
                "Ensure the Dockerfile exists or configure a registry to pull from.");
        }

        _logger.LogInformation("Building worker image {Image} from {Dockerfile}", imageName, dockerfilePath);

        // Create a tar archive of the build context (the Dockerfile directory)
        using var tarStream = CreateTarArchive(dockerfileDir);

        var tag = imageName.Contains(':') ? imageName : $"{imageName}:latest";

        await _client.Images.BuildImageFromDockerfileAsync(
            new ImageBuildParameters
            {
                Tags = [tag],
                Dockerfile = Path.GetFileName(dockerfilePath),
                Remove = true,
                ForceRemove = true
            },
            tarStream,
            null,
            null,
            new Progress<JSONMessage>(msg =>
            {
                if (!string.IsNullOrEmpty(msg.Stream))
                    _logger.LogDebug("Build: {Stream}", msg.Stream.TrimEnd('\n'));
            }),
            cancellationToken);

        _logger.LogInformation("Worker image {Image} built successfully", imageName);
    }

    private static MemoryStream CreateTarArchive(string directory)
    {
        var stream = new MemoryStream();
        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(directory, file).Replace('\\', '/');
            var content = File.ReadAllBytes(file);

            // TAR header (512 bytes)
            var header = new byte[512];
            var nameBytes = Encoding.ASCII.GetBytes(relativePath);
            Array.Copy(nameBytes, header, Math.Min(nameBytes.Length, 100));

            // File mode (octal, ASCII)
            var mode = Encoding.ASCII.GetBytes("0000755\0");
            Array.Copy(mode, 0, header, 100, 8);

            // UID/GID
            var zero = Encoding.ASCII.GetBytes("0000000\0");
            Array.Copy(zero, 0, header, 108, 8); // uid
            Array.Copy(zero, 0, header, 116, 8); // gid

            // File size (octal, ASCII)
            var size = Encoding.ASCII.GetBytes(Convert.ToString(content.Length, 8).PadLeft(11, '0') + "\0");
            Array.Copy(size, 0, header, 124, 12);

            // Modification time
            var mtime = Encoding.ASCII.GetBytes(Convert.ToString(
                (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds, 8).PadLeft(11, '0') + "\0");
            Array.Copy(mtime, 0, header, 136, 12);

            // Type flag (regular file)
            header[156] = (byte)'0';

            // Checksum placeholder (spaces)
            for (int i = 148; i < 156; i++) header[i] = (byte)' ';

            // Calculate checksum
            int checksum = 0;
            for (int i = 0; i < 512; i++) checksum += header[i];
            var checksumStr = Encoding.ASCII.GetBytes(Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ");
            Array.Copy(checksumStr, 0, header, 148, 8);

            stream.Write(header, 0, 512);
            stream.Write(content, 0, content.Length);

            // Pad to 512-byte boundary
            var padding = 512 - (content.Length % 512);
            if (padding < 512)
                stream.Write(new byte[padding], 0, padding);
        }

        // Two empty 512-byte blocks to end the archive
        stream.Write(new byte[1024], 0, 1024);
        stream.Position = 0;
        return stream;
    }

    public async Task<string> LaunchWorkerAsync(WorkerLaunchRequest request, CancellationToken cancellationToken = default)
    {
        var containerName = $"nexus-worker-{request.JobId:N}";
        var dockerConfig = _config.Docker;

        _logger.LogInformation("Launching worker container {Name} for job {JobId}", containerName, request.JobId);

        var binds = new List<string>
        {
            $"{request.RepoPath}:/workspace/repo:rw",
            $"{request.PromptFilePath}:/workspace/prompt.md:ro",
            $"{request.OutputPath}:/workspace/output:rw"
        };

        if (!string.IsNullOrEmpty(request.SpokeSkillsPath) && Directory.Exists(request.SpokeSkillsPath))
            binds.Add($"{request.SpokeSkillsPath}:/workspace/skills/spoke:ro");

        if (!string.IsNullOrEmpty(request.ProjectSkillsPath) && Directory.Exists(request.ProjectSkillsPath))
            binds.Add($"{request.ProjectSkillsPath}:/workspace/skills/project:ro");

        var envVars = new List<string>
        {
            $"JOB_ID={request.JobId}",
            $"JOB_TYPE={request.JobType}",
            $"PROJECT_KEY={request.ProjectKey}"
        };

        // Pass ANTHROPIC_API_KEY from the spoke's environment
        // Network is disabled (none) so the key cannot be exfiltrated
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
            envVars.Add($"ANTHROPIC_API_KEY={apiKey}");

        var createParams = new CreateContainerParameters
        {
            Image = dockerConfig.WorkerImage,
            Name = containerName,
            Env = envVars,
            AttachStdout = true,
            AttachStderr = true,
            Tty = false,
            HostConfig = new HostConfig
            {
                Binds = binds,
                NetworkMode = dockerConfig.NetworkMode,
                CapDrop = ["ALL"],
                ReadonlyRootfs = dockerConfig.ReadOnlyRootFs,
                SecurityOpt = ["no-new-privileges"],
                Memory = dockerConfig.ResourceLimits.MemoryBytes,
                MemorySwap = dockerConfig.ResourceLimits.MemoryBytes, // Disable swap
                NanoCPUs = (long)dockerConfig.ResourceLimits.CpuCount * 1_000_000_000L,
                Tmpfs = new Dictionary<string, string>
                {
                    ["/tmp"] = "rw,noexec,nosuid,size=1g"
                }
            },
            User = dockerConfig.ContainerUser
        };

        var response = await _client.Containers.CreateContainerAsync(createParams, cancellationToken);

        if (response.Warnings?.Count > 0)
        {
            foreach (var warning in response.Warnings)
                _logger.LogWarning("Container creation warning: {Warning}", warning);
        }

        await _client.Containers.StartContainerAsync(response.ID, null, cancellationToken);

        _logger.LogInformation("Worker container {ContainerId} started for job {JobId}", response.ID[..12], request.JobId);
        return response.ID;
    }

    public async IAsyncEnumerable<(string Content, string StreamType)> StreamOutputAsync(
        string containerId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stream = await _client.Containers.AttachContainerAsync(containerId,
            tty: false,
            new ContainerAttachParameters
            {
                Stream = true,
                Stdout = true,
                Stderr = true
            }, cancellationToken);

        var buffer = new byte[8192];
        var lineBuffer = new StringBuilder();
        var currentStreamType = "stdout";

        // Docker multiplexed stream format:
        // [8-byte header][payload]
        // Header: byte[0] = stream type (1=stdout, 2=stderr), bytes[4-7] = payload length (big-endian)
        var headerBuffer = new byte[8];

        while (!cancellationToken.IsCancellationRequested)
        {
            // Read the 8-byte header
            int headerRead;
            try
            {
                headerRead = await ReadExactAsync(stream, headerBuffer, 8, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // Stream closed
                break;
            }

            if (headerRead < 8)
                break; // End of stream

            currentStreamType = headerBuffer[0] switch
            {
                1 => "stdout",
                2 => "stderr",
                _ => "stdout"
            };

            var payloadLength = (headerBuffer[4] << 24) | (headerBuffer[5] << 16) |
                                (headerBuffer[6] << 8) | headerBuffer[7];

            if (payloadLength == 0)
                continue;

            // Read the payload
            var remaining = payloadLength;
            while (remaining > 0)
            {
                var toRead = Math.Min(remaining, buffer.Length);
                int read;
                try
                {
                    read = await ReadExactAsync(stream, buffer, toRead, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (IOException)
                {
                    yield break;
                }

                if (read == 0)
                    yield break;

                remaining -= read;
                var text = Encoding.UTF8.GetString(buffer, 0, read);
                lineBuffer.Append(text);

                // Yield complete lines (newline-delimited JSON from Claude's stream-json output)
                while (true)
                {
                    var content = lineBuffer.ToString();
                    var newlineIndex = content.IndexOf('\n');
                    if (newlineIndex < 0)
                        break;

                    var line = content[..(newlineIndex + 1)];
                    lineBuffer.Remove(0, newlineIndex + 1);

                    if (!string.IsNullOrWhiteSpace(line))
                        yield return (line, currentStreamType);
                }
            }
        }

        // Flush any remaining content in the buffer
        if (lineBuffer.Length > 0)
        {
            var remaining = lineBuffer.ToString();
            if (!string.IsNullOrWhiteSpace(remaining))
                yield return (remaining, currentStreamType);
        }
    }

    private static async Task<int> ReadExactAsync(MultiplexedStream stream, byte[] buffer, int count,
        CancellationToken cancellationToken)
    {
        // MultiplexedStream.ReadOutputAsync returns ReadResult with buffer and stream type
        var result = await stream.ReadOutputAsync(buffer, 0, count, cancellationToken);
        return result.Count;
    }

    public async Task<long> WaitForExitAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var response = await _client.Containers.WaitContainerAsync(containerId, cancellationToken);
        _logger.LogInformation("Container {ContainerId} exited with code {ExitCode}",
            containerId[..Math.Min(12, containerId.Length)], response.StatusCode);
        return response.StatusCode;
    }

    public async Task KillContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try graceful stop first (5 second grace period)
            await _client.Containers.StopContainerAsync(containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 5 }, cancellationToken);

            _logger.LogInformation("Container {ContainerId} stopped gracefully", containerId[..Math.Min(12, containerId.Length)]);
        }
        catch (DockerContainerNotFoundException)
        {
            _logger.LogDebug("Container {ContainerId} already removed", containerId[..Math.Min(12, containerId.Length)]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping container {ContainerId}, attempting force kill",
                containerId[..Math.Min(12, containerId.Length)]);

            try
            {
                await _client.Containers.KillContainerAsync(containerId,
                    new ContainerKillParameters { Signal = "SIGKILL" }, cancellationToken);
            }
            catch (DockerContainerNotFoundException)
            {
                // Already gone
            }
        }
    }

    public async Task RemoveContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.Containers.RemoveContainerAsync(containerId,
                new ContainerRemoveParameters { Force = true }, cancellationToken);

            _logger.LogDebug("Container {ContainerId} removed", containerId[..Math.Min(12, containerId.Length)]);
        }
        catch (DockerContainerNotFoundException)
        {
            // Already removed — idempotent
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await ValueTask.CompletedTask;
        GC.SuppressFinalize(this);
    }
}
