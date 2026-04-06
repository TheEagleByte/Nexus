using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nexus.Spoke.Models;
using Nexus.Spoke.Services;

namespace Nexus.Spoke.Tests.Services;

public class PromptAssemblerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ISkillSelector> _skillSelectorMock;
    private readonly Mock<IProjectHistoryInjector> _historyInjectorMock;
    private readonly PromptAssembler _sut;

    public PromptAssemblerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nexus-test-{Guid.NewGuid():N}");
        var templatesDir = Path.Combine(_tempDir, "templates");
        Directory.CreateDirectory(templatesDir);

        // Write a simple template for testing
        File.WriteAllText(Path.Combine(templatesDir, "worker-prompt-base.md"),
            "Job: {JOB_ID} Type: {JOB_TYPE} Project: {PROJECT_KEY}\n" +
            "Ticket: {TICKET_KEY} - {TICKET_SUMMARY}\n" +
            "Issue: {ISSUE_TYPE}\n" +
            "Description: {TICKET_DESCRIPTION}\n" +
            "AC: {ACCEPTANCE_CRITERIA}\n" +
            "Context: {PROJECT_CONTEXT}\n" +
            "Plan: {IMPLEMENTATION_PLAN}\n" +
            "Skills: {SKILL_GUIDANCE}\n" +
            "History: {PROJECT_HISTORY}\n" +
            "Hub: {HUB_CONTEXT}\n" +
            "Time: {TIMESTAMP}\n");

        _skillSelectorMock = new Mock<ISkillSelector>();
        _historyInjectorMock = new Mock<IProjectHistoryInjector>();

        _skillSelectorMock
            .Setup(m => m.SelectAndSummarizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<JobType>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        _historyInjectorMock
            .Setup(m => m.GetHistorySummaryAsync(
                It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var config = new SpokeConfiguration
        {
            Workspace = new SpokeConfiguration.WorkspaceConfig { BaseDirectory = _tempDir }
        };

        _sut = new PromptAssembler(
            _skillSelectorMock.Object,
            _historyInjectorMock.Object,
            Options.Create(config),
            NullLogger<PromptAssembler>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private PromptAssemblyContext CreateContext(
        TicketMetadata? ticket = null,
        string hubContext = "do the thing",
        string? projectContext = null,
        string? planMd = null) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            JobType.Implement,
            "TEST-PROJ",
            hubContext,
            ticket,
            projectContext,
            planMd,
            Path.Combine(_tempDir, "skills", "spoke"),
            Path.Combine(_tempDir, "skills", "project"));

    [Fact]
    public async Task AssembleAsync_ReplacesAllPlaceholders()
    {
        var ticket = new TicketMetadata("NEX-99", "Add feature", "Build the feature",
            ["Feature works", "Tests pass"], "Story", null, null);

        var result = await _sut.AssembleAsync(CreateContext(ticket: ticket));

        Assert.Contains("11111111-1111-1111-1111-111111111111", result);
        Assert.Contains("Implement", result);
        Assert.Contains("TEST-PROJ", result);
        Assert.Contains("NEX-99", result);
        Assert.Contains("Add feature", result);
        Assert.Contains("Build the feature", result);
        Assert.Contains("Feature works", result);
        Assert.Contains("Tests pass", result);
        Assert.Contains("Story", result);
        Assert.Contains("do the thing", result);
        Assert.DoesNotContain("{JOB_ID}", result);
        Assert.DoesNotContain("{TICKET_KEY}", result);
    }

    [Fact]
    public async Task AssembleAsync_WithNullTicket_UsesHubContext()
    {
        var result = await _sut.AssembleAsync(CreateContext(hubContext: "hub provided context"));

        Assert.Contains("hub provided context", result);
        // Ticket placeholders should be empty, not the literal placeholder
        Assert.DoesNotContain("{TICKET_KEY}", result);
    }

    [Fact]
    public async Task AssembleAsync_InjectsProjectContext()
    {
        var result = await _sut.AssembleAsync(
            CreateContext(projectContext: "This is a .NET project with EF Core"));

        Assert.Contains("This is a .NET project with EF Core", result);
    }

    [Fact]
    public async Task AssembleAsync_InjectsPlanContent()
    {
        var result = await _sut.AssembleAsync(
            CreateContext(planMd: "## Step 1\nDo something important"));

        Assert.Contains("Do something important", result);
    }

    [Fact]
    public async Task AssembleAsync_SkipsDefaultPlanStub()
    {
        var result = await _sut.AssembleAsync(
            CreateContext(planMd: "# Implementation Plan\n\nPlan will be generated by the agent.\n"));

        // The default stub content should not appear
        Assert.DoesNotContain("Plan will be generated by the agent", result);
    }

    [Fact]
    public async Task AssembleAsync_InjectsSkillGuidance()
    {
        _skillSelectorMock
            .Setup(m => m.SelectAndSummarizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<JobType>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("- **auth-patterns** (spoke): OAuth and JWT best practices");

        var result = await _sut.AssembleAsync(CreateContext());

        Assert.Contains("auth-patterns", result);
        Assert.Contains("OAuth and JWT", result);
    }

    [Fact]
    public async Task AssembleAsync_InjectsProjectHistory()
    {
        _historyInjectorMock
            .Setup(m => m.GetHistorySummaryAsync(
                It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("### Job 11111111 (2026-04-01)\nFixed login bug");

        var result = await _sut.AssembleAsync(CreateContext());

        Assert.Contains("Fixed login bug", result);
    }

    [Fact]
    public async Task AssembleAsync_FormatsAcceptanceCriteria()
    {
        var ticket = new TicketMetadata("NEX-1", "Test", null,
            ["Criterion one", "Criterion two"], null, null, null);

        var result = await _sut.AssembleAsync(CreateContext(ticket: ticket));

        Assert.Contains("- [ ] Criterion one", result);
        Assert.Contains("- [ ] Criterion two", result);
    }

    [Fact]
    public async Task AssembleAsync_WithMissingTemplate_UsesFallback()
    {
        // Delete the template
        File.Delete(Path.Combine(_tempDir, "templates", "worker-prompt-base.md"));

        var result = await _sut.AssembleAsync(CreateContext(hubContext: "fallback test"));

        // Should still produce output using fallback template
        Assert.Contains("fallback test", result);
        Assert.DoesNotContain("{HUB_CONTEXT}", result);
    }

    [Fact]
    public async Task AssembleAsync_CallsSkillSelectorWithCorrectArgs()
    {
        var ticket = new TicketMetadata("NEX-1", "Test", "Build auth", null, null, null, null);
        var context = CreateContext(ticket: ticket);

        await _sut.AssembleAsync(context);

        _skillSelectorMock.Verify(m => m.SelectAndSummarizeAsync(
            context.SpokeSkillsPath,
            context.ProjectSkillsPath,
            JobType.Implement,
            "Build auth",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssembleAsync_CallsHistoryInjectorWithCorrectArgs()
    {
        var context = CreateContext();

        await _sut.AssembleAsync(context);

        _historyInjectorMock.Verify(m => m.GetHistorySummaryAsync(
            "TEST-PROJ",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
