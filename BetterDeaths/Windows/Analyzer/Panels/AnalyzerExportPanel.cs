namespace BetterDeaths.Windows.Analyzer.Panels;

using BetterDeaths.Domain;
using BetterDeaths.Exports;
using Dalamud.Bindings.ImGui;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

internal sealed class AnalyzerExportPanel : IAnalyzerWorkspacePanel
{
    private readonly object stateLock = new();
    private string exportDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "BetterDeaths",
        "AnalyzerExports");
    private string? status;
    private bool exporting;

    public string Id => "export";

    public string Label => "Export";

    public void Draw(AnalyzerWorkspacePanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ImGui.Text("Canonical pull export");
        ImGui.TextWrapped("Canonical export retains player names, absolute timestamps, and source references. Use it for your own local data interchange.");
        ImGui.TextWrapped("Anonymized export replaces player/pet/unknown actor names, removes source references and wall-clock timestamps, and preserves pull-relative evidence plus actor relationships.");
        ImGui.Separator();

        ImGui.SetNextItemWidth(-1.0f);
        ImGui.InputText("##AnalyzerExportDirectory", ref exportDirectory, 1024);
        ImGui.TextDisabled("Export directory");

        bool isExporting;
        string? currentStatus;
        lock (stateLock)
        {
            isExporting = exporting;
            currentStatus = status;
        }

        if (isExporting)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Export canonical"))
        {
            QueueExport(context.Pull, CanonicalPullExportMode.Canonical);
        }

        ImGui.SameLine();
        if (ImGui.Button("Export anonymized"))
        {
            QueueExport(context.Pull, CanonicalPullExportMode.Anonymized);
        }

        if (isExporting)
        {
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextDisabled("Exporting...");
        }

        if (!string.IsNullOrWhiteSpace(currentStatus))
        {
            ImGui.TextWrapped(currentStatus);
        }
    }

    private void QueueExport(RecordedPull pull, CanonicalPullExportMode mode)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var requestedDirectory = exportDirectory.Trim();

        lock (stateLock)
        {
            if (exporting)
            {
                return;
            }

            exporting = true;
            status = null;
        }

        _ = Task.Run(async () =>
        {
            string? tempPath = null;
            try
            {
                if (string.IsNullOrWhiteSpace(requestedDirectory))
                {
                    throw new InvalidOperationException("Choose an export directory first.");
                }

                var directory = Path.GetFullPath(requestedDirectory);
                Directory.CreateDirectory(directory);
                var export = CanonicalPullExporter.Export(new CanonicalPullExportRequest
                {
                    Pull = pull,
                    Options = new CanonicalPullExportOptions { Mode = mode },
                });
                var kind = mode == CanonicalPullExportMode.Anonymized ? "anonymized" : "canonical";
                var fileName = $"{kind}-pull-{export.ExportedPullId.Value:N}.json";
                var path = Path.Combine(directory, fileName);
                tempPath = path + ".tmp";

                await File.WriteAllTextAsync(tempPath, export.Payload, Encoding.UTF8).ConfigureAwait(false);
                File.Move(tempPath, path, overwrite: true);
                tempPath = null;

                lock (stateLock)
                {
                    status = $"Exported {kind} pull to {path}";
                }
            }
            catch (Exception exception)
            {
                lock (stateLock)
                {
                    status = $"Export failed: {exception.Message}";
                }
            }
            finally
            {
                if (tempPath is not null)
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                lock (stateLock)
                {
                    exporting = false;
                }
            }
        });
    }
}
