# 三院區SQL告警

## 使用方式

從主畫面功能選單或通知區域的「三院區SQL告警」開啟B案總覽。「告警結果」顯示搜尋範圍內每個規則的最新結果，三張卡片合計各院區異常筆數，另列取得失敗及尚未完成的規則數。

Visual Studio的F5啟動目前專案時顯示`frm_SessionGrid`。若已有常駐實例，F5不會轉送到舊版，而會要求先在通知區域選單按「Close」正式結束後重試；視窗的×只會隱藏。一般重複起動顯示既有主畫面，只有通知履歷Guid才直接開啟告警履歷。一般首次起動仍維持通知區域常駐。

「SQL規則管理」可以新增、編輯、複製、啟用／停用及立即執行。新增SQL從空白開始，複製保留來源設定並產生新ID，第一次儲存一律停用。儲存後明確啟用，才從下一個台北排程時間開始。修改規則會建立新版本並從下次排程開始，執行中的查詢仍使用開始時的快照。

「測試查詢」使用目前編輯輸入，經相同的唯讀執行佇列查詢並保存手動履歷，不自動儲存或變更規則。「執行紀錄」保留當時名稱、分類、預定及實際時間，雙擊列開啟原取得明細。正常零筆、異常、取得失敗、執行中及中斷均分開顯示。

院區明細按原SQL欄位順序顯示；重複名稱依欄位位置識別，DB NULL顯示「NULL」。每頁200筆，完整結果以500筆分割保存，不會把結果截斷成預覽。

「告警結果」、「SQL規則管理」、「執行紀錄」及下方明細均可點選欄位標題排序；首次點選為升冪，重複點選切換升冪／降冪，標題箭頭會顯示方向。搜尋或背景更新後，各清單仍沿用目前開啟畫面所選的排序；關閉程式後不保存。排序時維持選取的告警識別碼。

院區狀態先依「未執行、正常、異常、執行中、已中斷、取得失敗」排列；異常依件數以數值排序，例如2筆排在10筆之前。「停用」在下次執行排序中固定列於最後。下方明細只排序目前顯示的一頁（最多200筆）：可確認為數值的欄位按數值比較，其他欄位按文字比較；包含無法解析值的欄位會整欄使用文字順序。NULL升冪在前、降冪在後，相同值維持原順序。明細排序不改變保存資料，也不影響其他頁。

## SQL及參數

- 院區順序：KTGH00、HPK210、TSHIS。至少選一院區，一院區失敗仍繼續其他院區。
- SQL只允許一個SELECT或WITH … SELECT，不允許DML、DDL、PL/SQL或FOR UPDATE。沿用現有SELECT驗證及型別轉換；所有值使用具名Bind，參數與SQL中的名稱必須一致。
- `Fixed`使用指定Oracle型別、固定值、NULL及可選長度。數值使用不變文化格式；日期來源的自動參數型別固定為Varchar2。
- `ScheduleDate`：預定執行日。`PreviousWeekMonday`：預定日所屬週的前週一。`ThisWeekMonday`：預定日所屬週的本週一。
- `ThisMonthFirstDay`：台北基準日所屬月份的首日。`ThisMonthLastDay`：同月份的最後一天，包含月末當日，會自動處理大小月及閏年。
- 當月參數範例：基準日為民國`1151027`時，`sdte`選擇`ThisMonthFirstDay`、`edte`選擇`ThisMonthLastDay`，兩者型別均為`Varchar2`、日期格式均為`RepublicOfChina`，分別傳入`1151001`與`1151031`；改選`Gregorian`則為`20261001`與`20261031`。
- 當月來源的排程及補跑仍依原預定日計算；例如10月31日的預定於11月1日補跑，仍使用10月首末日。立即執行及測試查詢使用操作時刻，排隊跨月不改變基準。月末值包含當日，與週範圍的結束未滿不同；本功能不自動修改既有SQL的比較條件。
- `Gregorian`為西元8碼yyyyMMdd；`RepublicOfChina`為民國7碼YYYMMdd。週範圍使用「前週一以上、本週一未滿」。補跑仍以預定日計算；手動執行以操作時間計算。
- 新增業務SQL時先核對Oracle Reference、Index及query-rules；本功能不自動連線更新Schema，也不初始註冊範例業務SQL。
- 使用KTCC.Core 2.0.9既有Provider，連線與讀取集中於Alerts/Infrastructure/KTCC。唯讀Transaction在所有路徑Rollback，不使用Commit，不額外設定Command Timeout。Rollback失敗視為取得錯誤。

## 排程及保存

編輯規則的「排程（台北時間）」可選擇頻率：

- 「每週」：指定星期及時間，例如每週一09:00。
- 「每天」：指定每日時間，例如每天09:00，不需要選星期。
- 「每小時」：指定每小時的00至59分，例如15分會在09:15、10:15等時間執行；00分表示每小時整點。

未保存頻率的舊規則仍視為「每週」，保留原星期及時間。頻率與時間一併加密保存，編輯與複製會還原選項。

PC及應用程式持續運作時，每30秒檢查一次台北排程，實際開始可能稍晚於預定時間；查詢共用直列佇列。關閉告警視窗或把主畫面隱藏到通知區域，排程仍繼續。正式Close會取消查詢並等待Rollback及保存完成。

停止期間的預定只補最近一次，三種頻率皆適用。中斷但未完成的預定可重新執行；完成後按固定ID、設定版本及預定時間去重。手動執行不推進排程水位。單一起動使用Windows使用者專用Mutex與CurrentUserOnly Pipe，避免二重背景工作，第二實例只轉送顯示或通知履歷ID。

設定、固定值、摘要及取得明細均以CurrentUser DPAPI保存於 `%LOCALAPPDATA%\AutoCallGit\Alerts\`，只有同一Windows使用者可解密。暫存內容也已加密，原子取代正式檔。不要把加密檔案當成可跨使用者移轉的設定匯出。

履歷與明細保存90日，啟動及台北日期變更時清理專用保存目錄。設定及去重水位不隨履歷到期刪除。明細保存未完成不會確定正常或異常筆數；損壞、缺檔及保存失敗會回報錯誤。設定初始化失敗時編輯及執行停用，保留原檔供修復。

技術錯誤經既有Sanitizer去識別化後保存於 `%LOCALAPPDATA%\AutoCallGit\Logs\alerts.jsonl`。不保存原始Oracle例外、SQL、患者資料、真實參數或認證資訊。紀錄Writer可以由IAlertDiagnostics替換，紀錄本身失敗亦回報狀態。

## Windows通知及配布

使用Microsoft.WindowsAppSDK 2.5.1的AppNotificationManager。每次執行只傳送一筆警告名稱、分類及院區件數／失敗摘要，點擊參數只有履歷Guid。全部選定院區正常零筆時不通知。Windows通知停用或送出失敗不影響已保存履歷；通知失敗記錄於該次履歷。

配布對象為64位元Windows 10 1809以上及Windows 11，維持.NET 8與未封裝WinForms。需另外安裝與NuGet版本相符的Windows App Runtime 2.5及.NET 8 Desktop／ASP.NET Core Runtime，並保留目前外部DLL及KTCC依賴。一般權限執行；AppNotificationManager不支援提高權限執行的應用程式通知。程式明確初始化Runtime，缺少Runtime時保留既有功能與告警保存，但通知無法使用並顯示狀態。

來源：[通知初始化與點擊](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/app-notifications-dotnet)、[未封裝Runtime配布](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/use-windows-app-sdk-in-existing-project)。

## 驗證

Fake測試不連線Oracle，使用獨立Temp目錄及測試IPC識別。涵蓋台北週界、首次啟用、最近補跑、重啟去重、取消與接續、部分失敗、NULL與重複列、分割及跨片段分頁、Rollback與保存失敗、通知停用、DPAPI、90日清理、損壞檔案、Log遮罩及STA畫面生成。

建置／測試：

```powershell
dotnet build "Project auto push to gitlab.sln" --no-restore -m:1
dotnet test "SqlOptimizer.Tests/SqlOptimizer.Tests.csproj" --no-restore -m:1
```

NamedPipe測試需要目前Windows使用者的正常IPC權限，受限制的沙箱可能拒絕連線。Windows桌面通知的實際彈出及冷啟動點擊須在Runtime已導入且通知啟用的端末驗證。實Oracle驗證另行安排，禁止Commit，無法保證Rollback時使用Fake。

2026-10-05：SDK 10.0.401／.NET 8.0.31，Build無錯誤、保留原有116個警告；一般測試110個成功，其中告警20個，Oracle實DB測試1個略過。以STA生成B案及編輯視窗，驗證重複欄位及NULL。151個新增或修改的手寫Class／Method已確認XML、參數、回傳、UI對應及署名。Windows App Runtime 2.5尚未安裝於本驗證端末，實際通知彈出、結束後通知點擊及Visual Studio Designer內拖曳操作尚待端末確認。

2026-10-06：新增每天／每小時頻率，建置成功。新頻率的18個測試通過，涵蓋台北日界、恰好到期、00／59分、舊JSON相容、加密保存、補跑去重及STA畫面切換。全體127個測試在沙箱通過；獨立IPC測試因沙箱權限拒絕而改用一般Windows權限重新驗證，1個通過，合計128個通過，Oracle實DB測試1個略過。修改範圍13個手寫方法的XML、參數、回傳及UI對應已確認，保留原編碼與換行。實際檢查每週、每天及每小時的架空資料畫面；Visual Studio Designer內拖曳未另行驗證。

同日修正F5與一般重複起動的主畫面路由，並將起動要求改為既有Load完成後處理，避免畫面隱藏而未觸發Shown。新增4個起動測試，確認一般起動、通知履歷、Program入口及首次Load隱藏後的還原。最新全體132個通過、Oracle實DB1個略過；獨立IPC在一般Windows權限通過。修改的手寫處理已確認XML及UI對應，保留原編碼與換行。Visual Studio實際F5操作尚未驗證。
