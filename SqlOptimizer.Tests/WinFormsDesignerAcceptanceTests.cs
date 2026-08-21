using System.Collections;
using System.Drawing;
using System.Data;
using System.Reflection;
using System.Resources;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Project_auto_push_to_gitlab;
using Project_auto_push_to_gitlab.SqlOptimizer.Codex;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>
/// 驗證frm_SqlOptimizer的Designer資源、基本配置與定時顯示更新行為。
/// </summary>
/// <remarks>
/// UI：直接建立frm_SqlOptimizer，檢查Designer控制項與Grid更新；不啟動Controller或Oracle連線。
/// </remarks>
public sealed class WinFormsDesignerAcceptanceTests
{
    [Fact]
    public void SqlOptimizerForm_DesignerAndResxInitializeOnStaThread()
    {
        AssertEmbeddedResxCanBeRead();

        ExceptionDispatchInfo? capturedFailure = null;
        Thread staThread = new(
            () =>
            {
                try
                {
                    Assert.Equal(
                        ApartmentState.STA,
                        Thread.CurrentThread.GetApartmentState());
                    VerifyFormCanBeConstructedAndDisposed();
                }
                catch (Exception exception)
                {
                    capturedFailure = ExceptionDispatchInfo.Capture(exception);
                }
            })
        {
            IsBackground = true,
            Name = "frm_SqlOptimizer Designer acceptance"
        };
        staThread.SetApartmentState(ApartmentState.STA);

        staThread.Start();
        bool completed = staThread.Join(TimeSpan.FromSeconds(10));

        Assert.True(
            completed,
            "frm_SqlOptimizer construction did not finish within 10 seconds.");
        capturedFailure?.Throw();
    }

    /// <summary>
    /// 驗證相同履歷快照重複送入時保留既有Grid列及結果預覽DataSource，避免輪詢造成閃爍。
    /// </summary>
    /// <remarks>
    /// UI：直接使用frm_SqlOptimizer的gridHistory與gridResultPreview。
    /// 測試在STA執行緒執行，不啟動Controller、Timer、Codex或Oracle。
    /// </remarks>
    [Fact]
    public void SqlOptimizerForm_RepeatedHistorySnapshotPreservesGridObjects()
    {
        // 1150820 codex しずくちゃん_GPT-5：以兩份可見內容相同但DataTable不同的快照驗證Grid不重建。
        ExceptionDispatchInfo? capturedFailure = null;
        Thread staThread = new(
            () =>
            {
                try
                {
                    using frm_SqlOptimizer form = new();
                    DataTable initialPreview = new("InitialPreview");
                    initialPreview.Columns.Add("VALUE", typeof(string));
                    initialPreview.Rows.Add("safe-value");
                    DateTime updatedAt = new(2026, 8, 20, 9, 0, 0);
                    SqlOptimizerIterationDisplay initial = new()
                    {
                        IterationId = Guid.NewGuid().ToString("D"),
                        Round = 1,
                        Candidate = 1,
                        Status = "Completed",
                        Cost = 2,
                        ElapsedMilliseconds = 12,
                        Rows = 1,
                        PlanHash = "123456",
                        UpdatedAt = updatedAt,
                        SqlDiff = "same diff",
                        ExplainPlan = "same explain",
                        AiAnalysis = "same analysis",
                        ActualPlan = "same actual",
                        ResultPreview = initialPreview
                    };

                    form.SetIterationHistory([initial]);
                    DataGridView history =
                        FindControl<DataGridView>(form, "gridHistory");
                    DataGridView resultPreview =
                        FindControl<DataGridView>(form, "gridResultPreview");
                    DataGridViewRow originalRow = Assert.Single(
                        history.Rows.Cast<DataGridViewRow>());
                    Assert.Same(initialPreview, resultPreview.DataSource);

                    DataTable replacementPreview = initialPreview.Copy();
                    SqlOptimizerIterationDisplay repeated = new()
                    {
                        IterationId = initial.IterationId,
                        Round = initial.Round,
                        Candidate = initial.Candidate,
                        Status = initial.Status,
                        Cost = initial.Cost,
                        ElapsedMilliseconds = initial.ElapsedMilliseconds,
                        Rows = initial.Rows,
                        PlanHash = initial.PlanHash,
                        UpdatedAt = initial.UpdatedAt,
                        SqlDiff = initial.SqlDiff,
                        ExplainPlan = initial.ExplainPlan,
                        AiAnalysis = initial.AiAnalysis,
                        ActualPlan = initial.ActualPlan,
                        ResultPreview = replacementPreview
                    };

                    form.SetIterationHistory([repeated]);

                    Assert.Same(originalRow, Assert.Single(
                        history.Rows.Cast<DataGridViewRow>()));
                    Assert.Same(initialPreview, resultPreview.DataSource);
                }
                catch (Exception exception)
                {
                    capturedFailure = ExceptionDispatchInfo.Capture(exception);
                }
            })
        {
            IsBackground = true,
            Name = "frm_SqlOptimizer history refresh acceptance"
        };
        staThread.SetApartmentState(ApartmentState.STA);

        staThread.Start();
        bool completed = staThread.Join(TimeSpan.FromSeconds(10));

        Assert.True(
            completed,
            "frm_SqlOptimizer history refresh did not finish within 10 seconds.");
        capturedFailure?.Throw();
    }

    private static void AssertEmbeddedResxCanBeRead()
    {
        const string resourceName =
            "Project_auto_push_to_gitlab.frm_SqlOptimizer.resources";
        Assembly assembly = typeof(frm_SqlOptimizer).Assembly;

        Assert.Contains(resourceName, assembly.GetManifestResourceNames());
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' was not found.");
        using ResourceReader reader = new(stream);
        IDictionaryEnumerator enumerator = reader.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Assert.NotNull(enumerator.Key);
        }
    }

    /// <summary>
    /// 驗證frm_SqlOptimizer可建立與釋放、方案A配置完整，且動態Codex能力與狀態鎖定符合規則。
    /// </summary>
    /// <remarks>
    /// UI：直接檢查frm_SqlOptimizer的Designer控制項、Tab順序、最小尺寸與Codex三個選擇欄。
    /// </remarks>
    private static void VerifyFormCanBeConstructedAndDisposed()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：在STA執行緒以實際Form驗證新增Codex設定列。
        frm_SqlOptimizer form = new();
        try
        {
            Assert.False(form.Visible);
            Assert.False(form.IsDisposed);
            Assert.Equal("AI SQL最適化", form.Text);
            Assert.Equal(SqlOptimizerFormState.Draft, form.CurrentState);

            ComboBox hospitalArea =
                FindControl<ComboBox>(form, "cmbHospitalArea");
            ComboBox optimizationGoal =
                FindControl<ComboBox>(form, "cmbGoal");
            NumericUpDown timeout =
                FindControl<NumericUpDown>(form, "numTimeoutSeconds");
            NumericUpDown previewRows =
                FindControl<NumericUpDown>(form, "numPreviewRows");
            NumericUpDown maxCandidates =
                FindControl<NumericUpDown>(form, "numMaxCandidates");
            TextBox sqlEditor =
                FindControl<TextBox>(form, "txtSql");
            DataGridView bindings =
                FindControl<DataGridView>(form, "gridBindings");
            DataGridView history =
                FindControl<DataGridView>(form, "gridHistory");
            TabControl details =
                FindControl<TabControl>(form, "detailsTabs");
            Button approve =
                FindControl<Button>(form, "btnApprove");
            Button startCodex =
                FindControl<Button>(form, "btnStartCodex");
            Button help =
                FindControl<Button>(form, "btnHelp");
            Label explainWarning =
                FindControl<Label>(form, "lblExplainWarning");
            TableLayoutPanel inputLayout =
                FindControl<TableLayoutPanel>(form, "inputLayout");
            TableLayoutPanel optionsLayout =
                FindControl<TableLayoutPanel>(form, "optionsLayout");
            FlowLayoutPanel commandPanel =
                FindControl<FlowLayoutPanel>(form, "commandPanel");
            GroupBox codexSettings =
                FindControl<GroupBox>(form, "grpCodexExecutionSettings");
            ComboBox codexModel =
                FindControl<ComboBox>(form, "cmbCodexModel");
            ComboBox codexReasoning =
                FindControl<ComboBox>(form, "cmbCodexReasoning");
            ComboBox codexSpeed =
                FindControl<ComboBox>(form, "cmbCodexSpeed");
            Button refreshCodexModels =
                FindControl<Button>(form, "btnRefreshCodexModels");
            Label codexCatalogStatus =
                FindControl<Label>(form, "lblCodexCatalogStatus");

            Assert.Equal(
                ["KTGH00", "HPK210", "TSHIS"],
                hospitalArea.Items.Cast<string>().ToArray());
            Assert.Equal("KTGH00", hospitalArea.SelectedItem);
            Assert.Equal(
                ["バランス", "応答時間", "I/O", "計画安定性"],
                optimizationGoal.Items.Cast<string>().ToArray());
            Assert.Equal("バランス", optimizationGoal.SelectedItem);
            Assert.Equal(60m, timeout.Value);
            Assert.Equal(200m, previewRows.Value);
            Assert.Equal(10m, maxCandidates.Value);
            Assert.True(sqlEditor.Multiline);
            Assert.Equal(5, bindings.Columns.Count);
            Assert.Equal(8, history.Columns.Count);
            Assert.Equal(
                ["SQL差分", "EXPLAIN", "AI分析", "実測計画", "結果プレビュー", "Agent進捗"],
                details.TabPages
                    .Cast<TabPage>()
                    .Select(page => page.Text)
                    .ToArray());
            Assert.True(approve.Enabled);
            Assert.False(startCodex.Enabled);
            Assert.Equal("使い方", help.Text);
            Assert.Same(optionsLayout, help.Parent);
            Assert.Equal(optionsLayout.ColumnCount - 1, optionsLayout.GetColumn(help));
            Assert.Equal(0, optionsLayout.GetRow(help));
            Assert.Same(inputLayout, explainWarning.Parent);
            Assert.Equal(4, inputLayout.GetRow(explainWarning));
            Assert.DoesNotContain(
                explainWarning,
                commandPanel.Controls.Cast<Control>());
            Assert.Equal("Codex実行設定", codexSettings.Text);
            Assert.Same(inputLayout, codexSettings.Parent);
            Assert.Equal(1, inputLayout.GetRow(codexSettings));
            Assert.Equal(1, codexSettings.TabIndex);
            Assert.Equal(5, inputLayout.RowCount);
            Assert.Equal(ComboBoxStyle.DropDownList, codexModel.DropDownStyle);
            Assert.Equal(ComboBoxStyle.DropDownList, codexReasoning.DropDownStyle);
            Assert.Equal(ComboBoxStyle.DropDownList, codexSpeed.DropDownStyle);
            Assert.True(codexModel.TabIndex < codexReasoning.TabIndex);
            Assert.True(codexReasoning.TabIndex < codexSpeed.TabIndex);
            Assert.True(codexSpeed.TabIndex < refreshCodexModels.TabIndex);
            Assert.Equal("一覧更新", refreshCodexModels.Text);
            Assert.False(string.IsNullOrWhiteSpace(codexCatalogStatus.Text));
            Assert.True(form.SelectedCodexExecutionSelection.InheritsDefaults);

            // 1150820 codex しずくちゃん_gpt-5.6-sol：以Designer實體驗證方案A配置、動態能力選擇及僅執行中的設定鎖定。
            CodexModelOption dynamicModel = new(
                "future-id",
                "gpt-future",
                "GPT Future",
                "Future model",
                true,
                "future-high",
                [new CodexReasoningEffortOption("future-high", "Future effort")],
                [new CodexServiceTierOption("priority", "Fast", "Fast responses")],
                "default",
                null,
                null);
            form.SetUiState(SqlOptimizerFormState.Approved);
            form.SetCodexModelCatalog(
                new CodexModelCatalog([dynamicModel], DateTimeOffset.UtcNow),
                new CodexExecutionSelection(
                    "gpt-future",
                    "future-high",
                    "priority"),
                "1件取得");
            Assert.Equal(SqlOptimizerFormState.Approved, form.CurrentState);
            Assert.Equal("gpt-future", form.SelectedCodexExecutionSelection.Model);
            Assert.Equal("future-high", form.SelectedCodexExecutionSelection.ReasoningEffort);
            Assert.Equal("priority", form.SelectedCodexExecutionSelection.ServiceTier);
            Assert.Equal(
                ["標準 (default)", "Fast"],
                codexSpeed.Items.Cast<object>()
                    .Select(item => item.ToString() ?? string.Empty)
                    .ToArray());

            form.SetUiState(SqlOptimizerFormState.Running);
            Assert.False(codexModel.Enabled);
            Assert.False(codexReasoning.Enabled);
            Assert.False(codexSpeed.Enabled);
            Assert.False(refreshCodexModels.Enabled);
            form.SetUiState(SqlOptimizerFormState.Approved);
            Assert.True(codexModel.Enabled);
            Assert.True(codexReasoning.Enabled);
            Assert.True(codexSpeed.Enabled);
            Assert.True(refreshCodexModels.Enabled);

            foreach (SqlOptimizerFormState state in
                     Enum.GetValues<SqlOptimizerFormState>())
            {
                form.SetUiState(state, "Designer acceptance state check");
                Assert.True(
                    help.Enabled,
                    $"btnHelp must remain enabled in state {state}.");
            }

            form.SetUiState(SqlOptimizerFormState.Draft);
            VerifyMinimumSizeLayout(
                form,
                help,
                explainWarning,
                commandPanel,
                codexSettings,
                refreshCodexModels);

            sqlEditor.Text = "select 1 as VALUE from dual";
            Assert.True(
                form.TryCaptureInput(
                    out SqlOptimizerFormInput? input,
                    out string validationMessage),
                validationMessage);
            Assert.NotNull(input);
            Assert.Equal("KTGH00", input.HospitalArea);
            Assert.Equal("バランス", input.OptimizationGoal);
            Assert.Equal(60, input.CommandTimeoutSeconds);
            Assert.Equal(200, input.PreviewRowLimit);
            Assert.Equal(10, input.MaxCandidatesPerRound);
            Assert.Empty(input.Bindings);
        }
        finally
        {
            form.Dispose();
        }

        Assert.True(form.IsDisposed);
    }

    /// <summary>
    /// 驗證Form縮至MinimumSize時，既有警告與新增Codex設定列皆位於可視範圍且不互相重疊。
    /// </summary>
    /// <param name="form">已建立且可配置的frm_SqlOptimizer，不可為null。</param>
    /// <param name="help">optionsLayout內的btnHelp，不可為null。</param>
    /// <param name="explainWarning">輸入區底部的lblExplainWarning，不可為null。</param>
    /// <param name="commandPanel">承認與執行Button所在的commandPanel，不可為null。</param>
    /// <param name="codexSettings">方案A的grpCodexExecutionSettings，不可為null。</param>
    /// <param name="refreshCodexModels">Codex設定列中的btnRefreshCodexModels，不可為null。</param>
    /// <remarks>UI：只由VerifyFormCanBeConstructedAndDisposed直接使用，不啟動Controller。</remarks>
    private static void VerifyMinimumSizeLayout(
        Form form,
        Button help,
        Label explainWarning,
        FlowLayoutPanel commandPanel,
        GroupBox codexSettings,
        Button refreshCodexModels)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：以最小視窗尺寸確認新增設定列仍可由Designer安全配置。
        form.Size = form.MinimumSize;
        _ = form.Handle;
        PerformLayoutRecursively(form);
        PerformLayoutRecursively(form);

        Rectangle helpBounds = GetBoundsRelativeTo(help, form);
        Rectangle warningBounds = GetBoundsRelativeTo(explainWarning, form);
        Rectangle commandBounds = GetBoundsRelativeTo(commandPanel, form);
        Rectangle codexSettingsBounds =
            GetBoundsRelativeTo(codexSettings, form);

        Assert.True(
            form.ClientRectangle.Contains(helpBounds),
            $"btnHelp is outside the minimum-size client area. " +
            $"Button={helpBounds}, Client={form.ClientRectangle}.");
        Assert.True(
            help.Parent?.ClientRectangle.Contains(help.Bounds) == true,
            $"btnHelp is clipped by its parent at minimum size. Bounds={help.Bounds}.");
        Assert.True(
            help.Width >= 75 && help.Height >= 23,
            $"btnHelp is too small at minimum size: {help.Size}.");

        Assert.True(
            form.ClientRectangle.Contains(warningBounds),
            $"lblExplainWarning is outside the minimum-size client area. " +
            $"Warning={warningBounds}, Client={form.ClientRectangle}.");
        Assert.True(
            explainWarning.Parent?.ClientRectangle.Contains(explainWarning.Bounds) == true,
            $"lblExplainWarning is clipped by its parent at minimum size. " +
            $"Bounds={explainWarning.Bounds}.");
        Assert.False(
            warningBounds.IntersectsWith(commandBounds),
            "lblExplainWarning overlaps the command buttons at minimum size.");
        Assert.True(
            form.ClientRectangle.Contains(codexSettingsBounds),
            $"grpCodexExecutionSettings is outside the minimum-size client area. Bounds={codexSettingsBounds}.");
        Assert.True(
            refreshCodexModels.Parent?.ClientRectangle.Contains(
                refreshCodexModels.Bounds) == true,
            $"btnRefreshCodexModels is clipped at minimum size. Bounds={refreshCodexModels.Bounds}.");

        Size measuredWarning = TextRenderer.MeasureText(
            explainWarning.Text,
            explainWarning.Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix);
        int availableWidth =
            explainWarning.ClientSize.Width - explainWarning.Padding.Horizontal;
        int availableHeight =
            explainWarning.ClientSize.Height - explainWarning.Padding.Vertical;
        Assert.True(
            measuredWarning.Width <= availableWidth &&
            measuredWarning.Height <= availableHeight,
            $"lblExplainWarning text is clipped at minimum size. " +
            $"Required={measuredWarning}, Available={new Size(availableWidth, availableHeight)}.");
    }

    private static void PerformLayoutRecursively(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls)
        {
            PerformLayoutRecursively(child);
        }
    }

    private static Rectangle GetBoundsRelativeTo(
        Control control,
        Control ancestor)
    {
        Point location = control.Location;
        Control? parent = control.Parent;
        while (parent is not null && parent != ancestor)
        {
            location.Offset(parent.Location);
            parent = parent.Parent;
        }

        Assert.Same(ancestor, parent);
        return new Rectangle(location, control.Size);
    }

    private static TControl FindControl<TControl>(
        Control root,
        string name)
        where TControl : Control
    {
        Control[] matches = root.Controls.Find(name, searchAllChildren: true);
        Assert.Single(matches);
        return Assert.IsType<TControl>(matches[0]);
    }
}
