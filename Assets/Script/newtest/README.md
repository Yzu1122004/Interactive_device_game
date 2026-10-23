# prototype：手推輪椅、Arduino 與語音控制

`Assets/Scenes/prototype.unity` 已接好以下元件；其他場景與共用輪椅 Prefab 不需更動。

| 元件 | 掛載位置 | 功能 |
| --- | --- | --- |
| PrototypeArduinoConnection | PrototypeInput | 序列埠連線、背景接收、斷線清理 |
| PrototypeEncoderInput | PrototypeInput | 累積計數差分、方向反轉、前後與轉向輸入 |
| PrototypeWheelchairController | WhellChair | Rigidbody／WheelCollider 移動、平滑與煞車 |
| PrototypeVoiceControl | PrototypeInput | 按住 V 錄音、Groq STT、短暫語音移動與狀態提示 |
| PrototypeRouteNetwork | RouteNetwork | 路網起點、Scene 輔助線、距離最短路徑查詢 |
| PrototypeRouteNode | 起點、路口1～4、銀行 | 雙向路段連接與目的地名稱 |
| PrototypeRouteNavigator | WhellChair | 沿線行駛、路口選向、原地掉頭與目的地導航 |

## 編輯隱形路線

目前已依節點位置連成下列雙向路網，保留你原本擺放的位置：

```text
起點 ─ 路口1 ───── 路口3
         │           │
       路口2 ───── 路口4 ─ 銀行
```

- 在 **Scene** 視窗開啟 **Gizmos**，即可看見藍色路段、黃色起點、綠色目的地與名稱標籤。這些不是遊戲內的 LineRenderer，不會出現在建置後的畫面。
- 拖動原本的空物件即可調整路線；路段是兩點間的直線，彎道應增加中繼節點，避免連線切過路外。
- 在 Hierarchy 按住 **Ctrl** 選取兩個節點，再按 Inspector 的「連接選取的兩個節點」或「解除選取節點的連線」。也可使用 `Prototype > Routes` 選單。
- 單選節點，在 `Connections` 拖入其他節點也能連線；即使只填一側，也視為雙向通行。
- 新增空物件後，掛上 `PrototypeRouteNode`；或選取 `RouteNetwork`，按「替子物件加入路徑節點元件」。
- 目的地在 `Destination Name` 填名稱，例如「銀行」。一般轉彎點留白即可。`RouteNetwork > Start Node` 指定起點。
- 輪椅已掛上 `PrototypeRouteNavigator`，語音元件的 `Use Route Navigation` 預設開啟。開始導航後由自動控制接管；按空白鍵停止自動移動後，才開放手推／WASD 接手。

## 沿線移動指令

| 語音 | 行為 |
| --- | --- |
| 前進 | 沿目前面向進入路線，路口優先直行，持續行駛 |
| 後退 | 保持面向，沿背後的路線倒退行駛 |
| 左轉／右轉 | 取消目的地導航，在下一個有對應分支的路口轉向；此前若可直行則沿線繼續 |
| 掉頭／迴轉／轉半圈 | 停止平移，旋轉 180°，完成後正向沿路返回 |
| 去銀行／前往銀行／銀行 | 從目前路段規劃最短路徑，自動轉彎，到銀行停止 |
| 停止 | 取消路線行駛、微調及待執行轉向 |
| 前進一點／後退一點 | 取消導航，保持面向，以低速前後微調約 30 公分，再停住 |
| 左轉一點／右轉一點 | 取消導航，原地微調約 15°，再停住 |

左右以輪椅的行進方向和路網幾何位置判定。「前進」會選擇與來向最接近、偏轉不超過 60° 的連線，因此可沿一般彎折與繞路節點繼續行駛；90° 路口仍需轉向指令。路口沒有可直行或指定方向的路段時會停止，並區分「沒有可直行路段」與「路線盡頭」提示，不會自動掉頭。前往目的地時，則由規劃好的路徑自動選擇每個路口。

按住 V 開始錄音時會先暫停導航，放開後等待辨識，再依新指令繼續。錄音過短、無聲、辨識失敗或未知指令會保持暫停，請重新說「前進」、方向或目的地。自動移動與錄音暫停期間，手推及 WASD 輸入會被忽略，不會中斷導航。按空白鍵會煞車並取消自動移動，之後才可用手推／WASD；不會自行恢復導航。離開 Game 視窗也會取消導航。

導航參數位於 `WhellChair > Prototype Route Navigator`：目標速度 `Move Speed = 2.5` 公尺／秒，轉向目標上限 `Rotation Speed = 100` 度／秒，實際速度受輪子扭力、地形與碰撞影響。接近路口會減速，進入 `Arrival Tolerance = 0.5` 公尺的抵達範圍或在線附近越過節點時，會先煞車並記住已抵達，停穩後進入下個路段，避免因慣性滑過節點而掉頭追點。輪椅距離路線超過 `Max Attach Distance = 2` 公尺時會拒絕導航，需先手動回到線附近；不會瞬移回起點。

沿線行駛和手推共用原本的 WheelCollider 馬達與煞車，不切換 Kinematic、不停用輪子，也不直接改寫 Rigidbody 位置、角度或速度。Rigidbody 必須保持 `Use Gravity` 與 `Detect Collisions` 開啟、`Is Kinematic` 關閉；導航期間使用連續碰撞偵測。實際高度由重力、地面與輪子懸吊決定，不會鎖在節點高度。

導航前後速度以兩個驅動輪的平均 RPM 回授，避免把車身懸吊晃動當成輪速誤差；轉向則使用 Rigidbody 實際繞 Y 軸的角速度回授，避免輪子已反向轉動、車身卻轉不動。`Route Yaw Speed Gain = 40` 控制轉速誤差修正，`Route Turn Torque Limit = 65` 限制每輪的轉向扭力分量；前後驅動扭力仍由 `Max Torque` 限制。`Prototype Wheelchair Controller` 的 `Route Acceleration`、`Route Yaw Acceleration`、`Route Torque Change Rate` 控制起步、轉向和扭力的平滑程度。輪椅稍微偏離線時會朝前方約 2.5 公尺的接入點修正，不會先急轉到正側方；大角度轉向採不同的進入／離開門檻，避免反覆切換前進和轉向。

接地判斷會考慮所有啟用的 WheelCollider（含前方小輪）。輪子回報未接地時，會再向下探測輪子半徑、懸吊長度及 `Route Ground Probe Tolerance = 0.15` 公尺的範圍，忽略輪椅本身、Trigger 和陡直表面，避免車身仍受地面支撐卻誤判離地。接地判斷容許 `Ground Contact Grace Time = 0.25` 秒的短暫接地中斷。短暫中斷不會反覆煞車或把起步目標清零；連續離地超過寬限才逐步收回驅動，超過 1 秒則取消導航。輪子暫時離地時仍保留轉動命令，由 Unity 決定是否產生地面牽引力。

Unity Editor 中預設啟用 `Record Diagnostics In Editor`，每次開始導航會寫入 `Logs/prototype-route-physics.csv`，每 0.1 秒記錄位置、接地、接地來源（`wheel`／`probe`／`none`）、RPM、馬達扭力、煞車與目標速度，最多記錄約 90 秒。檔案不含 API Key、錄音或辨識文字，不會在建置後的遊戲啟用；可取消勾選以停用紀錄。

碰到前方阻擋物、掉頭時碰到側邊障礙，會停止導航並煞車；輪椅離地超過 1 秒也會取消導航，重力仍持續作用。目前不包含自動繞障，路線應事先擺在可通行的位置。障礙物需要實體 Collider 才能阻擋輪椅，Trigger 僅會觸發事件。路線輔助線不會出現在建置後畫面。

## 碰撞後微調

撞到障礙物停止後，按住 V 說「後退一點」、放開，先退開障礙；再用「左轉一點／右轉一點」調整朝向，必要時說「前進一點」。微調可離開路線，不受節點或目的地約束；重力、碰撞與輪子物理仍持續作用，不會瞬移或穿牆。向障礙物推進仍會停止；原地微轉允許既有接觸由物理解算處理，若被卡住則最多 8 秒停止。

「一點」與「一點點」會保留為微調指令；單說「前進、後退、左轉、右轉」仍為沿線行駛／路口轉向。「前進一下」沿用原本的沿線前進含義。微調完成會煞車停住，不會自動恢復舊路線；可接著微調，或說「前進／去銀行」重新接入路網。離線超過 2 公尺時仍需手動回到附近才能導航。按住 V 會暫停微調；微調期間手推／WASD 不會接手，空白鍵或「停止」可取消。

數值位於輪椅的 `Prototype Route Navigator > Obstacle Recovery Nudges`：`Nudge Distance = 0.3` 公尺、`Nudge Angle = 15` 度、`Nudge Move Speed = 0.35` 公尺／秒、`Nudge Rotation Speed = 35` 度／秒、`Nudge Timeout = 8` 秒。距離和角度為物理控制的近似目標，實際結果受煞車、接觸及地形影響。

## 展示步驟

1. Arduino 沿用 `Assets/Arduino/FINAL/FINAL.ino`，通訊速率 **9600 baud**。
2. 關閉 Arduino IDE 的序列埠監控視窗，避免占用 COM 埠。
3. 開啟 `prototype`，選取 `PrototypeInput`，將連線元件的 `Port` 設為實際埠號（目前沿用 **COM4**）。
4. 按 Play，等待約兩秒讓 Arduino 啟動；連線元件的 `Runtime Status` 可查看目前狀態。
5. 推動兩輪測試前後移動、單輪轉向與雙輪反向原地轉向。
6. 停推後輸入會於 0.12 秒到期，接著控制器平滑減速並施加待機煞車。

原場景校正值：`Invert Left Encoder` 開啟、`Invert Right Encoder` 關閉。如果某輪方向相反，調整對應選項。

保留鍵盤備用操作：W／S 前後、A／D 轉向、空白鍵煞車。取消輪椅控制器的 `Enable Keyboard Input` 可以關閉 WASD；空白鍵仍保留煞車。

## 語音展示

1. 在 `PrototypeInput > Prototype Voice Control` 的 `Api Key Override` 填入自己的 Groq API Key；也可以沿用 AI 老師的 `GROQ_API_KEY` 環境變數。新增環境變數後，重新啟動 Unity／Unity Hub 才會繼承設定。不要將真實 Key 提交到版本庫。
2. 接上麥克風。`Microphone Name` 留白會選第一個裝置；多裝置時可填指定裝置的完整名稱。
3. 按 Play，點選 Game 視窗，**按住 V 說話，放開 V 才送出辨識**。上方會顯示錄音、辨識結果與執行狀態。
4. 支援「前進／往前走」「後退」「左轉」「右轉」「掉頭」「停止」「去銀行」，以及四個方向的「一點」微調。也接受「請幫我前進一下」這類簡單口語包裝。
5. 預設沿線持續行駛。若取消 `Use Route Navigation`，可回到舊的自由移動展示：前後移動 1 秒、轉向 1.2 秒，結束後煞車；此模式不支援目的地與掉頭。

每段最多錄音 5 秒，錄滿仍須放開 V 才送出。錄音過短、音量太低或無法判讀時不移動。指令採完整比對，拒絕「不要前進」、連續多個動作及任意秒數／距離指令。

沿線自動移動與微調期間，手推或 WASD 不會取消語音移動；請先按空白鍵停止自動控制，再用手推／WASD。關閉 `Use Route Navigation` 的舊自由移動模式仍允許手動輸入取消語音動作。處於手動模式等待辨識時，若手推／WASD 接手，也會取消該筆辨識，避免較晚回傳的舊指令重新移動。空白鍵會取消錄音、辨識與語音動作。語音「停止」仍須等待網路辨識，立即煞車請使用空白鍵。離開 Game 視窗或切換應用程式會取消錄音／辨識並煞車。

這版沿用 AI 老師的 Unity 麥克風、WAV 與 Groq STT 流程；使用 `whisper-large-v3`、`language=zh`，不依賴老師角色、教材、情緒辨識、LLM 或 TTS。音訊只保留在記憶體並於放開 V 後上傳辨識，沒有另存錄音檔。服務參數見 [Groq Speech to Text 文件](https://console.groq.com/docs/speech-to-text)。

## 通訊與檢查

- Arduino 訊息格式為一行 `MOVE:左輪累積值,右輪累積值`，例如 `MOVE:-20,20`；並非單次位移值。
- 連線後發送原有的 `GAME_RESET`；第一筆有效計數作為基準，不產生移動。
- 非移動訊息（舊按鈕／技能訊息）會被忽略。
- 可從連線元件右上角選單執行 `Connect Arduino`、`Disconnect Arduino` 或 `Reset Arduino Counters`；拔插後用 Connect 重新連線。
- `Log Arduino Messages` 可顯示接收原文，預設關閉以免洗版。
- Unity 選單 `Prototype > Validate Input and Scene` 可檢查編碼器、語音指令、WAV 格式、路網連通性與場景引用；此檢查不會連接硬體或呼叫 API。
