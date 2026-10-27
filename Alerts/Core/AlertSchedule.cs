using INOP1101.cls;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Oracle;

namespace Project_auto_push_to_gitlab.Alerts.Core;

/// <summary>計算台北每週、每天及每小時排程與日期參數；不依賴主機時區，不存取DB或UI。</summary>
/// <remarks>UI：FrmAlertRule的cmbFrequency、cmbDay及dtpTime經AlertService間接提供輸入；FrmAlerts的dgvRules顯示下一次執行。</remarks>
public static class AlertSchedule
{
    // 1151005 codex GPT-6_しずくちゃん：固定台北時區及三院區執行順序。
    public static TimeZoneInfo Taipei { get; } = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
    public static IReadOnlyList<string> CampusOrder { get; } = ["KTGH00", "HPK210", "TSHIS"];

    /// <summary>依規則頻率計算參考時間以前或當時最近一次的台北預定；每小時以整點加指定分鐘計算。</summary>
    /// <param name="rule">已驗證的規則，不可為null；不修改頻率、星期或時間。</param>
    /// <param name="now">任意時區的參考時間，先轉換為台北時間。</param>
    /// <returns>不晚於now的台北預定時間；恰好到期時回傳當次預定。</returns>
    /// <exception cref="ArgumentNullException">rule為null時拋出。</exception>
    /// <exception cref="ArgumentException">排程頻率未定義時拋出。</exception>
    /// <remarks>UI：FrmAlertRule的cmbFrequency、cmbDay及dtpTime經AlertService間接使用；背景排程依回傳時間更新FrmAlerts的dgvResults與dgvHistory。</remarks>
    public static DateTimeOffset Latest(AlertRule rule, DateTimeOffset now)
    {
        // 台北當地的日界及整點作為起點，補跑仍沿用實際預定而非主機時間。
        ArgumentNullException.ThrowIfNull(rule);
        DateTime local = TimeZoneInfo.ConvertTime(now, Taipei).DateTime;
        DateTime date = local.Date.Add(rule.Time);
        TimeSpan interval = TimeSpan.FromDays(1);
        switch (rule.Frequency)
        {
            case AlertScheduleFrequency.Weekly:
                int days = ((int)local.DayOfWeek - (int)rule.Day + 7) % 7;
                date = date.AddDays(-days);
                interval = TimeSpan.FromDays(7);
                break;
            case AlertScheduleFrequency.Daily:
                break;
            case AlertScheduleFrequency.Hourly:
                date = local.Date.AddHours(local.Hour).Add(rule.Time);
                interval = TimeSpan.FromHours(1);
                break;
            default:
                throw new ArgumentException("排程頻率無效。", nameof(rule));
        }
        if (date > local) date = date.Subtract(interval);
        return new DateTimeOffset(date, Taipei.GetUtcOffset(date));
    }

    /// <summary>沿用最近預定的計算，加上規則頻率的週、日或小時間隔，取得下一次台北預定。</summary>
    /// <param name="rule">已驗證的規則，不可為null；不修改內容。</param>
    /// <param name="now">任意時區的參考時間；當次已到期時仍回傳下一次。</param>
    /// <returns>嚴格晚於now的台北預定；不存取設定或執行查詢。</returns>
    /// <exception cref="ArgumentNullException">rule為null時拋出。</exception>
    /// <exception cref="ArgumentException">排程頻率未定義時拋出。</exception>
    /// <remarks>UI：FrmAlerts的dgvRules直接顯示下次執行；輸入由FrmAlertRule的cmbFrequency、cmbDay及dtpTime經AlertService保存。</remarks>
    public static DateTimeOffset Next(AlertRule rule, DateTimeOffset now)
    {
        DateTimeOffset latest = Latest(rule, now);
        return rule.Frequency switch
        {
            AlertScheduleFrequency.Weekly => latest.AddDays(7),
            AlertScheduleFrequency.Daily => latest.AddDays(1),
            AlertScheduleFrequency.Hourly => latest.AddHours(1),
            _ => throw new ArgumentException("排程頻率無效。", nameof(rule))
        };
    }

    /// <summary>產生去重識別鍵，包含固定ID及完整設定版本。</summary>
    /// <param name="rule">規則，不修改內容。</param><returns>不包含SQL及參數的識別鍵。</returns>
    /// <remarks>UI：無；AlertService及儲存實作呼叫。</remarks>
    public static string Key(AlertRule rule)
    {
        // 1151005 codex GPT-6_しずくちゃん：設定版ごとの重複防止キーを作ります。
        return $"{rule.Id:N}:{rule.Version}";
    }

    /// <summary>依台北基準日建立具名參數，計算週界與當月首末日；固定值沿用既有型別轉換，日期以字串Bind。</summary>
    /// <param name="rule">不可為null的規則；來源、日期格式及參數內容均不變更。</param>
    /// <param name="scheduled">含時區的日期計算起點；排程及補跑使用原預定，手動與測試使用排入佇列前的操作時刻。</param>
    /// <returns>依原順序回傳型別已轉換的參數List；自動日期使用民國7碼或西元8碼，月末來源包含該月最後一天；未定義參數時為空List，不回傳null。</returns>
    /// <exception cref="FormatException">固定值格式不符合型別時拋出。</exception>
    /// <remarks>UI：FrmAlertRule的dgvParameters之colSource及colFormat經AlertService間接使用；btnTest與FrmAlerts的btnRun或背景排程決定基準時刻。只建立Bind值，不變更SQL比較條件或操作UI、DB。</remarks>
    public static List<SqlBindParameter> Bind(AlertRule rule, DateTimeOffset scheduled)
    {
        // 1151005 codex GPT-6_しずくちゃん：既存変換を使い予定日から週境界の値を作ります。
        DateTime date = TimeZoneInfo.ConvertTime(scheduled, Taipei).Date;
        DateTime monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        return rule.Parameters.Select(parameter =>
        {
            object? value;
            if (parameter.Source == AlertParameterSource.Fixed)
                value = parameter.IsNull ? null : OracleBindValueConverter.Convert(parameter.DataType, parameter.Value ?? "");
            else
            {
                DateTime source = parameter.Source switch
                {
                    AlertParameterSource.PreviousWeekMonday => monday.AddDays(-7),
                    AlertParameterSource.ThisWeekMonday => monday,
                    // 1151027 codex しずくちゃん：以台北基準日計算當月邊界，月末包含最後一天並支援閏年。
                    AlertParameterSource.ThisMonthFirstDay => new DateTime(date.Year, date.Month, 1),
                    AlertParameterSource.ThisMonthLastDay => new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)),
                    _ => date
                };
                value = parameter.DateFormat == AlertDateFormat.RepublicOfChina
                    ? source.民國日期().PadLeft(7, '0') : source.西元日期YYYYMMDD();
            }
            return new SqlBindParameter(parameter.Name, parameter.DataType, value,
                parameter.Source == AlertParameterSource.Fixed && parameter.IsNull, parameter.Size);
        }).ToList();
    }

    /// <summary>驗證規則必要欄位、院區、排程頻率、時間範圍及SQL參數；每小時的時間只能是整點後的分鐘。</summary>
    /// <param name="rule">待驗證規則，不可為null；不變更內容或連線DB。</param>
    /// <exception cref="ArgumentNullException">rule為null時拋出。</exception>
    /// <exception cref="ArgumentException">頻率、星期、時間、必要欄位、院區或自動參數型別不合法時拋出。</exception>
    /// <exception cref="SqlValidationException">SQL不是單一SELECT或參數名稱不一致時拋出。</exception>
    /// <remarks>UI：FrmAlertRule的btnSave及btnTest經AlertService間接使用；排程輸入來自cmbFrequency、cmbDay及dtpTime，錯誤顯示lblMessage。</remarks>
    public static void Validate(AlertRule rule)
    {
        // 舊規則預設每週；新增頻率仍共用原有SQL及具名參數驗證。
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.Id == Guid.Empty || rule.Version < 1 || string.IsNullOrWhiteSpace(rule.Name)
            || string.IsNullOrWhiteSpace(rule.Purpose) || string.IsNullOrWhiteSpace(rule.Category))
            throw new ArgumentException("請填寫警告名稱、分類及用途說明。");
        if (!Enum.IsDefined(rule.Frequency) || !Enum.IsDefined(rule.Day)
            || rule.Time < TimeSpan.Zero || rule.Time >= TimeSpan.FromDays(1)
            || (rule.Frequency == AlertScheduleFrequency.Hourly
                && (rule.Time >= TimeSpan.FromHours(1) || rule.Time.Ticks % TimeSpan.TicksPerMinute != 0)))
            throw new ArgumentException("排程日期或時間無效。");
        if (rule.Campuses.Count == 0 || rule.Campuses.Distinct().Count() != rule.Campuses.Count
            || rule.Campuses.Any(campus => !CampusOrder.Contains(campus)))
            throw new ArgumentException("請選擇至少一個有效院區。");
        foreach (AlertParameter parameter in rule.Parameters)
        {
            if (!Enum.IsDefined(parameter.Source) || !Enum.IsDefined(parameter.DateFormat)
                || !Enum.IsDefined(parameter.DataType)) throw new ArgumentException("參數設定無效。");
            if (parameter.Source != AlertParameterSource.Fixed && parameter.DataType != OracleBindDataType.Varchar2)
                throw new ArgumentException("自動日期參數必須使用Varchar2字串型別。");
        }
        OracleSelectStatementValidator validator = new();
        validator.EnsureBindingsMatch(validator.Validate(rule.Sql), Bind(rule, DateTimeOffset.UtcNow));
    }
}
