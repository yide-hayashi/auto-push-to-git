using System.Data;
using System.Globalization;
using Project_auto_push_to_gitlab.Alerts.Application;
using Project_auto_push_to_gitlab.Alerts.Core;

namespace Project_auto_push_to_gitlab.Alerts.Presentation;

/// <summary>建立告警Grid專用的獨立顯示資料及隱藏排序鍵，保留原始顯示文字、欄位順序與固定識別碼。</summary>
/// <remarks>UI：FrmAlerts的dgvResults、dgvRules、dgvHistory及dgvDetails經資料繫結與表頭事件間接使用；不持有Control、不查詢DB、不修改規則或保存資料。</remarks>
internal static class AlertGridData
{
    /// <summary>將各規則最新執行結果投影成可依狀態、件數及真正執行時間排序的顯示資料。</summary>
    /// <param name="runs">不可為null的執行快照序列；保持原順序且不修改快照。</param>
    /// <returns>包含原顯示欄位、Guid識別碼與隱藏排序鍵的獨立DataTable；無結果時仍保留欄位，不回傳null。</returns>
    /// <exception cref="ArgumentNullException">runs為null時拋出。</exception>
    /// <remarks>UI：FrmAlerts的dgvResults經Render間接使用；執行選擇由Id維持，日期顯示台北分鐘，院區文字沿用AlertController.Status。</remarks>
    internal static DataTable Results(IEnumerable<AlertRun> runs)
    {
        // 1151027 codex しずくちゃん：建立最新結果的獨立顯示表，避免字串件數及格式化日期造成錯誤排序。
        ArgumentNullException.ThrowIfNull(runs);
        DataTable table = CreateRunTable(false);
        foreach (AlertRun run in runs) AddRunRow(table, run, false);
        return table;
    }

    /// <summary>將規則快照投影成可排序的管理清單，停用規則的下次執行在兩個方向皆排最後。</summary>
    /// <param name="rules">不可為null的規則序列；不修改規則、院區清單或傳入順序。</param>
    /// <param name="now">計算全部規則下次預定的同一參考時刻，允許任意UTC偏移。</param>
    /// <returns>保留Boolean啟用欄、台北預定顯示及固定Id的獨立DataTable；無規則時仍保留欄位，不回傳null。</returns>
    /// <exception cref="ArgumentNullException">rules為null時拋出。</exception>
    /// <exception cref="ArgumentException">啟用規則的排程頻率無效，既有AlertSchedule.Next無法計算時拋出。</exception>
    /// <remarks>UI：FrmAlerts的dgvRules經Render間接使用；btnEdit、btnDuplicate及btnToggle仍依Id選擇，不將排序寫回設定。</remarks>
    internal static DataTable Rules(IEnumerable<AlertRule> rules, DateTimeOffset now)
    {
        // 1151027 codex しずくちゃん：保留規則顯示契約，另以固定停用順位與實際預定時間排序。
        ArgumentNullException.ThrowIfNull(rules);
        DataTable table = CreateTable();
        table.Columns.Add("Id", typeof(Guid));
        foreach (string name in new[] { "名稱", "分類", "用途", "院區" }) table.Columns.Add(name, typeof(string));
        table.Columns.Add("啟用", typeof(bool));
        table.Columns.Add("下次執行", typeof(string));
        AddHiddenColumn(table, "__disabled", typeof(int));
        AddHiddenColumn(table, "__next", typeof(DateTime));
        AddHiddenColumn(table, "__order", typeof(int));
        table.Columns["下次執行"]!.ExtendedProperties["SortKeys"] = new[] { "__next" };
        table.Columns["下次執行"]!.ExtendedProperties["FixedAscendingKeys"] = new[] { "__disabled" };
        foreach (AlertRule rule in rules)
        {
            DateTimeOffset? next = rule.Enabled ? AlertSchedule.Next(rule, now) : null;
            DataRow row = table.NewRow();
            row["Id"] = rule.Id;
            row["名稱"] = Value(rule.Name);
            row["分類"] = Value(rule.Category);
            row["用途"] = Value(rule.Purpose);
            row["院區"] = string.Join("、", rule.Campuses);
            row["啟用"] = rule.Enabled;
            row["下次執行"] = next?.ToString("yyyy/MM/dd HH:mm") ?? "停用";
            row["__disabled"] = rule.Enabled ? 0 : 1;
            row["__next"] = next.HasValue ? next.Value.UtcDateTime : DBNull.Value;
            row["__order"] = table.Rows.Count;
            table.Rows.Add(row);
        }
        return table;
    }

    /// <summary>將執行履歷投影成可依真正執行與排程時間、院區狀態及件數排序的清單。</summary>
    /// <param name="runs">不可為null的履歷快照序列；不修改資料及來源順序。</param>
    /// <returns>保持原履歷顯示欄位與通知文字的獨立DataTable；無履歷時仍保留欄位，不回傳null。</returns>
    /// <exception cref="ArgumentNullException">runs為null時拋出。</exception>
    /// <remarks>UI：FrmAlerts的dgvHistory經Render間接使用；雙擊履歷仍使用原Guid，日期顯示精度與既有畫面一致。</remarks>
    internal static DataTable History(IEnumerable<AlertRun> runs)
    {
        // 1151027 codex しずくちゃん：履歷排序只改變顯示檢視，不修改原保存順序及取得時點快照。
        ArgumentNullException.ThrowIfNull(runs);
        DataTable table = CreateRunTable(true);
        foreach (AlertRun run in runs) AddRunRow(table, run, true);
        return table;
    }

    /// <summary>建立目前明細頁的文字顯示表，只對已確認數值型且全欄可解析的內容加入精確數值順位。</summary>
    /// <param name="columns">不可為null的原始欄位中繼資料；使用位置識別欄位，保留重複名稱，不修改內容。</param>
    /// <param name="rows">不可為null的目前頁明細；每列值數必須等於columns數，null值代表DB NULL，不修改原陣列。</param>
    /// <returns>顯示欄名為column0起的獨立DataTable，Caption保存原始欄名；值保持原文字與DBNull，無資料仍有欄位，不回傳null。</returns>
    /// <exception cref="ArgumentNullException">columns或rows為null時拋出。</exception>
    /// <exception cref="ArgumentException">任一明細列的值數不等於欄位數時拋出。</exception>
    /// <remarks>UI：FrmAlerts的dgvDetails經RenderDetailsAsync間接使用；排序範圍僅傳入頁面，未知型別或任一數值不可解析時整欄維持文字排序。隱藏鍵不改變原顯示值及NULL。</remarks>
    internal static DataTable Details(IReadOnlyList<AlertColumn> columns, IReadOnlyList<string?[]> rows)
    {
        // 1151027 codex しずくちゃん：依位置保留明細欄位，數值排序另外建立順位，避免改變顯示格式及數值精度。
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        DataTable table = CreateTable();
        for (int index = 0; index < columns.Count; index++)
            table.Columns.Add("column" + index, typeof(string)).Caption = columns[index].Name;
        AddHiddenColumn(table, "__order", typeof(int));
        foreach (string?[] values in rows)
        {
            if (values.Length != columns.Count) throw new ArgumentException("明細值數必須等於欄位數。", nameof(rows));
            DataRow row = table.NewRow();
            for (int index = 0; index < values.Length; index++) row[index] = Value(values[index]);
            row["__order"] = table.Rows.Count;
            table.Rows.Add(row);
        }
        for (int index = 0; index < columns.Count; index++)
        {
            if (!IsNumericType(columns[index].DataType)) continue;
            string?[] values = rows.Select(row => row[index]).ToArray();
            if (!ExactNumericSort.TryGetRanks(values, out int?[] ranks)) continue;
            string key = "__number" + index;
            AddHiddenColumn(table, key, typeof(int));
            table.Columns["column" + index]!.ExtendedProperties["SortKeys"] = new[] { key };
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) table.Rows[rowIndex][key] = Value(ranks[rowIndex]);
        }
        return table;
    }

    /// <summary>依指定顯示欄位更新DataView排序，將特殊欄位轉為其隱藏比較鍵並保持同值的原始順序。</summary>
    /// <param name="table">不可為null且由本類別建立的顯示表；只變更DefaultView.Sort，不改變DataRow值或原始Rows順序。</param>
    /// <param name="columnName">實際顯示欄位內部名稱，不可為null、空字串、缺少欄位或以雙底線起始的隱藏鍵。</param>
    /// <param name="descending">True為降冪、False為升冪；停用順位及同值原始順位固定升冪。</param>
    /// <exception cref="ArgumentNullException">table或columnName為null時拋出。</exception>
    /// <exception cref="ArgumentException">指定欄位不是本表可排序的顯示欄位，或缺少原始順位時拋出。</exception>
    /// <remarks>UI：FrmAlerts的四個Grid經ColumnHeaderMouseClick間接使用；NULL依DataView規則升冪在先、降冪在後，停用規則不論方向皆置尾。不使用使用者輸入組成排序運算式。</remarks>
    internal static void Sort(DataTable table, string columnName, bool descending)
    {
        // 1151027 codex しずくちゃん：從受控欄位對應建立排序運算式，使用元順作為固定最後比較鍵。
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columnName);
        DataColumn? column = table.Columns[columnName];
        if (column is null || columnName.StartsWith("__", StringComparison.Ordinal) || !table.Columns.Contains("__order"))
            throw new ArgumentException("只能排序顯示表中的有效欄位。", nameof(columnName));
        List<string> expressions = [];
        if (column.ExtendedProperties["FixedAscendingKeys"] is string[] fixedKeys)
            expressions.AddRange(fixedKeys.Select(key => Quote(key) + " ASC"));
        string[] keys = column.ExtendedProperties["SortKeys"] as string[] ?? [column.ColumnName];
        expressions.AddRange(keys.Select(key => Quote(key) + (descending ? " DESC" : " ASC")));
        expressions.Add("[__order] ASC");
        table.DefaultView.Sort = string.Join(", ", expressions);
    }

    /// <summary>建立使用目前文化及區分大小寫文字比較的空顯示表。</summary>
    /// <returns>新的空DataTable，不回傳null；尚未附加顯示欄位或排序鍵。</returns>
    /// <remarks>UI：無；Results、Rules、History及Details間接支援FrmAlerts的四個Grid，不建立檔案或連線。</remarks>
    private static DataTable CreateTable()
    {
        // 1151027 codex しずくちゃん：明確保留目前文化的文字比較，避免排序依主機預設資料表設定漂移。
        return new DataTable { Locale = CultureInfo.CurrentCulture, CaseSensitive = true };
    }

    /// <summary>建立最新結果或完整履歷的共用欄位配置，所有可見欄位先於排序鍵。</summary>
    /// <param name="history">True建立完整履歷欄位；False建立最新結果欄位，不變更。</param>
    /// <returns>尚無資料列、具有原始顯示順序及排序對應的獨立DataTable，不回傳null。</returns>
    /// <remarks>UI：無；Results及History間接支援FrmAlerts的dgvResults與dgvHistory，Id留給Form隱藏及選取復原。</remarks>
    private static DataTable CreateRunTable(bool history)
    {
        // 1151027 codex しずくちゃん：共用兩個執行清單的欄位契約與日期、院區比較鍵。
        DataTable table = CreateTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("名稱", typeof(string));
        table.Columns.Add("分類", typeof(string));
        if (!history) table.Columns.Add("用途", typeof(string));
        table.Columns.Add("執行時間", typeof(string));
        if (history)
            foreach (string name in new[] { "排程時間", "執行方式", "狀態" }) table.Columns.Add(name, typeof(string));
        foreach (string campus in AlertSchedule.CampusOrder) table.Columns.Add(campus, typeof(string));
        if (history) table.Columns.Add("通知", typeof(string));
        AddHiddenColumn(table, "__started", typeof(DateTime));
        table.Columns["執行時間"]!.ExtendedProperties["SortKeys"] = new[] { "__started" };
        if (history)
        {
            AddHiddenColumn(table, "__scheduled", typeof(DateTime));
            table.Columns["排程時間"]!.ExtendedProperties["SortKeys"] = new[] { "__scheduled" };
        }
        foreach (string campus in AlertSchedule.CampusOrder)
        {
            string statusKey = "__" + campus + "Status";
            string countKey = "__" + campus + "Count";
            AddHiddenColumn(table, statusKey, typeof(int));
            AddHiddenColumn(table, countKey, typeof(long));
            table.Columns[campus]!.ExtendedProperties["SortKeys"] = new[] { statusKey, countKey };
        }
        AddHiddenColumn(table, "__order", typeof(int));
        return table;
    }

    /// <summary>新增一筆執行顯示列，將原日期及院區狀態保留為獨立比較鍵。</summary>
    /// <param name="table">CreateRunTable建立的非null顯示表；方法新增一筆DataRow，不修改現有列。</param>
    /// <param name="run">非null的執行快照，不修改內容。</param>
    /// <param name="history">必須與table的欄位配置相符，True為履歷、False為最新結果。</param>
    /// <remarks>UI：無；Results及History間接輸出至FrmAlerts的dgvResults與dgvHistory，狀態文字使用既有AlertController.Status。</remarks>
    private static void AddRunRow(DataTable table, AlertRun run, bool history)
    {
        // 1151027 codex しずくちゃん：顯示維持既有格式，排序以元日期、狀態及實際件數為準。
        DataRow row = table.NewRow();
        row["Id"] = run.Id;
        row["名稱"] = Value(run.Name);
        row["分類"] = Value(run.Category);
        if (!history) row["用途"] = Value(run.Purpose);
        row["執行時間"] = TimeZoneInfo.ConvertTime(run.StartedAt, AlertSchedule.Taipei).ToString(history ? "yyyy/MM/dd HH:mm:ss" : "yyyy/MM/dd HH:mm");
        row["__started"] = run.StartedAt.UtcDateTime;
        if (history)
        {
            row["排程時間"] = TimeZoneInfo.ConvertTime(run.ScheduledAt, AlertSchedule.Taipei).ToString("yyyy/MM/dd HH:mm");
            row["__scheduled"] = run.ScheduledAt.UtcDateTime;
            row["執行方式"] = run.Manual ? "手動" : "排程";
            row["狀態"] = run.Status == AlertRunStatus.Completed ? "已完成" : run.Status == AlertRunStatus.Running ? "執行中" : "已中斷";
            row["通知"] = run.NotificationError is null ? "" : "未送出";
        }
        foreach (string campus in AlertSchedule.CampusOrder)
        {
            AlertCampusResult? result = run.Campuses.Find(item => item.Campus == campus);
            row[campus] = AlertController.Status(result);
            row["__" + campus + "Status"] = result?.Status switch
            {
                AlertCampusStatus.Normal => 1,
                AlertCampusStatus.Anomaly => 2,
                AlertCampusStatus.Pending => 3,
                AlertCampusStatus.Interrupted => 4,
                AlertCampusStatus.Error => 5,
                _ => 0
            };
            row["__" + campus + "Count"] = result?.Status == AlertCampusStatus.Anomaly ? result.RowCount : 0L;
        }
        row["__order"] = table.Rows.Count;
        table.Rows.Add(row);
    }

    /// <summary>加入只能作為內部排序鍵的隱藏欄位，不影響既有可見欄位順序。</summary>
    /// <param name="table">非null的顯示表，方法新增一個欄位。</param>
    /// <param name="name">以雙底線起始的固定內部欄名，不可為null或重複。</param>
    /// <param name="type">非null的排序值CLR型別，不修改。</param>
    /// <remarks>UI：無；本類別建表流程間接支援FrmAlerts的四個Grid；Form仍依雙底線名稱明確隱藏內部欄。</remarks>
    private static void AddHiddenColumn(DataTable table, string name, Type type)
    {
        // 1151027 codex しずくちゃん：隱藏排序鍵只存在記憶體顯示表，不加入業務保存模型。
        table.Columns.Add(name, type).ColumnMapping = MappingType.Hidden;
    }

    /// <summary>確認保存中繼資料是否明確表示Oracle或CLR數值型別，不從資料內容猜測型別。</summary>
    /// <param name="typeName">保存的型別名稱；允許null、空字串及大小寫差異，不修改。</param>
    /// <returns>明確列入數值白名單時為True；未知、空白及文字型別為False，維持文字排序。</returns>
    /// <remarks>UI：無；Details間接支援FrmAlerts的dgvDetails，文字代碼即使只含數字也不改為數值排序。</remarks>
    private static bool IsNumericType(string? typeName)
    {
        // 1151027 codex しずくちゃん：只接受可確認的數值型別名稱，避免把有前導零的代碼誤判為數字。
        return typeName?.Trim().ToUpperInvariant() is "NUMBER" or "NUMERIC" or "DECIMAL" or "ORACLEDECIMAL"
            or "FLOAT" or "BINARY_FLOAT" or "BINARY_DOUBLE" or "BINARYFLOAT" or "BINARYDOUBLE"
            or "INTEGER" or "INT" or "SMALLINT" or "BYTE" or "SBYTE" or "INT16" or "UINT16"
            or "INT32" or "UINT32" or "INT64" or "UINT64" or "SINGLE" or "DOUBLE"
            or "SYSTEM.BYTE" or "SYSTEM.SBYTE" or "SYSTEM.INT16" or "SYSTEM.UINT16"
            or "SYSTEM.INT32" or "SYSTEM.UINT32" or "SYSTEM.INT64" or "SYSTEM.UINT64"
            or "SYSTEM.SINGLE" or "SYSTEM.DOUBLE" or "SYSTEM.DECIMAL";
    }

    /// <summary>將可空顯示值轉為DataTable使用的DBNull，不改變非空值內容或型別。</summary>
    /// <param name="value">顯示文字或排序值；允許null，不修改傳入物件。</param>
    /// <returns>null回傳DBNull.Value，其他值原樣回傳，不回傳null。</returns>
    /// <remarks>UI：無；本類別投影方法間接提供FrmAlerts四個Grid的值，避免NULL變成空字串。</remarks>
    private static object Value(object? value)
    {
        // 1151027 codex しずくちゃん：保留DB NULL語意，與空字串及數字零分開。
        return value ?? DBNull.Value;
    }

    /// <summary>引用已確認存在的內部欄名，避免特殊字元被DataView當成排序語法。</summary>
    /// <param name="name">DataColumn實際欄名或本類別的固定排序鍵，不可為null，不修改。</param>
    /// <returns>以方括號引用並逸出反斜線及右括號的欄名，不回傳null或空字串。</returns>
    /// <remarks>UI：無；Sort間接支援FrmAlerts四個Grid的表頭事件，不接收SQL或未驗證的使用者運算式。</remarks>
    private static string Quote(string name)
    {
        // 1151027 codex しずくちゃん：引用欄位識別碼，保持排序字串只含既有欄位及固定方向。
        return "[" + name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal) + "]";
    }
}
