param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,

    [Parameter(Mandatory = $false)]
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'images')
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $RepositoryRoot 'Project auto push to gitlab.csproj'
if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file was not found: $projectPath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$captureTempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SqlOptimizerManualCapture-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $captureTempRoot | Out-Null

try {
    $escapedProjectPath = [System.Security.SecurityElement]::Escape($projectPath)
    $captureProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$escapedProjectPath" />
  </ItemGroup>
</Project>
"@

    $captureSource = @'
using System.Data;
using System.Drawing.Imaging;
using Project_auto_push_to_gitlab;
using Project_auto_push_to_gitlab.SqlOptimizer.Codex;

namespace SqlOptimizerManualCapture;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Output directory is required.");
            return 2;
        }

        string outputDirectory = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(outputDirectory);

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        SqlOptimizerFormInput safeInput = new(
            "KTGH00",
            "SELECT :DEPT_ID AS DEPT_ID, 'SAMPLE' AS LABEL FROM DUAL",
            "\u30D0\u30E9\u30F3\u30B9",
            60,
            200,
            10,
            new[]
            {
                new SqlOptimizerBindRow("DEPT_ID", "Int32", false, "10", null)
            });

        using (frm_SqlOptimizer draftForm = CreateBaseForm(safeInput))
        {
            draftForm.SetUiState(
                SqlOptimizerFormState.Draft,
                "Safe dummy input. Review the values and approve the session.");
            Capture(draftForm, Path.Combine(outputDirectory, "actual-input-draft.png"), "SAFE DUMMY DATA / Actual form input");
        }

        using (frm_SqlOptimizer resultForm = CreateBaseForm(safeInput))
        {
            DataTable preview = new();
            preview.Columns.Add("DEPT_ID", typeof(int));
            preview.Columns.Add("LABEL", typeof(string));
            preview.Rows.Add(10, "SAMPLE");

            resultForm.SetSessionId("00000000-0000-0000-0000-000000000001");
            resultForm.SetMcpStatus("MCP: local sample data");
            resultForm.UpsertIteration(new SqlOptimizerIterationDisplay
            {
                IterationId = "sample-round-1-candidate-2",
                Round = 1,
                Candidate = 2,
                Status = "Completed",
                Cost = 3,
                ElapsedMilliseconds = 18,
                Rows = 1,
                PlanHash = "SAMPLE-PLAN",
                UpdatedAt = new DateTime(2026, 7, 27, 10, 30, 0),
                SqlDiff = "Candidate 2: output columns and bind set are preserved (sample).",
                ExplainPlan = "Plan hash value: SAMPLE-PLAN\r\n| Id | Operation        | Name |\r\n|  0 | SELECT STATEMENT |      |\r\n|  1 | FAST DUAL        |      |",
                AiAnalysis = "Sample analysis: output shape is preserved. Use the actual plan for the final decision.",
                ActualPlan = "ALLSTATS LAST (sample)\r\nA-Rows: 1 / Elapsed: 18 ms",
                ResultPreview = preview
            });
            resultForm.SetUiState(
                SqlOptimizerFormState.AwaitingUser,
                "Review the final candidate, then accept, continue with feedback, or stop.");
            Capture(resultForm, Path.Combine(outputDirectory, "actual-awaiting-user.png"), "SAFE DUMMY DATA / Actual form result");
        }

        return 0;
    }

    private static frm_SqlOptimizer CreateBaseForm(SqlOptimizerFormInput input)
    {
        frm_SqlOptimizer form = new()
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-30000, -30000)
        };
        form.LoadInputForReview(input);
        CodexModelOption safeModel = new(
            "sample-model-id",
            "gpt-sample-codex",
            "GPT Sample Codex",
            "Safe screenshot sample",
            true,
            "high",
            new[]
            {
                new CodexReasoningEffortOption("medium", "Sample medium reasoning"),
                new CodexReasoningEffortOption("high", "Sample high reasoning")
            },
            new[]
            {
                new CodexServiceTierOption("priority", "Fast", "Sample fast tier")
            },
            "default",
            null,
            null);
        form.SetCodexModelCatalog(
            new CodexModelCatalog(new[] { safeModel }, DateTimeOffset.UtcNow),
            new CodexExecutionSelection("gpt-sample-codex", "high", "default"),
            "SAFE SAMPLE: one Codex model loaded.");
        form.SetSessionId(string.Empty);
        form.SetMcpStatus("MCP: disconnected (sample)");
        return form;
    }

    private static void Capture(Form form, string outputPath, string watermark)
    {
        form.Show();
        form.BringToFront();
        Application.DoEvents();
        form.PerformLayout();
        Application.DoEvents();

        using Bitmap image = new(form.ClientSize.Width, form.ClientSize.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.ClientSize));
        using (Graphics graphics = Graphics.FromImage(image))
        using (Font font = new("Yu Gothic UI", 12F, FontStyle.Bold, GraphicsUnit.Point))
        using (Brush background = new SolidBrush(Color.FromArgb(220, 255, 248, 220)))
        using (Brush foreground = new SolidBrush(Color.FromArgb(255, 90, 60, 0)))
        {
            SizeF measured = graphics.MeasureString(watermark, font);
            RectangleF box = new(
                image.Width - measured.Width - 28F,
                image.Height - measured.Height - 14F,
                measured.Width + 20F,
                measured.Height + 8F);
            graphics.FillRectangle(background, box);
            graphics.DrawString(watermark, font, foreground, box.X + 10F, box.Y + 4F);
        }

        image.Save(outputPath, ImageFormat.Png);
        form.Hide();
    }
}
'@

    [System.IO.File]::WriteAllText(
        (Join-Path $captureTempRoot 'SqlOptimizerManualCapture.csproj'),
        $captureProject,
        [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText(
        (Join-Path $captureTempRoot 'Program.cs'),
        $captureSource,
        [System.Text.UTF8Encoding]::new($false))

    $captureProjectPath = Join-Path $captureTempRoot 'SqlOptimizerManualCapture.csproj'
    & dotnet restore $captureProjectPath --ignore-failed-sources --property NuGetAudit=false
    if ($LASTEXITCODE -ne 0) {
        throw "The form capture helper restore failed with exit code $LASTEXITCODE."
    }

    & dotnet run --project $captureProjectPath --configuration Release --no-restore -- $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "The form capture helper failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $captureTempRoot) {
        Remove-Item -LiteralPath $captureTempRoot -Recurse -Force
    }
}
