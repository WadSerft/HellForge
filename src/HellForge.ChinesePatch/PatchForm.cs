using System.IO.Compression;
using System.Reflection;

namespace HellForge.ChinesePatch;

// The Chinese localization as one small installer that is its own uninstaller: run it once to apply (the launcher's
// current files are kept beside the patch's as ".pre-chinese", never overwritten), run it again to put them back.
// It touches only app\launcher's own files and language.json in the root; the game copy, profiles and mods are
// never read.
public sealed class PatchForm : Form
{
    // The launcher's files the patch replaces; zh-CN\ is handled with the same ".pre-chinese" suffix, as a folder.
    static readonly string[] PatchedFiles =
    [
        "HellForge.Launcher.dll", "HellForge.Core.dll", "HellForge.SteamDownload.dll",
        "HellForge.Launcher.exe", "HellForge.Launcher.deps.json", "HellForge.Launcher.runtimeconfig.json"
    ];

    const string Suffix = ".pre-chinese";

    readonly TextBox rootBox = new() { Dock = DockStyle.Fill };
    readonly Label status = new() { Dock = DockStyle.Fill, Height = 60, ForeColor = Color.Black };
    readonly Button browseButton = new() { Text = "浏览…", Dock = DockStyle.Fill };
    readonly Button actionButton = new() { Text = "应用汉化", Dock = DockStyle.Fill };
    string launcherFolder = "";

    public PatchForm()
    {
        Text = "HellForge 汉化补丁";
        MinimumSize = new Size(560, 300);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var padding = new Padding(16);
        var rootRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 36, ColumnCount = 2, ColumnStyles = { new(SizeType.Percent, 100F), new(SizeType.Absolute, 96F) } };
        rootRow.Controls.Add(rootBox, 0, 0);
        rootRow.Controls.Add(browseButton, 1, 0);
        var buttonRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 44, ColumnCount = 1, Padding = new Padding(0, 10, 0, 0) };
        buttonRow.Controls.Add(actionButton);
        Controls.Add(status);
        Controls.Add(rootRow);
        Controls.Add(buttonRow);
        status.Padding = padding;
        rootRow.Padding = padding;

        rootBox.Text = FindDefaultRoot();
        browseButton.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog { Description = "选择 HellForge 的安装文件夹（含 app\\launcher 的那个）", UseDescriptionForTitle = true };
            if (picker.ShowDialog(this) == DialogResult.OK) rootBox.Text = picker.SelectedPath;
        };
        rootBox.TextChanged += (_, _) => RefreshState();
        actionButton.Click += async (_, _) => await RunAsync();
        RefreshState();
    }

    // Where this patch most likely applies: the folder it was run from, then the usual place.
    static string FindDefaultRoot()
    {
        var candidates = new List<string> { AppContext.BaseDirectory };
        for (var folder = AppContext.BaseDirectory; !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar))!)
        {
            if (File.Exists(Path.Combine(folder, "hellforge-install.json")) && Directory.Exists(Path.Combine(folder, "app", "launcher")))
                return folder;
        }
        candidates.Add(@"D:\Games\Helldivers.2-InsaneRamZes");
        return candidates.Last();
    }

    string LauncherFolder => Path.Combine(rootBox.Text.Trim(), "app", "launcher");

    bool Installed => PatchedFiles.Any(name => File.Exists(Path.Combine(LauncherFolder, name + Suffix)));

    void RefreshState()
    {
        launcherFolder = LauncherFolder;
        actionButton.Enabled = false;
        if (!Directory.Exists(launcherFolder))
        {
            status.Text = rootBox.Text.Trim().Length == 0
                ? "选择 HellForge 的安装文件夹。"
                : $"这里没有找到 HellForge 安装（缺少 app\\launcher）。请选择正确的文件夹。";
            return;
        }
        if (Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip") == null)
        {
            status.Text = "这个程序没有内嵌补丁文件（本地裸构建）。请使用 CI 构建的 HellForge-Chinese-Patch-Setup.exe。";
            return;
        }
        if (Installed)
        {
            status.Text = "检测到已汉化（原文件保留为 *.pre-chinese）。再次运行可还原英文原版。";
            actionButton.Text = "还原英文原版";
        }
        else
        {
            status.Text = "未汉化。应用后原文件会保留为 *.pre-chinese，随时可再次运行本程序还原。";
            actionButton.Text = "应用汉化";
        }
        actionButton.Enabled = true;
    }

    async Task RunAsync()
    {
        actionButton.Enabled = false;
        browseButton.Enabled = false;
        string result;
        try
        {
            result = Installed ? await Task.Run(Revert) : await Task.Run(Apply);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            status.Text = "失败：" + error.Message;
            return;
        }
        finally { browseButton.Enabled = true; }
        RefreshState();
        MessageBox.Show(this, result, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    string Apply()
    {
        var installed = VersionOf(Path.Combine(launcherFolder, "HellForge.Core.dll"));
        var staged = Path.Combine(Path.GetTempPath(), "HellForge-Chinese-Patch");
        if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);
        using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")!)
        using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries.Where(entry => entry.Name.EndsWith("HellForge.Core.dll", StringComparison.OrdinalIgnoreCase)))
            {
                Directory.CreateDirectory(staged);
                entry.ExtractToFile(Path.Combine(staged, entry.Name), overwrite: true);
                break;
            }
        }
        var patchVersion = VersionOf(Path.Combine(staged, "HellForge.Core.dll"));
        if (installed == null || patchVersion == null || !installed.StartsWith(patchVersion, StringComparison.OrdinalIgnoreCase))
            return $"版本不符：安装的是 {installed ?? "未知版本"}，补丁构建自 {patchVersion ?? "未知版本"}。请先升级 HellForge（或用对应版本的补丁）。";

        foreach (var name in PatchedFiles)
        {
            var path = Path.Combine(launcherFolder, name);
            if (File.Exists(path)) File.Move(path, path + Suffix);
        }
        var resources = Path.Combine(launcherFolder, "zh-CN");
        if (Directory.Exists(resources)) Directory.Move(resources, resources + Suffix);

        using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")!)
        using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
            archive.ExtractToDirectory(launcherFolder, overwriteFiles: true);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(launcherFolder)!, "..", "language.json"), "{\"language\":\"zh-CN\"}");
        return "汉化已应用。启动 HellForge 即为中文；再次运行本程序可还原。";
    }

    string Revert()
    {
        foreach (var name in PatchedFiles)
        {
            var patched = Path.Combine(launcherFolder, name);
            if (File.Exists(patched)) File.Delete(patched);
            var saved = patched + Suffix;
            if (File.Exists(saved)) File.Move(saved, patched);
        }
        var resources = Path.Combine(launcherFolder, "zh-CN");
        if (Directory.Exists(resources)) Directory.Delete(resources, recursive: true);
        var savedResources = resources + Suffix;
        if (Directory.Exists(savedResources)) Directory.Move(savedResources, resources);
        var language = Path.GetFullPath(Path.Combine(launcherFolder, "..", "..", "language.json"));
        if (File.Exists(language)) File.Delete(language);
        return "已还原英文原版（*.pre-chinese 已恢复原名）。";
    }

    // A launcher assembly's product version (0.4.0-beta...), or null when it cannot be read.
    static string? VersionOf(string path) => File.Exists(path)
        ? System.Diagnostics.FileVersionInfo.GetVersionInfo(path).ProductVersion
        : null;
}
