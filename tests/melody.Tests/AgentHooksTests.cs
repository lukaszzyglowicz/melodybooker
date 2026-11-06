using System.Diagnostics;

namespace melody.Tests;

/// <summary>
/// Proves the GitHub Copilot CLI agent hooks (<c>.github/hooks/check-edited-file.js</c> for
/// <c>postToolUse</c> and <c>.github/hooks/end-of-turn.js</c> for <c>agentStop</c>) actually behave
/// as documented in <c>.github/skills/10x-configure-hook/references/copilot.md</c>: silent on a
/// clean/skipped/nonexistent/unparseable input, and loud (via the channel that reaches the agent)
/// on a real compiler or test failure.
/// </summary>
/// <remarks>
/// Every case that edits a file runs against a throwaway <c>git worktree</c> checked out from
/// <c>HEAD</c> (see <see cref="HookWorktreeFixture"/>), never against the developer's own working
/// tree. The hook scripts themselves are read from the real repo root (they do not need to be
/// committed for this to work: the scripts only need a target checkout to resolve via
/// <c>git rev-parse --show-toplevel</c>, and the worktree supplies that).
/// </remarks>
public class AgentHooksTests : IClassFixture<HookWorktreeFixture>
{
    private readonly HookWorktreeFixture _fixture;
    private readonly string _checkEditedFileScript;
    private readonly string _endOfTurnScript;

    private const string StudentRelPath = "src/melody/Models/Domain/Student.cs";
    private const string ReadmeRelPath = "README.md";

    public AgentHooksTests(HookWorktreeFixture fixture)
    {
        _fixture = fixture;
        _checkEditedFileScript = Path.Combine(_fixture.RepoRoot, ".github", "hooks", "check-edited-file.js");
        _endOfTurnScript = Path.Combine(_fixture.RepoRoot, ".github", "hooks", "end-of-turn.js");
    }

    // ---- check-edited-file.js (postToolUse) -------------------------------------------------

    [Fact]
    public void CheckEditedFile_CleanFile_ProducesNoOutput()
    {
        var absPath = _fixture.AbsolutePath(StudentRelPath);
        var payload = BuildEditPayload(absPath);

        var result = RunCheckEditedFile(payload);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout.Trim());
    }

    [Fact]
    public void CheckEditedFile_BrokenFile_ReportsCompilerErrorForThatFile()
    {
        _fixture.WriteFile(StudentRelPath, original =>
            original.Replace("TimeSpan.FromMinutes(20)", "TimeSpanBROKEN.FromMinutes(20)"));
        try
        {
            var absPath = _fixture.AbsolutePath(StudentRelPath);
            var payload = BuildEditPayload(absPath);

            var result = RunCheckEditedFile(payload);

            Assert.Equal(0, result.ExitCode); // postToolUse signals via stdout JSON, never a nonzero exit
            Assert.Contains("additionalContext", result.Stdout);
            Assert.Contains("Student.cs", result.Stdout);
            Assert.Contains("error CS", result.Stdout); // "TimeSpanBROKEN" is an unknown type
        }
        finally
        {
            _fixture.RestoreFile(StudentRelPath);
        }
    }

    [Fact]
    public void CheckEditedFile_SkippedFileType_ProducesNoOutput()
    {
        var absPath = _fixture.AbsolutePath(ReadmeRelPath);
        var payload = BuildEditPayload(absPath);

        var result = RunCheckEditedFile(payload);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout.Trim());
    }

    [Fact]
    public void CheckEditedFile_NonexistentFile_ProducesNoOutput()
    {
        var absPath = _fixture.AbsolutePath("src/melody/Models/Domain/DoesNotExist.cs");
        var payload = BuildEditPayload(absPath);

        var result = RunCheckEditedFile(payload);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout.Trim());
    }

    [Fact]
    public void CheckEditedFile_EmptyPayload_ProducesNoOutput()
    {
        var result = RunCheckEditedFile("{}");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout.Trim());
    }

    [Fact]
    public void CheckEditedFile_UnparseablePayload_ProducesNoOutput()
    {
        var result = RunCheckEditedFile("not json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout.Trim());
    }

    [Fact]
    public void CheckEditedFile_ReadOnlyToolName_SkipsEvenAnExistingBrokenFile()
    {
        _fixture.WriteFile(StudentRelPath, original =>
            original.Replace("TimeSpan.FromMinutes(20)", "TimeSpanBROKEN.FromMinutes(20)"));
        try
        {
            var absPath = _fixture.AbsolutePath(StudentRelPath);
            var payload = BuildViewPayload(absPath);

            var result = RunCheckEditedFile(payload);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.Stdout.Trim()); // read-only tools never trigger a build
        }
        finally
        {
            _fixture.RestoreFile(StudentRelPath);
        }
    }

    // Note: "no silent success when dotnet is missing" (check-edited-file.js's result.error branch)
    // is implemented in the script but is not re-proven here as an automated case: simulating a
    // missing `dotnet` by stripping it from PATH for a grandchild process (test -> node -> dotnet)
    // resolved inconsistently across this repo's Windows dev machine and the Linux CI runner
    // (multiple PATH entries containing a dotnet executable, shell-specific exec resolution), so
    // keeping it caused CI flakes without proving anything the manual session proof didn't already
    // confirm. Verified manually: deleting PATH's dotnet entirely before invoking the hook script
    // produces `"could not run"` in its `additionalContext` output, never a silent exit 0.

    // ---- end-of-turn.js (agentStop) ----------------------------------------------------------

    [Fact]
    public void EndOfTurn_CleanTree_ProducesNoOutput()
    {
        _fixture.EnsureClean();

        var result = RunEndOfTurn("{}");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout.Trim());
    }

    [Fact]
    public void EndOfTurn_RetryFlagSet_SkipsEvenWithAPendingError()
    {
        _fixture.WriteFile(StudentRelPath, original =>
            original.Replace("TimeSpan.FromMinutes(20)", "TimeSpanBROKEN.FromMinutes(20)"));
        try
        {
            var result = RunEndOfTurn("""{"stop_hook_active":true}""");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.Stdout.Trim()); // one retry only; never loop forever
        }
        finally
        {
            _fixture.RestoreFile(StudentRelPath);
        }
    }

    [Fact]
    public void EndOfTurn_EditBypassedThePerEditHook_BlocksAndNamesTheFile()
    {
        // Simulates an edit made through a shell command rather than the harness's edit tool:
        // no per-edit hook ever saw this change, so end-of-turn is the only safety net. Uses a
        // compile-breaking edit (not a logic-only test failure) so the solution build fails before
        // end-of-turn.js ever reaches `dotnet test` - proving the "block and name the file" path
        // without paying for a first-ever cold build of the whole test project in the throwaway
        // worktree (its many framework assemblies make that prohibitively slow under this
        // machine's antivirus real-time scanning of a brand-new directory).
        _fixture.WriteFile(StudentRelPath, original =>
            original.Replace("TimeSpan.FromMinutes(20)", "TimeSpanBROKEN.FromMinutes(20)"));
        try
        {
            var result = RunEndOfTurn("{}");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"decision\":\"block\"", result.Stdout);
            Assert.Contains("Student.cs", result.Stdout);
            Assert.Contains("error CS", result.Stdout);
        }
        finally
        {
            _fixture.RestoreFile(StudentRelPath);
        }
    }

    // ---- helpers ------------------------------------------------------------------------------

    private (int ExitCode, string Stdout, string Stderr) RunCheckEditedFile(string payload) =>
        RunNode(_checkEditedFileScript, payload);

    private (int ExitCode, string Stdout, string Stderr) RunEndOfTurn(string payload) =>
        RunNode(_endOfTurnScript, payload);

    private (int ExitCode, string Stdout, string Stderr) RunNode(string scriptPath, string payload)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _fixture.NodeExePath,
            WorkingDirectory = _fixture.WorktreePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(scriptPath);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start node for {scriptPath}");
        process.StandardInput.Write(payload);
        process.StandardInput.Close();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        var exited = process.WaitForExit(60_000);
        Assert.True(exited, $"{Path.GetFileName(scriptPath)} did not exit within 60s");

        return (process.ExitCode, stdout, stderr);
    }

    private static string EscapeJson(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string BuildEditPayload(string absPath) =>
        "{\"tool_name\":\"edit\",\"tool_input\":{\"file_path\":\"" + EscapeJson(absPath) + "\"}}";

    private static string BuildViewPayload(string absPath) =>
        "{\"tool_name\":\"view\",\"tool_input\":{\"file_path\":\"" + EscapeJson(absPath) + "\"}}";
}

/// <summary>
/// Creates one throwaway <c>git worktree</c> (checked out from <c>HEAD</c>) shared by every test in
/// <see cref="AgentHooksTests"/>, so deliberate breaks never touch the developer's actual working
/// tree. xUnit runs the tests within one class sequentially by default, so reusing a single
/// worktree across test methods is safe.
/// </summary>
public sealed class HookWorktreeFixture : IDisposable
{
    public string RepoRoot { get; }
    public string WorktreePath { get; }
    public string NodeExePath { get; }

    private readonly Dictionary<string, string> _originalContents = new();

    public HookWorktreeFixture()
    {
        RepoRoot = RunGitCapture(AppContext.BaseDirectory, "rev-parse --show-toplevel").Trim();
        WorktreePath = Path.Combine(Path.GetTempPath(), "melody-hook-test-" + Guid.NewGuid().ToString("N"));
        RunGit(RepoRoot, $"worktree add --detach \"{WorktreePath}\" HEAD");
        NodeExePath = ResolveExePath("node");
    }

    public string AbsolutePath(string repoRelativePath) =>
        Path.Combine(WorktreePath, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));

    public void WriteFile(string repoRelativePath, Func<string, string> transform)
    {
        var absPath = AbsolutePath(repoRelativePath);
        var original = File.ReadAllText(absPath);
        _originalContents[repoRelativePath] = original;
        File.WriteAllText(absPath, transform(original));
    }

    public void RestoreFile(string repoRelativePath)
    {
        RunGit(WorktreePath, $"checkout -- \"{repoRelativePath}\"");
        _originalContents.Remove(repoRelativePath);
    }

    public void EnsureClean()
    {
        RunGit(WorktreePath, "checkout -- .");
        RunGit(WorktreePath, "clean -fd");
    }

    public void Dispose()
    {
        try
        {
            EnsureClean();
        }
        catch
        {
            /* best-effort cleanup */
        }

        try
        {
            RunGit(RepoRoot, $"worktree remove --force \"{WorktreePath}\"");
        }
        catch
        {
            try
            {
                Directory.Delete(WorktreePath, recursive: true);
            }
            catch
            {
                /* best-effort cleanup */
            }
        }
    }

    private static string ResolveExePath(string name)
    {
        // "where" on Windows, "which" everywhere else (CI runs on ubuntu-latest).
        var finder = OperatingSystem.IsWindows() ? "where" : "which";
        var result = RunCapture(Environment.CurrentDirectory, finder, name);
        var first = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
        {
            throw new InvalidOperationException($"Could not resolve '{name}' via '{finder} {name}'.");
        }

        return first;
    }

    private static void RunGit(string cwd, string arguments) => RunCapture(cwd, "git", arguments);

    private static string RunGitCapture(string cwd, string arguments) => RunCapture(cwd, "git", arguments);

    private static string RunCapture(string cwd, string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start '{fileName} {arguments}'");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{fileName} {arguments}' failed (exit {process.ExitCode}):\n{stdout}\n{stderr}");
        }

        return stdout;
    }
}

