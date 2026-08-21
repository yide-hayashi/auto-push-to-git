using Project_auto_push_to_gitlab.SqlOptimizer.Codex;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Mcp;
using Project_auto_push_to_gitlab.SqlOptimizer.Oracle;
using Project_auto_push_to_gitlab.SqlOptimizer.Persistence;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Application
{
    /// <summary>
    /// 作為獨立SQL最佳化功能的Composition Root，集中管理工作階段、Oracle、MCP與Codex服務生命週期。
    /// </summary>
    public sealed class SqlOptimizerApplicationRuntime : IAsyncDisposable
    {
        private readonly JsonOptimizationSessionStore _store;
        private readonly SqlOptimizationService _sessionService;
        private readonly SqlOptimizerTokenStore _tokenStore;
        private readonly SqlOptimizerMcpHost _mcpHost;
        private readonly CodexCliRunner _cliRunner;
        private readonly CodexCliDiagnosticsService _diagnostics;
        private readonly CodexModelCatalogService _modelCatalogService;
        private readonly CodexExecutionSettingsStore _codexExecutionSettingsStore;
        private readonly CodexMcpConfigurationService _configuration;
        private readonly OracleOptimizerReadinessProbe _oracleReadiness;
        private bool _disposed;

        /// <summary>
        /// 建立SQL最佳化功能所需的儲存、Oracle檢查、MCP主機與Codex服務。
        /// </summary>
        /// <remarks>
        /// UI：由主程式開啟frm_SqlOptimizer時直接建立；本建構式不啟動Codex或連線Oracle。
        /// Codex模型清單與前次選擇會使用同一個SqlOptimizer LocalAppData目錄。
        /// </remarks>
        public SqlOptimizerApplicationRuntime()
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：集中建立動態模型目錄與選擇值儲存服務，供畫面生命週期共用。
            _store = new JsonOptimizationSessionStore();
            OracleSelectStatementValidator validator =
                new OracleSelectStatementValidator();
            OracleSqlPlanEvaluator evaluator = new OracleSqlPlanEvaluator(
                new KtccOracleConnectionFactory(),
                validator);
            _oracleReadiness = new OracleOptimizerReadinessProbe(evaluator);
            _sessionService = new SqlOptimizationService(
                _store,
                evaluator,
                validator);

            _tokenStore = new SqlOptimizerTokenStore(_store.StorageDirectory);
            _mcpHost = new SqlOptimizerMcpHost(
                _sessionService,
                _tokenStore);

            CodexCliLocator locator = new CodexCliLocator();
            _cliRunner = new CodexCliRunner(locator, _tokenStore);
            _diagnostics = new CodexCliDiagnosticsService(locator);
            _modelCatalogService = new CodexModelCatalogService(locator);
            _codexExecutionSettingsStore =
                new CodexExecutionSettingsStore(_store.StorageDirectory);
            _configuration = new CodexMcpConfigurationService(locator);

            CodexWorkingDirectory = Path.Combine(
                _store.StorageDirectory,
                "CodexWork");
        }

        public string CodexWorkingDirectory { get; }

        public string? McpStartupError { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Directory.CreateDirectory(CodexWorkingDirectory);

            try
            {
                await _mcpHost.StartAsync(cancellationToken)
                    .ConfigureAwait(false);
                McpStartupError = null;

                if (_mcpHost.IsRunning)
                {
                    try
                    {
                        await _configuration.EnsureConfiguredAsync(
                                _mcpHost.Endpoint,
                                cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (
                        cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        McpStartupError ??= exception.Message;
                    }
                }
            }
            catch (Exception exception)
            {
                // The legacy monitoring screen must remain available even when
                // the optional local MCP endpoint cannot start.
                McpStartupError = exception.Message;
            }
        }

        /// <summary>
        /// 建立frm_SqlOptimizer並注入工作階段、Codex動態模型與執行所需Controller服務。
        /// </summary>
        /// <returns>已連結SqlOptimizerUiController且可顯示的全新frm_SqlOptimizer。</returns>
        /// <remarks>
        /// UI：由主程式開啟SQL最佳化功能時直接使用；每次呼叫建立新的Form與Controller。
        /// </remarks>
        public frm_SqlOptimizer CreateOptimizerForm()
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：將動態模型目錄與選擇Store一併注入畫面Controller。
            ObjectDisposedException.ThrowIf(_disposed, this);
            frm_SqlOptimizer form = new frm_SqlOptimizer();
            _ = new SqlOptimizerUiController(
                form,
                _sessionService,
                _store,
                _mcpHost,
                _cliRunner,
                _diagnostics,
                _modelCatalogService,
                _codexExecutionSettingsStore,
                _configuration,
                _oracleReadiness,
                CodexWorkingDirectory,
                McpStartupError);
            return form;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _cliRunner.DisposeAsync().ConfigureAwait(false);
            await _mcpHost.DisposeAsync().ConfigureAwait(false);
        }
    }
}
