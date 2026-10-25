using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// Reuses the AI teacher's record -> WAV -> Groq STT flow, without teacher dependencies.
[DefaultExecutionOrder(100)]
public class PrototypeVoiceControl : MonoBehaviour
{
    [Header("Target")]
    public PrototypeWheelchairController wheelchair;
    public PrototypeRouteNavigator navigation;
    [Tooltip("Use free movement commands plus named destination navigation.")]
    public bool useRouteNavigation = true;
    public KeyCode pushToTalkKey = KeyCode.V;

    [Header("Microphone")]
    [Tooltip("Leave empty to use the first microphone.")]
    public string microphoneName = "";
    public int sampleRate = 16000;
    [Range(1, 15)] public int maxRecordingSeconds = 10;
    [Min(0.1f)] public float minimumRecordingSeconds = 0.2f;
    [Range(0f, 0.1f)] public float silenceThreshold = 0.002f;

    [Header("Groq STT")]
    [Tooltip("Optional. Otherwise reads GROQ_API_KEY from the environment.")]
    [SerializeField] private string apiKeyOverride = "";
    public string transcriptionModel = "whisper-large-v3";
    [Range(3, 60)] public int requestTimeoutSeconds = 15;

    [Header("Groq Language Understanding")]
    public bool enableSemanticUnderstanding = true;
    public string intentModel = PrototypeVoiceIntentInterpreter.DefaultModel;
    [Range(3, 60)] public int intentTimeoutSeconds = 15;

    [Header("Free Movement (Route Navigation Disabled)")]
    [Range(0.1f, 3f)] public float moveDuration = 1f;
    [Range(0.1f, 3f)] public float turnDuration = 1.2f;
    [Range(0.1f, 1f)] public float inputStrength = 0.7f;
    public bool showStatusOverlay = true;

    [Header("Runtime Status")]
    [SerializeField] private string status = "按住 V 說話，放開後辨識";
    [SerializeField] private string lastTranscript = "";
    [SerializeField] private string understoodAction = "";
    public string Status { get { return status; } }
    public string LastTranscript { get { return lastTranscript; } }
    public bool IsRecording { get; private set; }
    public bool IsProcessing { get; private set; }

    private const string Endpoint = "https://api.groq.com/openai/v1/audio/transcriptions";
    private const string IntentEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    private AudioClip recording;
    private string activeMicrophone;
    private float recordingStartedAt;
    private int requestRevision;
    private UnityWebRequest pendingRequest;
    private Coroutine requestRoutine;
    private GUIStyle overlayStyle;
    private Font overlayFont;

    [Serializable] private class TranscriptResponse { public string text = ""; }
    [Serializable] private class ServiceError { public string code = ""; }
    [Serializable] private class ServiceErrorResponse { public ServiceError error = null; }

    private void Start()
    {
        if (wheelchair == null) wheelchair = FindObjectOfType<PrototypeWheelchairController>();
        if (navigation == null && wheelchair != null) navigation = wheelchair.GetComponent<PrototypeRouteNavigator>();
        if (wheelchair == null) status = "尚未指定輪椅控制器";
        else if (string.IsNullOrWhiteSpace(ResolveApiKey()))
            status = "請先設定 GROQ_API_KEY 或語音元件的 Api Key Override";
    }

    private void Update()
    {
        if (IsProcessing && wheelchair != null && wheelchair.ControlRevision != requestRevision)
            CancelPending("已取消辨識：由手推或鍵盤接手控制");

        if (wheelchair != null && Input.GetKeyDown(wheelchair.brakeKey))
        {
            CancelRecording();
            CancelPending("已煞車；按住 V 可重新錄音");
            return;
        }
        if (Input.GetKeyDown(pushToTalkKey)) StartRecording();
        if (!IsRecording) return;
        if (!Input.GetKey(pushToTalkKey)) FinishRecording();
        else if (Time.unscaledTime - recordingStartedAt >= maxRecordingSeconds)
            status = "已錄滿 " + maxRecordingSeconds + " 秒，放開 V 送出辨識";
    }

    private string ResolveApiKey()
    {
        return string.IsNullOrWhiteSpace(apiKeyOverride)
            ? Environment.GetEnvironmentVariable("GROQ_API_KEY") : apiKeyOverride.Trim();
    }

    private void StartRecording()
    {
        if (IsProcessing) { status = "正在辨識上一句，請稍候再按 V"; return; }
        if (wheelchair == null || !wheelchair.isActiveAndEnabled)
        { status = "找不到可用的輪椅控制器"; return; }
        if (Input.GetKey(wheelchair.brakeKey)) return;
        if (string.IsNullOrWhiteSpace(ResolveApiKey()))
        { status = "請先設定 GROQ_API_KEY 或語音元件的 Api Key Override"; return; }
        if (Microphone.devices.Length == 0) { status = "找不到麥克風，請確認裝置與麥克風權限"; return; }
        activeMicrophone = string.IsNullOrWhiteSpace(microphoneName) ? Microphone.devices[0] : microphoneName;
        if (Array.IndexOf(Microphone.devices, activeMicrophone) < 0)
        { status = "找不到指定麥克風，請檢查 Microphone Name"; return; }

        try
        {
            int frequency = Mathf.Clamp(sampleRate, 8000, 48000);
            Microphone.GetDeviceCaps(activeMicrophone, out int minimum, out int maximum);
            if (maximum > 0) frequency = Mathf.Clamp(frequency, minimum, maximum);
            recording = Microphone.Start(activeMicrophone, false, Mathf.Clamp(maxRecordingSeconds, 1, 15), frequency);
            if (recording == null) { status = "麥克風無法開始錄音"; return; }
            // Recording/listening does not own movement. The current command
            // continues until a new command is accepted or normal movement ends.
            recordingStartedAt = Time.unscaledTime;
            IsRecording = true;
            lastTranscript = "";
            understoodAction = "";
            status = "錄音中…維持原動作，說完放開 V；空白鍵立即停止";
        }
        catch (Exception)
        {
            CancelRecording();
            status = "無法開啟麥克風，請檢查裝置與權限";
        }
    }

    private void FinishRecording()
    {
        IsRecording = false;
        byte[] wav = null;
        try
        {
            int frames = Microphone.GetPosition(activeMicrophone);
            // A non-looping microphone returns position 0 after reaching its limit.
            if (frames <= 0 && Time.unscaledTime - recordingStartedAt >= recording.length)
                frames = recording.samples;
            Microphone.End(activeMicrophone);
            frames = Mathf.Clamp(frames, 0, recording.samples);
            if (frames / (float)recording.frequency < minimumRecordingSeconds)
            { status = "錄音太短，請按住 V 說完指令再放開"; return; }
            var samples = new float[frames * recording.channels];
            if (!recording.GetData(samples, 0))
            { status = "無法讀取錄音，請再試一次"; return; }
            wav = PrototypeVoiceAudio.EncodeWav(samples, recording.channels, recording.frequency, out float rms);
            if (rms < silenceThreshold)
            { status = "沒有聽到聲音，請靠近麥克風再試一次"; return; }
        }
        catch (Exception) { status = "錄音讀取失敗，請確認麥克風仍有連接"; return; }
        finally
        {
            if (!string.IsNullOrEmpty(activeMicrophone)) Microphone.End(activeMicrophone);
            if (recording != null) Destroy(recording);
            recording = null;
        }
        IsProcessing = true;
        requestRevision = wheelchair.ControlRevision;
        requestRoutine = StartCoroutine(Recognize(wav));
    }

    private IEnumerator Recognize(byte[] wav)
    {
        yield return RecognizeAndInterpret(wav);
        IsProcessing = false;
        requestRoutine = null;
    }

    private IEnumerator RecognizeAndInterpret(byte[] wav)
    {
        status = "辨識中…";
        var form = new WWWForm();
        form.AddBinaryData("file", wav, "command.wav", "audio/wav");
        form.AddField("model", string.IsNullOrWhiteSpace(transcriptionModel) ? "whisper-large-v3" : transcriptionModel);
        form.AddField("language", "zh");
        form.AddField("response_format", "json");
        form.AddField("temperature", "0");
        form.AddField("prompt", "輪椅控制情境。請保留完整句子、否定、先後順序與一點等用詞，勿摘要成口令，以繁體中文如實轉錄。可能談到直走、前後微調、回頭、停下、去前面路口後轉向、協助跨過門檻。" +
            (useRouteNavigation && navigation != null ? "目的地：" + navigation.DestinationHint() : ""));
        using (UnityWebRequest request = UnityWebRequest.Post(Endpoint, form))
        {
            pendingRequest = request;
            request.timeout = Mathf.Clamp(requestTimeoutSeconds, 3, 60);
            request.SetRequestHeader("Authorization", "Bearer " + ResolveApiKey());
            yield return request.SendWebRequest();
            pendingRequest = null;
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 401 || request.responseCode == 403)
                    status = "辨識服務拒絕存取，請確認 Groq API Key 與模型權限";
                else if (request.responseCode == 429) status = "辨識服務目前忙碌，請稍後重試";
                else status = "辨識失敗或逾時，請確認網路後再試（HTTP " + request.responseCode + "）";
                yield break;
            }
            if (wheelchair == null || !wheelchair.isActiveAndEnabled ||
                requestRevision != wheelchair.ControlRevision || Input.GetKey(wheelchair.brakeKey))
            { status = "已取消本次指令，請重新按住 V 錄音"; yield break; }

            TranscriptResponse response = null;
            try { response = JsonUtility.FromJson<TranscriptResponse>(request.downloadHandler.text); }
            catch (Exception) { }
            lastTranscript = response == null || response.text == null ? "" : response.text.Trim();
            if (useRouteNavigation)
            {
                if (navigation == null || !navigation.isActiveAndEnabled) status = "路線導航元件尚未啟用或指定";
                else if (navigation.TryHandleTranscript(lastTranscript, out string message))
                { status = message; understoodAction = message; }
                else if (enableSemanticUnderstanding && !string.IsNullOrWhiteSpace(lastTranscript))
                    yield return InterpretTranscript();
                else status = "未辨識為支援的移動指令，未變更原動作";
                yield break;
            }
            if (!PrototypeVoiceCommandParser.TryParse(lastTranscript, out PrototypeVoiceCommand command))
            { status = "未辨識為移動指令，請說：前進、後退、左轉、右轉或停止"; yield break; }
            ApplyCommand(command);
        }
    }

    private IEnumerator InterpretTranscript()
    {
        status = "正在理解你的意思…";
        string payload = PrototypeVoiceIntentInterpreter.BuildRequest(intentModel, lastTranscript,
            navigation.IntentContext, navigation.DestinationHint());
        using (var request = new UnityWebRequest(IntentEndpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + ResolveApiKey());
            request.timeout = Mathf.Clamp(intentTimeoutSeconds, 3, 60);
            pendingRequest = request;
            yield return request.SendWebRequest();
            pendingRequest = null;
            if (wheelchair == null || !wheelchair.isActiveAndEnabled || !isActiveAndEnabled ||
                requestRevision != wheelchair.ControlRevision || Input.GetKey(wheelchair.brakeKey))
            { status = "本次語意指令已取消，請重新錄音"; yield break; }
            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorCode = "";
                try { errorCode = JsonUtility.FromJson<ServiceErrorResponse>(request.downloadHandler.text)?.error?.code ?? ""; }
                catch (Exception) { }
                status = errorCode == "model_permission_blocked_project" ? "語意模型被 Groq 專案限制：請在 Projects → Limits 允許 " + intentModel :
                    errorCode == "model_permission_blocked_org" ? "語意模型被 Groq 組織限制，請由組織管理員啟用 " + intentModel :
                    request.responseCode == 401 || request.responseCode == 403 ? "語意服務拒絕存取，請確認 Groq Key 與 LLM 模型權限" :
                    request.responseCode == 429 ? "語意服務額度或頻率受限，未變更原動作，請稍後重試" :
                    "語意服務失敗或逾時，未變更原動作（HTTP " + request.responseCode + "）";
                yield break;
            }
            if (!PrototypeVoiceIntentInterpreter.TryReadCompletion(request.downloadHandler.text, out var intent, out string message))
            { status = message; understoodAction = "尚未執行，需要說明"; yield break; }
            PrototypeVoiceIntentInterpreter.Apply(intent, navigation, out message);
            status = message;
            understoodAction = message;
        }
    }

    private void ApplyCommand(PrototypeVoiceCommand command)
    {
        if (command == PrototypeVoiceCommand.CrossThreshold || command == PrototypeVoiceCommand.KeepLeft || command == PrototypeVoiceCommand.KeepRight)
        {
            if (navigation == null) status = "尚未指定輪椅的門檻協助元件";
            else { navigation.Execute(command); status = navigation.Status; }
            return;
        }
        if (PrototypeVoiceCommandParser.IsNudge(command))
        {
            if (navigation == null || !navigation.isActiveAndEnabled) status = "微調需要啟用輪椅的 Prototype Route Navigator";
            else { navigation.Execute(command); status = navigation.Status; }
            return;
        }
        if (command == PrototypeVoiceCommand.UTurn)
        { status = "掉頭功能需要啟用路線導航"; return; }
        if (command == PrototypeVoiceCommand.Stop)
        {
            wheelchair.StopMovement();
            status = "已停止";
            return;
        }
        float forward = command == PrototypeVoiceCommand.Forward ? inputStrength :
            command == PrototypeVoiceCommand.Backward ? -inputStrength : 0f;
        float turn = command == PrototypeVoiceCommand.Right ? -inputStrength :
            command == PrototypeVoiceCommand.Left ? inputStrength : 0f;
        float duration = turn == 0f ? moveDuration : turnDuration;
        bool accepted = wheelchair.SetVoiceInput(forward, turn, duration);
        status = accepted ? "已執行「" + PrototypeVoiceCommandParser.GetLabel(command) + "」，短暫移動後自動停止"
            : "目前由手推、鍵盤或煞車控制，未執行語音移動";
    }

    private void CancelRecording()
    {
        if (IsRecording && !string.IsNullOrEmpty(activeMicrophone)) Microphone.End(activeMicrophone);
        IsRecording = false;
        if (recording != null) Destroy(recording);
        recording = null;
    }

    private void CancelPending(string message)
    {
        if (pendingRequest != null) pendingRequest.Abort();
        if (requestRoutine != null) StopCoroutine(requestRoutine);
        pendingRequest?.Dispose();
        pendingRequest = null;
        requestRoutine = null;
        IsProcessing = false;
        status = message;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        CancelRecording();
        CancelPending("已暫停錄音與辨識；回到遊戲後重新按住 V");
        if (wheelchair != null) wheelchair.StopMovement();
    }

    private void OnDisable()
    {
        CancelRecording();
        CancelPending("語音控制已停用");
        if (wheelchair != null) wheelchair.CancelVoiceInput();
        if (navigation != null) navigation.StopNavigation("語音控制已停用");
    }

    private void OnDestroy()
    {
        if (overlayFont != null) Destroy(overlayFont);
    }

    private void OnGUI()
    {
        if (!showStatusOverlay) return;
        if (overlayStyle == null)
        {
            overlayFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Arial" }, 18);
            overlayStyle = new GUIStyle(GUI.skin.label) { font = overlayFont, fontSize = 18, wordWrap = true };
            overlayStyle.normal.textColor = Color.white;
        }
        float width = Mathf.Min(620f, Screen.width - 24f);
        string overlayText =
            "按住 V 說話，放開辨識｜空白鍵停止自動、切換手動\n" + status +
            "\n辨識文字：" + (string.IsNullOrEmpty(lastTranscript) ? "—" : lastTranscript) +
            "\n理解動作：" + (string.IsNullOrEmpty(understoodAction) ? "—" : understoodAction) +
            "\n自由：直走／前進／後退／左轉／右轉／回頭／停下來" +
            "\n微調：前進一點／後退一點／左轉一點／右轉一點；避讓：靠左一點／靠右一點" +
            (useRouteNavigation && navigation != null ? "\n移動：" + navigation.Status +
                "\n目的地：" + navigation.DestinationHint() : "");
        float height = overlayStyle.CalcHeight(new GUIContent(overlayText), width - 24f);
        GUI.Box(new Rect(12, 12, width, height + 20f), GUIContent.none);
        GUI.Label(new Rect(24, 20, width - 24f, height), overlayText, overlayStyle);
    }
}
